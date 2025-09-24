﻿using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update.Internal;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
#pragma warning disable EF1001 // Suppress warning about internal APIs usage, common for providers/extensions
    public class TimescaleMigrationsModelDiffer(
        IRelationalTypeMappingSource typeMappingSource,
        IMigrationsAnnotationProvider migrationsAnnotationProvider,
        IRelationalAnnotationProvider relationalAnnotationProvider,
        IRowIdentityMapFactory rowIdentityMapFactory,
        CommandBatchPreparerDependencies commandBatchPreparerDependencies) : MigrationsModelDiffer(
              typeMappingSource,
              migrationsAnnotationProvider,
              relationalAnnotationProvider,
              rowIdentityMapFactory,
              commandBatchPreparerDependencies)
    {
        public override IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            // Get the standard migration operations (CreateTable, AddColumn, etc.) from the base MigrationsModelDiffer.
            List<MigrationOperation> operations = [.. base.GetDifferences(source, target)];

            // Hypertable diffs
            List<CreateHypertableOperation> targetHypertables = [.. GetHypertables(target)];
            List<CreateHypertableOperation> sourceHypertables = [.. GetHypertables(source)];

            // Identify new hypertables
            List<CreateHypertableOperation> newHypertables = [.. targetHypertables.Where(t => !sourceHypertables.Any(s => s.TableName == t.TableName))];

            foreach (CreateHypertableOperation? hypertable in newHypertables)
            {
                int createTableOpIndex = operations.FindIndex(op =>
                    op is CreateTableOperation createTable &&
                    createTable.Name == hypertable.TableName);

                if (createTableOpIndex != -1)
                {
                    operations.Insert(createTableOpIndex + 1, hypertable);
                }
            }

            // Identity updated hypertables
            var updatedHypertables = targetHypertables
                .Join(
                    sourceHypertables,
                    target => target.TableName,
                    source => source.TableName,
                    (target, source) => new { Target = target, Source = source }
                )
                .Where(x =>
                    x.Target.ChunkTimeInterval != x.Source.ChunkTimeInterval ||
                    x.Target.EnableCompression != x.Source.EnableCompression ||
                    !AreChunkSkipColumnsEqual(x.Target.ChunkSkipColumns, x.Source.ChunkSkipColumns)
                )
                .ToList();

            foreach (var hypertable in updatedHypertables)
            {
                AlterHypertableOperation alterOperation = new()
                {
                    TableName = hypertable.Target.TableName,
                    ChunkTimeInterval = hypertable.Target.ChunkTimeInterval,
                    EnableCompression = hypertable.Target.EnableCompression,
                    ChunkSkipColumns = hypertable.Target.ChunkSkipColumns,

                    OldChunkTimeInterval = hypertable.Source.ChunkTimeInterval,
                    OldEnableCompression = hypertable.Source.EnableCompression,
                    OldChunkSkipColumns = hypertable.Source.ChunkSkipColumns
                };

                operations.Add(alterOperation);
            }

            // Reorder diffs
            List<AddReorderPolicyOperation> sourcePolicies = [.. GetReorderPolicies(source)];
            List<AddReorderPolicyOperation> targetPolicies = [.. GetReorderPolicies(target)];

            // Identiy new reorder policies
            IEnumerable<AddReorderPolicyOperation> newReorderPolicies = targetPolicies.Where(t => !sourcePolicies.Any(s => s.TableName == t.TableName));
            operations.AddRange(newReorderPolicies);

            // Identify updated reorder policies
            var updatedReorderPolicies = targetPolicies
                .Join(
                    sourcePolicies,
                    targetPolicy => targetPolicy.TableName,
                    sourcePolicy => sourcePolicy.TableName,
                    (targetPolicy, sourcePolicy) => new { Target = targetPolicy, Source = sourcePolicy }
                )
                .Where(x =>
                    x.Target.IndexName != x.Source.IndexName ||
                    x.Target.InitialStart != x.Source.InitialStart ||
                    x.Target.ScheduleInterval != x.Source.ScheduleInterval ||
                    x.Target.MaxRuntime != x.Source.MaxRuntime ||
                    x.Target.MaxRetries != x.Source.MaxRetries ||
                    x.Target.RetryPeriod != x.Source.RetryPeriod
                );

            foreach (var policy in updatedReorderPolicies)
            {
                operations.Add(new AlterReorderPolicyOperation
                {
                    TableName = policy.Target.TableName,
                    IndexName = policy.Target.IndexName,
                    InitialStart = policy.Target.InitialStart,
                    ScheduleInterval = policy.Target.ScheduleInterval,
                    MaxRuntime = policy.Target.MaxRuntime,
                    MaxRetries = policy.Target.MaxRetries,
                    RetryPeriod = policy.Target.RetryPeriod,

                    OldIndexName = policy.Source.IndexName,
                    OldInitialStart = policy.Source.InitialStart,
                    OldScheduleInterval = policy.Source.ScheduleInterval,
                    OldMaxRuntime = policy.Source.MaxRuntime,
                    OldMaxRetries = policy.Source.MaxRetries,
                    OldRetryPeriod = policy.Source.RetryPeriod
                });
            }

            IEnumerable<DropReorderPolicyOperation> removedReorderPolicies = sourcePolicies
                .Where(s => !targetPolicies.Any(t => t.TableName == s.TableName))
                .Select(p => new DropReorderPolicyOperation { TableName = p.TableName });
            operations.AddRange(removedReorderPolicies);

            return operations;
        }

        // Helper method to extract hypertable configuration from an IRelationalModel
        private static IEnumerable<CreateHypertableOperation> GetHypertables(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }

            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                // Retrieve the annotations set by the convention
                bool isHypertable = entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value as bool? ?? false;
                string? timeColumnName = entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value as string;

                string chunkTimeInterval = entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value as string ?? DefaultValues.ChunkTimeInterval;

                string? chunkSkipColumnsString = entityType.FindAnnotation(HypertableAnnotations.ChunkSkipColumns)?.Value as string;
                List<string>? chunkSkipColumns = chunkSkipColumnsString?.Split(',', StringSplitOptions.TrimEntries).ToList();

                bool enableCompression = entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value as bool? ?? false;

                IAnnotation? additionalDimensionsAnnotations = entityType.FindAnnotation(HypertableAnnotations.AdditionalDimensions);
                List<Dimension>? additionalDimensions = null;

                if (additionalDimensionsAnnotations?.Value is string json && !string.IsNullOrWhiteSpace(json))
                {
                    additionalDimensions = JsonSerializer.Deserialize<List<Dimension>>(json);
                }

                if (isHypertable && !string.IsNullOrWhiteSpace(timeColumnName))
                {
                    yield return new CreateHypertableOperation
                    {
                        TableName = entityType.GetTableName()!,
                        TimeColumnName = timeColumnName,
                        ChunkTimeInterval = chunkTimeInterval ?? DefaultValues.ChunkTimeInterval,
                        EnableCompression = enableCompression,
                        ChunkSkipColumns = chunkSkipColumns,
                        AdditionalDimensions = additionalDimensions
                    };
                }
            }
        }

        private static IEnumerable<AddReorderPolicyOperation> GetReorderPolicies(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }
            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                // Retrieve the annotations set by the convention
                bool hasReorderPolicy = entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value as bool? ?? false;
                string? indexName = entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value as string;
                DateTime? initialStart = entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value as DateTime?;

                if (hasReorderPolicy && !string.IsNullOrWhiteSpace(indexName) && !string.IsNullOrWhiteSpace(indexName))
                {
                    yield return new AddReorderPolicyOperation
                    {
                        TableName = entityType.GetTableName()!,
                        IndexName = indexName!,
                        InitialStart = initialStart,
                        ScheduleInterval = entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value as string ?? DefaultValues.ReorderPolicyScheduleInterval,
                        MaxRuntime = entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value as string ?? DefaultValues.ReorderPolicyMaxRuntime,
                        MaxRetries = entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value as int? ?? DefaultValues.ReorderPolicyMaxRetries,
                        RetryPeriod = entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value as string ?? DefaultValues.ReorderPolicyRetryPeriod
                    };
                }
            }
        }

        // Helper method to compare two lists of chunk skip columns
        private static bool AreChunkSkipColumnsEqual(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;

            HashSet<string> set1 = [.. list1];
            return set1.SetEquals(list2);
        }
    }
#pragma warning restore EF1001
}
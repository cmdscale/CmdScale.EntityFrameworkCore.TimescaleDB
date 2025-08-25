using CmdScale.EntityFrameworkCore.TimescaleDB.Annotation;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update.Internal;

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

                if (isHypertable && !string.IsNullOrWhiteSpace(timeColumnName))
                {
                    yield return new CreateHypertableOperation
                    {
                        TableName = entityType.GetTableName()!,
                        TimeColumnName = timeColumnName,
                        ChunkTimeInterval = chunkTimeInterval,
                        EnableCompression = enableCompression,
                        ChunkSkipColumns = chunkSkipColumns
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
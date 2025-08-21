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

            // --- Custom TimescaleDB Operations ---

            // Identify Hypertables in the target model that are new (or newly configured) compared to the source model.
            List<CreateHypertableOperation> newHypertables = [.. GetHypertables(target)
                .Where(targetHypertable =>
                {
                    // Check if this hypertable (table name and time column) existed in the source model
                    // and if its configuration has changed significantly.

                    CreateHypertableOperation? sourceHypertable = GetHypertables(source)
                                            .FirstOrDefault(s =>
                                                s.TableName == targetHypertable.TableName &&
                                                s.TimeColumnName == targetHypertable.TimeColumnName);

                    // If it's completely new or a 'regular' table becoming a hypertable
                    return sourceHypertable == null;
                })];

            // Add CreateHypertable operations for new hypertables
            foreach (CreateHypertableOperation? hypertable in newHypertables)
            {
                // Find the index of the CreateTableOperation for this table.
                int createTableOpIndex = operations.FindIndex(op =>
                    op is CreateTableOperation createTable &&
                    createTable.Name == hypertable.TableName);

                if (createTableOpIndex != -1)
                {
                    operations.Insert(createTableOpIndex + 1, new CreateHypertableOperation
                    {
                        TableName = hypertable.TableName,
                        TimeColumnName = hypertable.TimeColumnName
                    });
                }
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

                if (isHypertable && !string.IsNullOrWhiteSpace(timeColumnName))
                {
                    yield return new CreateHypertableOperation
                    {
                        TableName = entityType.GetTableName()!,
                        TimeColumnName = timeColumnName,
                    };
                }
            }
        }
    }
#pragma warning restore EF1001
}
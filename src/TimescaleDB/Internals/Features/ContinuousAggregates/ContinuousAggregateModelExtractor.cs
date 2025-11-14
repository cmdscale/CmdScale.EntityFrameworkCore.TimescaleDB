using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates
{
    internal class ContinuousAggregateModelExtractor
    {
        public static IEnumerable<CreateContinuousAggregateOperation> GetContinuousAggregates(IRelationalModel? relationalModel)
        {
            if (relationalModel == null)
            {
                yield break;
            }

            foreach (IEntityType entityType in relationalModel.Model.GetEntityTypes())
            {
                // Check if this entity is configured as a continuous aggregate
                string? materializedViewName = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value as string;
                if (string.IsNullOrWhiteSpace(materializedViewName))
                {
                    continue;
                }

                // Get the parent (source) entity name
                string? parentModelName = entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value as string;
                if (string.IsNullOrWhiteSpace(parentModelName))
                {
                    continue;
                }

                // Find the parent entity type to get its table name
                IEntityType? parentEntityType = relationalModel.Model.GetEntityTypes()
                    .FirstOrDefault(e => e.ClrType?.Name == parentModelName || e.ShortName() == parentModelName);
                if (parentEntityType == null)
                {
                    continue;
                }

                string? parentTableName = parentEntityType.GetTableName();
                if (string.IsNullOrWhiteSpace(parentTableName))
                {
                    continue;
                }

                // Get time bucket configuration
                string? timeBucketWidth = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value as string;
                if (string.IsNullOrWhiteSpace(timeBucketWidth))
                {
                    continue;
                }

                string? timeBucketSourceColumnModelName = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value as string;
                if (string.IsNullOrWhiteSpace(timeBucketSourceColumnModelName))
                {
                    continue;
                }

                // Get convention-aware store identifier for the parent table
                StoreObjectIdentifier parentStoreIdentifier = StoreObjectIdentifier.Table(parentTableName, parentEntityType.GetSchema());

                // Resolve time bucket source column to database column name
                string? timeBucketSourceColumn = parentEntityType.FindProperty(timeBucketSourceColumnModelName)?.GetColumnName(parentStoreIdentifier);
                if (string.IsNullOrWhiteSpace(timeBucketSourceColumn))
                {
                    continue;
                }

                // Get optional configuration
                bool timeBucketGroupBy = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy)?.Value as bool? ?? true;
                string? chunkInterval = entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value as string;
                bool withNoData = entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value as bool? ?? false;
                bool createGroupIndexes = entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value as bool? ?? false;
                bool materializedOnly = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value as bool? ?? false;
                string? whereClause = entityType.FindAnnotation(ContinuousAggregateAnnotations.WhereClause)?.Value as string;

                // Process aggregate functions - convert model property names to database column names
                List<string> aggregateFunctions = [];
                IAnnotation? aggregateFunctionsAnnotation = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions);
                if (aggregateFunctionsAnnotation?.Value is List<string> modelAggregateFunctions)
                {
                    foreach (string aggInfo in modelAggregateFunctions)
                    {
                        string[] parts = aggInfo.Split(':');
                        if (parts.Length != 3)
                        {
                            // Skip malformed string
                            continue;
                        }

                        string aliasModelName = parts[0];
                        string functionEnumString = parts[1];
                        string sourceColumnModelName = parts[2];

                        // Resolve source column name from parent entity
                        string? sourceColumnDbName = parentEntityType.FindProperty(sourceColumnModelName)?.GetColumnName(parentStoreIdentifier);
                        if (string.IsNullOrWhiteSpace(sourceColumnDbName))
                        {
                            // Skip if source column not found
                            continue;
                        }

                        // Alias stays as-is since it's the target column name in the aggregate view
                        aggregateFunctions.Add($"{aliasModelName}:{functionEnumString}:{sourceColumnDbName}");
                    }
                }

                // Process group by columns - convert model property names to database column names
                // Note: Some group by columns might be raw SQL expressions (e.g., "1, 2"), not property names
                List<string> groupByColumns = [];
                IAnnotation? groupByColumnsAnnotation = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
                if (groupByColumnsAnnotation?.Value is List<string> modelGroupByColumns)
                {
                    foreach (string modelColumn in modelGroupByColumns)
                    {
                        // Try to resolve as a property name from the parent entity
                        string? dbColumnName = parentEntityType.FindProperty(modelColumn)?.GetColumnName(parentStoreIdentifier);

                        if (!string.IsNullOrWhiteSpace(dbColumnName))
                        {
                            // It's a property name, use the resolved database column name
                            groupByColumns.Add(dbColumnName);
                        }
                        else
                        {
                            // It's not a property, assume it's a raw SQL expression and use as-is
                            groupByColumns.Add(modelColumn);
                        }
                    }
                }

                // Use parent table's schema for the continuous aggregate
                string schema = parentEntityType.GetSchema() ?? entityType.GetSchema() ?? DefaultValues.DefaultSchema;

                yield return new CreateContinuousAggregateOperation
                {
                    Schema = schema,
                    MaterializedViewName = materializedViewName,
                    ParentName = parentTableName,
                    ChunkInterval = chunkInterval,
                    WithNoData = withNoData,
                    CreateGroupIndexes = createGroupIndexes,
                    MaterializedOnly = materializedOnly,
                    TimeBucketWidth = timeBucketWidth,
                    TimeBucketSourceColumn = timeBucketSourceColumn,
                    TimeBucketGroupBy = timeBucketGroupBy,
                    AggregateFunctions = aggregateFunctions,
                    GroupByColumns = groupByColumns,
                    WhereClaus = whereClause
                };
            }
        }
    }
}

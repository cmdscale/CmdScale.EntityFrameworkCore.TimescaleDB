using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates
{
    internal class ContinuousAggregateModelExtractor
    {
        public static IEnumerable<CreateContinuousAggregateOperation> GetContinuousAggregates(IRelationalModel? relationalModel)
            => SortParentsFirst([.. ExtractContinuousAggregates(relationalModel)]);

        private static IEnumerable<CreateContinuousAggregateOperation> ExtractContinuousAggregates(IRelationalModel? relationalModel)
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

                // Find the parent entity type
                IEntityType? parentEntityType = ParentEntityTypeResolver.Resolve(relationalModel.Model, parentModelName);
                if (parentEntityType == null)
                {
                    continue;
                }

                // A parent mapped to a view is itself a continuous aggregate (hierarchical aggregation),
                // so the relational name and store identifier must come from the view mapping.
                string? parentTableName = parentEntityType.GetTableName();
                string? parentRelationalName = parentTableName ?? parentEntityType.GetViewName();
                if (string.IsNullOrWhiteSpace(parentRelationalName))
                {
                    continue;
                }

                // Get time bucket configuration
                string? viewDefinition = entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value as string;
                bool useRawDefinition = !string.IsNullOrWhiteSpace(viewDefinition);

                // Get time bucket configuration
                string? timeBucketWidth = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth)?.Value as string;
                string? timeBucketSourceColumnModelName = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value as string;
                string? timeBucketTargetPropertyName = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketTargetProperty)?.Value as string;
                if (!useRawDefinition && (string.IsNullOrWhiteSpace(timeBucketWidth) || string.IsNullOrWhiteSpace(timeBucketSourceColumnModelName)))
                {
                    continue;
                }

                // Get convention-aware store identifier for the parent table or view
                StoreObjectIdentifier parentStoreIdentifier = parentTableName != null
                    ? StoreObjectIdentifier.Table(parentTableName, parentEntityType.GetSchema())
                    : StoreObjectIdentifier.View(parentRelationalName, parentEntityType.GetViewSchema() ?? parentEntityType.GetSchema());

                string? viewName = entityType.GetViewName() ?? materializedViewName;
                StoreObjectIdentifier aggregateStoreIdentifier = StoreObjectIdentifier.View(viewName, entityType.GetViewSchema() ?? entityType.GetSchema());

                // Resolve time bucket source column to database column name. Skipped on
                // the raw-definition path because the structured field is unused.
                string? timeBucketSourceColumn = useRawDefinition
                    ? null
                    : ColumnNameResolver.Resolve(parentEntityType, timeBucketSourceColumnModelName!, parentStoreIdentifier);
                if (!useRawDefinition && string.IsNullOrWhiteSpace(timeBucketSourceColumn))
                {
                    continue;
                }

                // Resolve the bucket output column alias from the designated target property.
                string timeBucketColumnName = DefaultValues.ContinuousAggregateTimeBucketColumnName;
                if (!useRawDefinition && !string.IsNullOrWhiteSpace(timeBucketTargetPropertyName))
                {
                    string? resolvedBucketColumn = ColumnNameResolver.Resolve(entityType, timeBucketTargetPropertyName, aggregateStoreIdentifier);
                    if (!string.IsNullOrWhiteSpace(resolvedBucketColumn))
                    {
                        timeBucketColumnName = resolvedBucketColumn;
                    }
                }

                // Get optional configuration
                bool timeBucketGroupBy = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy)?.Value as bool? ?? true;
                string? chunkInterval = entityType.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value as string;
                bool withNoData = entityType.FindAnnotation(ContinuousAggregateAnnotations.WithNoData)?.Value as bool? ?? false;
                bool createGroupIndexes = entityType.FindAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value as bool? ?? false;
                bool materializedOnly = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedOnly)?.Value as bool? ?? false;
                string? whereClause = entityType.FindAnnotation(ContinuousAggregateAnnotations.WhereClause)?.Value as string;

                List<string> aggregateFunctions = ResolveAggregateFunctions(entityType, parentEntityType, parentStoreIdentifier, aggregateStoreIdentifier);
                List<string> groupByColumns = ResolveGroupByColumns(entityType, parentEntityType, parentStoreIdentifier);

                // Schema resolution: prefer the CA's own view schema (set by .ToView(...)
                // or by the scaffolder), fall back to the parent's schema, finally default.
                string schema = entityType.GetViewSchema()
                    ?? entityType.GetSchema()
                    ?? parentEntityType.GetViewSchema()
                    ?? parentEntityType.GetSchema()
                    ?? DefaultValues.DefaultSchema;

                bool enableCompression = entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value as bool? ?? false;
                List<string>? compressionSegmentBy = CompressionAnnotationExtractor.ExtractSegmentByColumns(entityType, aggregateStoreIdentifier);
                List<string>? compressionOrderBy = CompressionAnnotationExtractor.ExtractOrderByColumns(entityType, aggregateStoreIdentifier);

                yield return new CreateContinuousAggregateOperation
                {
                    Schema = schema,
                    MaterializedViewName = materializedViewName,
                    ParentName = parentRelationalName,
                    ChunkInterval = chunkInterval,
                    WithNoData = withNoData,
                    CreateGroupIndexes = createGroupIndexes,
                    MaterializedOnly = materializedOnly,
                    TimeBucketWidth = timeBucketWidth ?? string.Empty,
                    TimeBucketSourceColumn = timeBucketSourceColumn ?? string.Empty,
                    TimeBucketColumnName = timeBucketColumnName,
                    TimeBucketGroupBy = timeBucketGroupBy,
                    AggregateFunctions = aggregateFunctions,
                    GroupByColumns = groupByColumns,
                    WhereClause = whereClause,
                    ViewDefinition = useRawDefinition ? viewDefinition : null,
                    EnableCompression = enableCompression || compressionSegmentBy?.Count > 0 || compressionOrderBy?.Count > 0,
                    CompressionSegmentBy = compressionSegmentBy,
                    CompressionOrderBy = compressionOrderBy,
                };
            }
        }

        private static List<string> ResolveAggregateFunctions(
            IEntityType entityType,
            IEntityType parentEntityType,
            StoreObjectIdentifier parentStoreIdentifier,
            StoreObjectIdentifier aggregateStoreIdentifier)
        {
            List<string> aggregateFunctions = [];
            IAnnotation? aggregateFunctionsAnnotation = entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions);
            if (aggregateFunctionsAnnotation?.Value is not List<string> modelAggregateFunctions)
            {
                return aggregateFunctions;
            }

            foreach (string aggInfo in modelAggregateFunctions)
            {
                string[] parts = aggInfo.Split(':');
                if (parts.Length != 3)
                {
                    continue;
                }

                string aliasModelName = parts[0];
                string functionEnumString = parts[1];
                string sourceColumnModelName = parts[2];

                // Resolve source column name from parent entity. "*" is not a column
                // but the COUNT(*) wildcard, so it bypasses resolution.
                string? sourceColumnDbName = sourceColumnModelName == "*"
                    ? "*"
                    : ColumnNameResolver.Resolve(parentEntityType, sourceColumnModelName, parentStoreIdentifier);
                if (string.IsNullOrWhiteSpace(sourceColumnDbName))
                {
                    continue;
                }

                // Resolve alias column name from aggregate entity to respect naming conventions
                string? aliasDbName = ColumnNameResolver.Resolve(entityType, aliasModelName, aggregateStoreIdentifier);
                if (string.IsNullOrWhiteSpace(aliasDbName))
                {
                    aliasDbName = aliasModelName;
                }

                aggregateFunctions.Add($"{aliasDbName}:{functionEnumString}:{sourceColumnDbName}");
            }

            return aggregateFunctions;
        }

        /// <summary>
        /// Orders operations so that a continuous aggregate precedes any aggregate built on top of it
        /// (hierarchical aggregation). Aggregates are matched by <c>MaterializedViewName</c>, mirroring
        /// how the differ pairs source and target aggregates.
        /// </summary>
        private static List<CreateContinuousAggregateOperation> SortParentsFirst(List<CreateContinuousAggregateOperation> operations)
        {
            Dictionary<string, CreateContinuousAggregateOperation> byViewName = [];
            foreach (CreateContinuousAggregateOperation operation in operations)
            {
                byViewName.TryAdd(operation.MaterializedViewName, operation);
            }

            List<CreateContinuousAggregateOperation> sorted = [];
            HashSet<string> visited = [];

            void Visit(CreateContinuousAggregateOperation operation)
            {
                if (!visited.Add(operation.MaterializedViewName))
                {
                    return;
                }

                if (byViewName.TryGetValue(operation.ParentName, out CreateContinuousAggregateOperation? parent))
                {
                    Visit(parent);
                }

                sorted.Add(operation);
            }

            foreach (CreateContinuousAggregateOperation operation in operations)
            {
                Visit(operation);
            }

            return sorted;
        }

        private static List<string> ResolveGroupByColumns(
            IEntityType entityType,
            IEntityType parentEntityType,
            StoreObjectIdentifier parentStoreIdentifier)
        {
            List<string> groupByColumns = [];
            IAnnotation? groupByColumnsAnnotation = entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
            if (groupByColumnsAnnotation?.Value is not List<string> modelGroupByColumns)
            {
                return groupByColumns;
            }

            foreach (string modelColumn in modelGroupByColumns)
            {
                // Try to resolve as a property name from the parent entity
                string? dbColumnName = ColumnNameResolver.Resolve(parentEntityType, modelColumn, parentStoreIdentifier);
                groupByColumns.Add(!string.IsNullOrWhiteSpace(dbColumnName) ? dbColumnName : modelColumn);
            }

            return groupByColumns;
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration
{
    /// <summary>
    /// Guards against colliding output column names in a structured continuous aggregate view.
    /// The bucket column, group-by columns and aggregate aliases share the view's projection, so
    /// two of them resolving to the same database column would produce ambiguous SQL. Validation
    /// runs at model finalization against resolved store column names.
    /// </summary>
    internal class ContinuousAggregateViewColumnValidationConvention : IModelFinalizedConvention
    {
        /// <summary>
        /// Called once the model has been finalized and relational type mappings are resolved.
        /// </summary>
        /// <param name="model">The finalized model.</param>
        /// <returns>The unchanged model.</returns>
        public IModel ProcessModelFinalized(IModel model)
        {
            foreach (IEntityType entityType in model.GetEntityTypes())
            {
                ValidateViewColumns(model, entityType);
            }

            return model;
        }

        private static void ValidateViewColumns(IModel model, IEntityType entityType)
        {
            string? materializedViewName = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value as string;
            if (string.IsNullOrWhiteSpace(materializedViewName))
            {
                return;
            }

            // The raw view-definition path does not use the structured projection fields.
            string? viewDefinition = entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value as string;
            if (!string.IsNullOrWhiteSpace(viewDefinition))
            {
                return;
            }

            StoreObjectIdentifier? aggregateStoreIdentifier = EntityStoreObjectResolver.GetStoreObjectIdentifier(entityType);
            if (aggregateStoreIdentifier == null)
            {
                return;
            }

            string bucketColumnName = ResolveBucketColumnName(entityType, aggregateStoreIdentifier.Value, materializedViewName);

            IEntityType? parentEntityType = ResolveParent(model, entityType);
            StoreObjectIdentifier? parentStoreIdentifier = parentEntityType == null ? null : EntityStoreObjectResolver.GetStoreObjectIdentifier(parentEntityType);

            List<string> outputColumns = [bucketColumnName];
            outputColumns.AddRange(ResolveGroupByColumns(entityType, parentEntityType, parentStoreIdentifier));
            outputColumns.AddRange(ResolveAggregateAliasColumns(entityType, aggregateStoreIdentifier.Value));

            HashSet<string> seen = [];
            foreach (string column in outputColumns)
            {
                if (!seen.Add(column))
                {
                    throw new InvalidOperationException(
                        $"The continuous aggregate '{EntityStoreObjectResolver.DisplayName(entityType)}' (materialized view '{materializedViewName}') " +
                        $"produces the output column '{column}' more than once. Rename the conflicting property or use " +
                        $"WithTimeBucketProperty to map the bucket column to a distinct property.");
                }
            }
        }

        /// <summary>
        /// Resolves the bucket output column from the designated target property, falling back to the
        /// function-name-derived default. Throws when the annotation names a property that does not exist.
        /// </summary>
        private static string ResolveBucketColumnName(IEntityType entityType, StoreObjectIdentifier aggregateStoreIdentifier, string materializedViewName)
        {
            string? targetPropertyName = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketTargetProperty)?.Value as string;
            if (string.IsNullOrWhiteSpace(targetPropertyName))
            {
                return DefaultValues.ContinuousAggregateTimeBucketColumnName;
            }

            IProperty? property = ColumnNameResolver.ResolveProperty(entityType, targetPropertyName, aggregateStoreIdentifier);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"The continuous aggregate '{EntityStoreObjectResolver.DisplayName(entityType)}' (materialized view '{materializedViewName}') " +
                    $"designates '{targetPropertyName}' as its time-bucket property, but no such property exists on the entity.");
            }

            string? columnName = property.GetColumnName(aggregateStoreIdentifier);
            return string.IsNullOrWhiteSpace(columnName)
                ? DefaultValues.ContinuousAggregateTimeBucketColumnName
                : columnName;
        }

        /// <summary>
        /// Resolves the group-by output columns against the parent entity, skipping raw SQL expressions
        /// (entries containing a comma, parenthesis, or space) that are not plain columns.
        /// </summary>
        private static IEnumerable<string> ResolveGroupByColumns(IEntityType entityType, IEntityType? parentEntityType, StoreObjectIdentifier? parentStoreIdentifier)
        {
            if (entityType.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns)?.Value is not List<string> modelGroupByColumns)
            {
                yield break;
            }

            foreach (string modelColumn in modelGroupByColumns)
            {
                bool isRawSqlExpression = modelColumn.Contains(',') || modelColumn.Contains('(') || modelColumn.Contains(' ');
                if (isRawSqlExpression)
                {
                    continue;
                }

                string? dbColumnName = parentEntityType == null || parentStoreIdentifier == null
                    ? null
                    : ColumnNameResolver.Resolve(parentEntityType, modelColumn, parentStoreIdentifier.Value);
                yield return string.IsNullOrWhiteSpace(dbColumnName) ? modelColumn : dbColumnName;
            }
        }

        /// <summary>
        /// Resolves the aggregate alias output columns against the aggregate entity. The annotation stores
        /// entries in "alias:function:source" form; only the alias participates in the view projection.
        /// </summary>
        private static IEnumerable<string> ResolveAggregateAliasColumns(IEntityType entityType, StoreObjectIdentifier aggregateStoreIdentifier)
        {
            if (entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value is not List<string> modelAggregateFunctions)
            {
                yield break;
            }

            foreach (string aggInfo in modelAggregateFunctions)
            {
                string[] parts = aggInfo.Split(':');
                if (parts.Length != 3)
                {
                    continue;
                }

                string aliasModelName = parts[0];
                string? aliasDbName = ColumnNameResolver.Resolve(entityType, aliasModelName, aggregateStoreIdentifier);
                yield return string.IsNullOrWhiteSpace(aliasDbName) ? aliasModelName : aliasDbName;
            }
        }

        private static IEntityType? ResolveParent(IModel model, IEntityType entityType)
        {
            string? parentName = entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value as string;
            return string.IsNullOrWhiteSpace(parentName) ? null : ParentEntityTypeResolver.Resolve(model, parentName);
        }
    }
}

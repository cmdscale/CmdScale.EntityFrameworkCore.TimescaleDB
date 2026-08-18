using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration
{
    /// <summary>
    /// This is the authoritative guardrail for time-column types: the fluent API accepts any .NET type 
    /// so that custom mappings (such as the Npgsql NodaTime plugin) work, and correctness is enforced 
    /// here against the resolved store type.
    /// </summary>
    public class TimeColumnStoreTypeValidationConvention : IModelFinalizedConvention
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
                ValidateHypertableTimeColumn(entityType);
                ValidateContinuousAggregateTimeColumn(model, entityType);
            }

            return model;
        }

        private static void ValidateHypertableTimeColumn(IEntityType entityType)
        {
            bool isHypertable = entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value as bool? ?? false;
            if (!isHypertable)
            {
                return;
            }

            string? timeColumnName = entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value as string;
            if (string.IsNullOrWhiteSpace(timeColumnName))
            {
                return;
            }

            IProperty? property = ColumnNameResolver.ResolveProperty(entityType, timeColumnName, GetStoreObjectIdentifier(entityType));
            if (property == null)
            {
                // Unresolvable column names are left to the model extractor, which skips them; this keeps
                // behaviour consistent and avoids false positives on design-time/scaffolded models.
                return;
            }

            EnsureValidTimeColumn(property, $"hypertable '{DisplayName(entityType)}'", timeColumnName);
        }

        private static void ValidateContinuousAggregateTimeColumn(IModel model, IEntityType entityType)
        {
            string? materializedViewName = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value as string;
            if (string.IsNullOrWhiteSpace(materializedViewName))
            {
                return;
            }

            // The raw view-definition path does not use the structured time-bucket source column.
            string? viewDefinition = entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value as string;
            if (!string.IsNullOrWhiteSpace(viewDefinition))
            {
                return;
            }

            string? sourceColumnName = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn)?.Value as string;
            if (string.IsNullOrWhiteSpace(sourceColumnName))
            {
                return;
            }

            string? parentName = entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value as string;
            if (string.IsNullOrWhiteSpace(parentName))
            {
                return;
            }

            IEntityType? parentEntityType = model.GetEntityTypes()
                .FirstOrDefault(e =>
                    e.ClrType?.Name == parentName
                    || e.ShortName() == parentName
                    || e.GetTableName() == parentName);
            if (parentEntityType == null)
            {
                return;
            }

            IProperty? property = ColumnNameResolver.ResolveProperty(parentEntityType, sourceColumnName, GetStoreObjectIdentifier(parentEntityType));
            if (property == null)
            {
                return;
            }

            EnsureValidTimeColumn(property, $"continuous aggregate '{DisplayName(entityType)}'", sourceColumnName);
        }

        private static void EnsureValidTimeColumn(IProperty property, string owner, string columnModelName)
            => EnsureValidTimeColumn(property.GetColumnType(), property.FindRelationalTypeMapping()?.StoreType, owner, columnModelName);

        /// <summary>
        /// Validates the resolved store type of a time column, preferring the explicit column type and
        /// falling back to the relational type mapping's store type.
        /// </summary>
        internal static void EnsureValidTimeColumn(string? columnType, string? mappingStoreType, string owner, string columnModelName)
        {
            string? storeType = string.IsNullOrWhiteSpace(columnType) ? mappingStoreType : columnType;

            // If the store type cannot be determined, do not block model building.
            if (string.IsNullOrWhiteSpace(storeType))
            {
                return;
            }

            if (!TimeColumnStoreTypeValidator.IsValid(storeType))
            {
                throw new InvalidOperationException($"The time column '{columnModelName}' on {owner} maps to PostgreSQL type '{storeType}', which is not a valid TimescaleDB time dimension.");
            }
        }

        private static StoreObjectIdentifier? GetStoreObjectIdentifier(IEntityType entityType)
        {
            string? tableName = entityType.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                return StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            }

            string? viewName = entityType.GetViewName();
            if (!string.IsNullOrWhiteSpace(viewName))
            {
                return StoreObjectIdentifier.View(viewName, entityType.GetViewSchema() ?? entityType.GetSchema());
            }

            return null;
        }

        private static string DisplayName(IEntityType entityType) => entityType.ClrType?.Name ?? entityType.Name;
    }
}

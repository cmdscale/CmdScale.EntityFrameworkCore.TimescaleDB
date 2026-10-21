using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
#pragma warning disable EF1001 // Npgsql internal validator/options are the intended base to preserve Npgsql validations.
    /// <summary>
    /// Extends Npgsql's model validator with TimescaleDB-specific model warnings. Derives from
    /// <see cref="NpgsqlModelValidator"/> all Npgsql validations still run via <see cref="Validate"/>'s <c>base</c> call. 
    /// </summary>
    internal class TimescaleModelValidator(
        ModelValidatorDependencies dependencies,
        RelationalModelValidatorDependencies relationalDependencies,
        INpgsqlSingletonOptions npgsqlSingletonOptions)
        : NpgsqlModelValidator(dependencies, relationalDependencies, npgsqlSingletonOptions)
    {
        public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            base.Validate(model, logger);

            foreach (IEntityType entityType in model.GetEntityTypes())
            {
                WarnWhenBucketColumnUnmapped(entityType, logger);
                WarnWhenAggregateEntryIgnored(model, entityType, logger);
            }
        }

        /// <summary>
        /// Warns for each <see cref="ContinuousAggregateAnnotations.AggregateFunctions"/> entry the model extractor
        /// leaves out of the view, using the extractor's own resolution. A warning rather than an exception keeps
        /// models that already migrate working.
        /// </summary>
        private static void WarnWhenAggregateEntryIgnored(IModel model, IEntityType entityType, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            string? materializedViewName = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value as string;
            if (string.IsNullOrWhiteSpace(materializedViewName)
                || !string.IsNullOrWhiteSpace(entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value as string)
                || entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value is not List<string> entries)
            {
                return;
            }

            IEntityType? parent = ParentEntityTypeResolver.Resolve(model, entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value as string);
            StoreObjectIdentifier? parentStoreIdentifier = parent == null ? null : EntityStoreObjectResolver.GetStoreObjectIdentifier(parent);
            StoreObjectIdentifier? aggregateStoreIdentifier = EntityStoreObjectResolver.GetStoreObjectIdentifier(entityType);
            if (parent == null || parentStoreIdentifier == null || aggregateStoreIdentifier == null)
            {
                return;
            }

            foreach (string entry in entries)
            {
                if (ContinuousAggregateModelExtractor.ResolveAggregateFunctionEntry(
                        entry, entityType, parent, parentStoreIdentifier.Value, aggregateStoreIdentifier.Value, out string? reason) == null)
                {
                    logger.ContinuousAggregateEntryIgnored(EntityStoreObjectResolver.DisplayName(entityType), materializedViewName, entry, reason!);
                }
            }
        }

        /// <summary>
        /// Warns when a structured continuous aggregate leaves its bucket column unmapped: no
        /// <see cref="ContinuousAggregateAnnotations.TimeBucketTargetProperty"/> designation and no
        /// property resolving to the fallback bucket column. Such a model is legal — the entity simply
        /// cannot query the bucket — but any LINQ touching the (absent) bucket property fails at runtime
        /// with an opaque "column does not exist" error, so the warning points at the fix. Entities
        /// carrying a raw <see cref="ContinuousAggregateAnnotations.ViewDefinition"/> are exempt, matching
        /// the scaffolded raw-definition exemption in the view-column validation convention.
        /// </summary>
        private static void WarnWhenBucketColumnUnmapped(IEntityType entityType, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            string? materializedViewName = entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value as string;
            if (string.IsNullOrWhiteSpace(materializedViewName))
            {
                return;
            }

            string? viewDefinition = entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value as string;
            if (!string.IsNullOrWhiteSpace(viewDefinition))
            {
                return;
            }

            string? targetPropertyName = entityType.FindAnnotation(ContinuousAggregateAnnotations.TimeBucketTargetProperty)?.Value as string;
            if (!string.IsNullOrWhiteSpace(targetPropertyName))
            {
                return;
            }

            StoreObjectIdentifier? aggregateStoreIdentifier = EntityStoreObjectResolver.GetStoreObjectIdentifier(entityType);
            if (aggregateStoreIdentifier == null)
            {
                return;
            }

            // Without a designation the bucket column is always the fallback; warn only when nothing maps to it.
            string bucketColumn = DefaultValues.ContinuousAggregateTimeBucketColumnName;
            string? mappedColumn = ColumnNameResolver.Resolve(entityType, bucketColumn, aggregateStoreIdentifier.Value);
            if (!string.IsNullOrWhiteSpace(mappedColumn))
            {
                return;
            }

            logger.TimeBucketColumnUnmapped(
                EntityStoreObjectResolver.DisplayName(entityType),
                materializedViewName,
                bucketColumn);
        }
    }
}
#pragma warning restore EF1001

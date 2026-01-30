using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies
{
    /// <summary>
    /// Extracts continuous aggregate refresh policy configuration from the EF Core model.
    /// </summary>
    public class ContinuousAggregatePolicyModelExtractor
    {
        /// <summary>
        /// Gets all continuous aggregate refresh policy configurations from the given model.
        /// </summary>
        /// <param name="relationalModel">The relational model to extract from.</param>
        /// <returns>An enumerable of AddContinuousAggregatePolicyOperation representing each configured policy.</returns>
        public static IEnumerable<AddContinuousAggregatePolicyOperation> GetContinuousAggregatePolicies(IRelationalModel? relationalModel)
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

                // Check if this continuous aggregate has a refresh policy configured
                bool? hasRefreshPolicy = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value as bool?;
                if (hasRefreshPolicy != true)
                {
                    continue;
                }

                // Get the parent (source) entity to determine the schema
                string? parentModelName = entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value as string;
                IEntityType? parentEntityType = null;
                if (!string.IsNullOrWhiteSpace(parentModelName))
                {
                    parentEntityType = relationalModel.Model.GetEntityTypes()
                        .FirstOrDefault(e => e.ClrType?.Name == parentModelName || e.ShortName() == parentModelName);
                }

                // Use parent table's schema for the continuous aggregate (matching ContinuousAggregateModelExtractor behavior)
                string schema = parentEntityType?.GetSchema() ?? entityType.GetSchema() ?? DefaultValues.DefaultSchema;

                // Extract policy configuration from annotations
                string? startOffset = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset)?.Value as string;
                string? endOffset = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset)?.Value as string;
                string? scheduleInterval = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval)?.Value as string;
                DateTime? initialStart = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart)?.Value as DateTime?;
                bool ifNotExists = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists)?.Value as bool? ?? false;
                bool? includeTieredData = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData)?.Value as bool?;
                int bucketsPerBatch = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value as int? ?? 1;
                int maxBatchesPerExecution = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value as int? ?? 0;
                bool refreshNewestFirst = entityType.FindAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value as bool? ?? true;

                yield return new AddContinuousAggregatePolicyOperation
                {
                    Schema = schema,
                    MaterializedViewName = materializedViewName,
                    StartOffset = startOffset,
                    EndOffset = endOffset,
                    ScheduleInterval = scheduleInterval,
                    InitialStart = initialStart,
                    IfNotExists = ifNotExists,
                    IncludeTieredData = includeTieredData,
                    BucketsPerBatch = bucketsPerBatch,
                    MaxBatchesPerExecution = maxBatchesPerExecution,
                    RefreshNewestFirst = refreshNewestFirst
                };
            }
        }
    }
}

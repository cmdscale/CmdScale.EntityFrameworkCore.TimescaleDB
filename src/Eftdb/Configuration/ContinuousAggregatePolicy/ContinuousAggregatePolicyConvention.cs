using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy
{
    /// <summary>
    /// A convention that configures the continuous aggregate refresh policy based on the presence of
    /// the [ContinuousAggregatePolicy] attribute.
    /// </summary>
    /// <remarks>
    /// This convention processes the [ContinuousAggregatePolicy] attribute and converts it to entity type annotations
    /// that will be used during migration generation to create the add_continuous_aggregate_policy() call.
    /// </remarks>
    public class ContinuousAggregatePolicyConvention : IEntityTypeAddedConvention
    {
        /// <summary>
        /// Called when an entity type is added to the model.
        /// </summary>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="context">Additional information available during convention execution.</param>
        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            ContinuousAggregatePolicyAttribute? attribute = entityType.ClrType?.GetCustomAttribute<ContinuousAggregatePolicyAttribute>();

            if (attribute is null)
                return;

            // Mark that this entity has a refresh policy configured
            entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);

            // Apply start offset
            if (!string.IsNullOrWhiteSpace(attribute.StartOffset))
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, attribute.StartOffset);

            // Apply end offset
            if (!string.IsNullOrWhiteSpace(attribute.EndOffset))
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, attribute.EndOffset);

            // Apply schedule interval
            if (!string.IsNullOrWhiteSpace(attribute.ScheduleInterval))
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, attribute.ScheduleInterval);

            // Apply initial start if provided
            if (!string.IsNullOrWhiteSpace(attribute.InitialStart))
            {
                if (DateTime.TryParse(attribute.InitialStart, out DateTime parsedDateTime))
                {
                    entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart, parsedDateTime);
                }
                else
                {
                    throw new InvalidOperationException($"InitialStart '{attribute.InitialStart}' is not a valid DateTime format. Please use a valid DateTime string in ISO 8601 format (e.g., '2025-12-15T03:00:00Z').");
                }
            }

            // Apply if_not_exists flag
            if (attribute.IfNotExists)
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.IfNotExists, attribute.IfNotExists);

            // Apply timezone
            if (!string.IsNullOrWhiteSpace(attribute.Timezone))
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.Timezone, attribute.Timezone);

            // Apply include_tiered_data if explicitly set
            if (attribute.IncludeTieredData.HasValue)
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData, attribute.IncludeTieredData.Value);

            // Apply buckets_per_batch if different from default
            if (attribute.BucketsPerBatch != 1)
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch, attribute.BucketsPerBatch);

            // Apply max_batches_per_execution if different from default
            if (attribute.MaxBatchesPerExecution != 0)
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, attribute.MaxBatchesPerExecution);

            // Apply refresh_newest_first if different from default
            if (attribute.RefreshNewestFirst != true)
                entityTypeBuilder.HasAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, attribute.RefreshNewestFirst);
        }
    }
}

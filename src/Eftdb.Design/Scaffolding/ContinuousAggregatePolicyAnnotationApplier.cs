using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.ContinuousAggregatePolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Applies continuous aggregate policy annotations to scaffolded database views.
    /// </summary>
    public sealed class ContinuousAggregatePolicyAnnotationApplier : IAnnotationApplier
    {
        public void ApplyAnnotations(DatabaseTable table, object featureInfo)
        {
            if (featureInfo is not ContinuousAggregatePolicyInfo info)
            {
                throw new ArgumentException($"Expected {nameof(ContinuousAggregatePolicyInfo)}, got {featureInfo.GetType().Name}", nameof(featureInfo));
            }

            // Mark that this continuous aggregate has a refresh policy
            table[ContinuousAggregatePolicyAnnotations.HasRefreshPolicy] = true;

            // Apply start_offset and end_offset
            if (!string.IsNullOrWhiteSpace(info.StartOffset))
            {
                table[ContinuousAggregatePolicyAnnotations.StartOffset] = info.StartOffset;
            }

            if (!string.IsNullOrWhiteSpace(info.EndOffset))
            {
                table[ContinuousAggregatePolicyAnnotations.EndOffset] = info.EndOffset;
            }

            // Apply schedule_interval
            if (!string.IsNullOrWhiteSpace(info.ScheduleInterval))
            {
                table[ContinuousAggregatePolicyAnnotations.ScheduleInterval] = info.ScheduleInterval;
            }

            // Apply initial_start
            if (info.InitialStart.HasValue)
            {
                table[ContinuousAggregatePolicyAnnotations.InitialStart] = info.InitialStart.Value;
            }

            // Apply include_tiered_data (only if not null - it's an optional parameter)
            if (info.IncludeTieredData.HasValue)
            {
                table[ContinuousAggregatePolicyAnnotations.IncludeTieredData] = info.IncludeTieredData.Value;
            }

            // Apply buckets_per_batch (only if different from default value of 1)
            if (info.BucketsPerBatch.HasValue && info.BucketsPerBatch.Value != 1)
            {
                table[ContinuousAggregatePolicyAnnotations.BucketsPerBatch] = info.BucketsPerBatch.Value;
            }

            // Apply max_batches_per_execution (only if different from default value of 0)
            if (info.MaxBatchesPerExecution.HasValue && info.MaxBatchesPerExecution.Value != 0)
            {
                table[ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution] = info.MaxBatchesPerExecution.Value;
            }

            // Apply refresh_newest_first (only if different from default value of true)
            if (info.RefreshNewestFirst.HasValue && !info.RefreshNewestFirst.Value)
            {
                table[ContinuousAggregatePolicyAnnotations.RefreshNewestFirst] = info.RefreshNewestFirst.Value;
            }
        }
    }
}

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy
{
    /// <summary>
    /// Shared annotation-writing logic for the continuous aggregate refresh policy.
    /// </summary>
    internal static class ContinuousAggregatePolicyBuilderCore
    {
        /// <summary>
        /// Writes the base refresh-policy annotations (HasRefreshPolicy flag and optional
        /// offset / schedule-interval strings).
        /// </summary>
        public static void WriteRefreshPolicy(
            EntityTypeBuilder builder,
            string? startOffset,
            string? endOffset,
            string? scheduleInterval)
        {
            builder.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);

            if (!string.IsNullOrWhiteSpace(startOffset))
                builder.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, startOffset);

            if (!string.IsNullOrWhiteSpace(endOffset))
                builder.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, endOffset);

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                builder.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, scheduleInterval);
        }

        /// <summary>Writes the initial-start annotation.</summary>
        public static void WithInitialStart(EntityTypeBuilder builder, DateTime initialStart)
            => PolicyJobBuilderCore.WithInitialStart(builder, ContinuousAggregatePolicyAnnotations.InitialStart, initialStart);

        /// <summary>Writes the if-not-exists annotation.</summary>
        public static void WithIfNotExists(EntityTypeBuilder builder, bool ifNotExists)
            => PolicyJobBuilderCore.WithIfNotExists(builder, ContinuousAggregatePolicyAnnotations.IfNotExists, ifNotExists);

        /// <summary>Writes the include-tiered-data annotation.</summary>
        public static void WithIncludeTieredData(EntityTypeBuilder builder, bool includeTieredData)
            => PolicyJobBuilderCore.WithIncludeTieredData(builder, ContinuousAggregatePolicyAnnotations.IncludeTieredData, includeTieredData);

        /// <summary>Validates and writes the buckets-per-batch annotation.</summary>
        public static void WithBucketsPerBatch(EntityTypeBuilder builder, int bucketsPerBatch)
            => PolicyJobBuilderCore.WithBucketsPerBatch(builder, ContinuousAggregatePolicyAnnotations.BucketsPerBatch, bucketsPerBatch);

        /// <summary>Validates and writes the max-batches-per-execution annotation.</summary>
        public static void WithMaxBatchesPerExecution(EntityTypeBuilder builder, int maxBatchesPerExecution)
            => PolicyJobBuilderCore.WithMaxBatchesPerExecution(builder, ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, maxBatchesPerExecution);

        /// <summary>Writes the refresh-newest-first annotation.</summary>
        public static void WithRefreshNewestFirst(EntityTypeBuilder builder, bool refreshNewestFirst)
            => PolicyJobBuilderCore.WithRefreshNewestFirst(builder, ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, refreshNewestFirst);
    }
}

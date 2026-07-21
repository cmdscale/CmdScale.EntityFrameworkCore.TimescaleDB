using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration
{
    /// <summary>
    /// Holds the annotation key names for a single TimescaleDB policy's job-level parameters.
    /// Pass an instance to <see cref="PolicyJobBuilderCore"/> so the shared writer knows
    /// which annotation keys to use for each feature.
    /// </summary>
    internal readonly struct PolicyJobAnnotationKeys
    {
        /// <summary>Annotation key for the schedule interval string.</summary>
        public string ScheduleInterval { get; init; }

        /// <summary>Annotation key for the initial start DateTime.</summary>
        public string InitialStart { get; init; }

        /// <summary>Annotation key for the if-not-exists bool.</summary>
        public string IfNotExists { get; init; }

        /// <summary>Annotation key for the include-tiered-data nullable bool.</summary>
        public string IncludeTieredData { get; init; }

        /// <summary>Annotation key for the buckets-per-batch int.</summary>
        public string BucketsPerBatch { get; init; }

        /// <summary>Annotation key for the max-batches-per-execution int.</summary>
        public string MaxBatchesPerExecution { get; init; }

        /// <summary>Annotation key for the refresh-newest-first bool.</summary>
        public string RefreshNewestFirst { get; init; }
    }

    /// <summary>
    /// Shared annotation-writing logic for TimescaleDB background-job policies
    /// (continuous aggregate refresh, reorder, retention). Operates on the non-generic
    /// <see cref="EntityTypeBuilder"/> so every typed and string-context policy builder
    /// can delegate here without duplicating validation or annotation calls.
    /// </summary>
    internal static class PolicyJobBuilderCore
    {
        /// <summary>
        /// Writes the initial-start annotation.
        /// </summary>
        public static void WithInitialStart(EntityTypeBuilder builder, string annotationKey, DateTime initialStart)
            => builder.HasAnnotation(annotationKey, initialStart);

        /// <summary>
        /// Writes the if-not-exists annotation.
        /// </summary>
        public static void WithIfNotExists(EntityTypeBuilder builder, string annotationKey, bool ifNotExists)
            => builder.HasAnnotation(annotationKey, ifNotExists);

        /// <summary>
        /// Writes the include-tiered-data annotation.
        /// </summary>
        public static void WithIncludeTieredData(EntityTypeBuilder builder, string annotationKey, bool includeTieredData)
            => builder.HasAnnotation(annotationKey, includeTieredData);

        /// <summary>
        /// Validates and writes the buckets-per-batch annotation.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="bucketsPerBatch"/> is less than 1.</exception>
        public static void WithBucketsPerBatch(EntityTypeBuilder builder, string annotationKey, int bucketsPerBatch)
        {
            if (bucketsPerBatch < 1)
                throw new ArgumentException("BucketsPerBatch must be at least 1.", nameof(bucketsPerBatch));

            builder.HasAnnotation(annotationKey, bucketsPerBatch);
        }

        /// <summary>
        /// Validates and writes the max-batches-per-execution annotation.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="maxBatchesPerExecution"/> is negative.</exception>
        public static void WithMaxBatchesPerExecution(EntityTypeBuilder builder, string annotationKey, int maxBatchesPerExecution)
        {
            if (maxBatchesPerExecution < 0)
                throw new ArgumentException("MaxBatchesPerExecution must be 0 (unlimited) or greater.", nameof(maxBatchesPerExecution));

            builder.HasAnnotation(annotationKey, maxBatchesPerExecution);
        }

        /// <summary>
        /// Writes the refresh-newest-first annotation.
        /// </summary>
        public static void WithRefreshNewestFirst(EntityTypeBuilder builder, string annotationKey, bool refreshNewestFirst)
            => builder.HasAnnotation(annotationKey, refreshNewestFirst);
    }
}

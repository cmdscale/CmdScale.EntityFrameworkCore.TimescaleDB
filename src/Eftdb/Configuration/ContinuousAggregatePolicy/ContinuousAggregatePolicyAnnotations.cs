namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy
{
    /// <summary>
    /// Contains constants for continuous aggregate policy annotations used by the TimescaleDB provider extension.
    /// </summary>
    public static class ContinuousAggregatePolicyAnnotations
    {
        /// <summary>
        /// Indicates whether the continuous aggregate has a refresh policy configured.
        /// </summary>
        public const string HasRefreshPolicy = "TimescaleDB:ContinuousAggregatePolicy:HasRefreshPolicy";

        /// <summary>
        /// Window start as interval relative to execution time. NULL equals earliest data.
        /// Stored as string (e.g., "1 month", "7 days") or can be an integer for integer-based time columns.
        /// </summary>
        public const string StartOffset = "TimescaleDB:ContinuousAggregatePolicy:StartOffset";

        /// <summary>
        /// Window end as interval relative to execution time. NULL equals latest data.
        /// Stored as string (e.g., "1 hour", "1 day") or can be an integer for integer-based time columns.
        /// </summary>
        public const string EndOffset = "TimescaleDB:ContinuousAggregatePolicy:EndOffset";

        /// <summary>
        /// Interval between refresh executions in wall-clock time.
        /// Stored as string (e.g., "1 hour", "24 hours"). Defaults to "24 hours".
        /// </summary>
        public const string ScheduleInterval = "TimescaleDB:ContinuousAggregatePolicy:ScheduleInterval";

        /// <summary>
        /// Policy first run time. Stored as DateTime. Affects next_start calculation.
        /// </summary>
        public const string InitialStart = "TimescaleDB:ContinuousAggregatePolicy:InitialStart";

        /// <summary>
        /// Issue notice instead of error if job exists. Stored as bool. Defaults to false.
        /// </summary>
        public const string IfNotExists = "TimescaleDB:ContinuousAggregatePolicy:IfNotExists";

        /// <summary>
        /// Override tiered read settings. Stored as nullable bool.
        /// </summary>
        public const string IncludeTieredData = "TimescaleDB:ContinuousAggregatePolicy:IncludeTieredData";

        /// <summary>
        /// Buckets processed per batch transaction. Stored as int. Defaults to 1.
        /// </summary>
        public const string BucketsPerBatch = "TimescaleDB:ContinuousAggregatePolicy:BucketsPerBatch";

        /// <summary>
        /// Maximum batches per run. 0 = unlimited. Stored as int. Defaults to 0.
        /// </summary>
        public const string MaxBatchesPerExecution = "TimescaleDB:ContinuousAggregatePolicy:MaxBatchesPerExecution";

        /// <summary>
        /// Direction of incremental refresh. Stored as bool. Defaults to true (newest first).
        /// </summary>
        public const string RefreshNewestFirst = "TimescaleDB:ContinuousAggregatePolicy:RefreshNewestFirst";
    }
}

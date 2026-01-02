namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy
{
    /// <summary>
    /// Configures a continuous aggregate refresh policy for a TimescaleDB continuous aggregate entity.
    /// This attribute adds an automatic refresh policy that runs on a schedule to keep the materialized view up to date.
    /// </summary>
    /// <remarks>
    /// The policy executes TimescaleDB's add_continuous_aggregate_policy() function during migrations.
    /// All parameters map directly to the function's parameters.
    /// </remarks>
    /// <example>
    /// <code>
    /// [ContinuousAggregate("hourly_metrics", "Metrics")]
    /// [ContinuousAggregatePolicy(
    ///     StartOffset = "1 month",
    ///     EndOffset = "1 hour",
    ///     ScheduleInterval = "1 hour"
    /// )]
    /// public class HourlyMetric
    /// {
    ///     // Properties...
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ContinuousAggregatePolicyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the window start as interval relative to execution time.
        /// Can be an interval string (e.g., "1 month", "7 days") or integer string for integer-based time columns.
        /// NULL or empty string equals earliest data.
        /// </summary>
        /// <example>
        /// "1 month", "7 days", "100000" (for integer-based timestamps)
        /// </example>
        public string? StartOffset { get; set; }

        /// <summary>
        /// Gets or sets the window end as interval relative to execution time.
        /// Can be an interval string (e.g., "1 hour", "1 day") or integer string for integer-based time columns.
        /// NULL or empty string equals latest data.
        /// </summary>
        /// <example>
        /// "1 hour", "1 day", "1000" (for integer-based timestamps)
        /// </example>
        public string? EndOffset { get; set; }

        /// <summary>
        /// Gets or sets the interval between refresh executions in wall-clock time.
        /// Defaults to "24 hours" if not specified.
        /// </summary>
        /// <example>
        /// "1 hour", "30 minutes", "24 hours"
        /// </example>
        public string? ScheduleInterval { get; set; }

        /// <summary>
        /// Gets or sets the first time the policy job is scheduled to run.
        /// Can be specified as a UTC date-time string in ISO 8601 format.
        /// If not set, the first run is scheduled based on the schedule_interval.
        /// </summary>
        /// <example>
        /// "2025-12-15T03:00:00Z"
        /// </example>
        public string? InitialStart { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to issue a notice instead of an error if the job already exists.
        /// Defaults to false.
        /// </summary>
        public bool IfNotExists { get; set; } = false;

        /// <summary>
        /// Gets or sets the timezone to mitigate daylight savings alignment shifts.
        /// </summary>
        /// <example>
        /// "UTC", "America/New_York", "Europe/London"
        /// </example>
        public string? Timezone { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to override tiered read settings.
        /// NULL means use default behavior.
        /// </summary>
        public bool? IncludeTieredData { get; set; }

        /// <summary>
        /// Gets or sets the number of buckets processed per batch transaction.
        /// Defaults to 1.
        /// </summary>
        public int BucketsPerBatch { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of batches per execution.
        /// 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaxBatchesPerExecution { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating the direction of incremental refresh.
        /// True means newest data first, false means oldest first.
        /// Defaults to true.
        /// </summary>
        public bool RefreshNewestFirst { get; set; } = true;
    }
}

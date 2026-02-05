using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    /// <summary>
    /// Represents a migration operation to add a continuous aggregate refresh policy in TimescaleDB.
    /// This operation generates a call to TimescaleDB's add_continuous_aggregate_policy() function.
    /// </summary>
    /// <remarks>
    /// The continuous aggregate policy automatically refreshes the materialized view on a schedule.
    /// </remarks>
    public class AddContinuousAggregatePolicyOperation : MigrationOperation
    {
        /// <summary>
        /// Gets or sets the name of the materialized view (continuous aggregate).
        /// </summary>
        public string MaterializedViewName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema name of the materialized view.
        /// </summary>
        public string Schema { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the window start as interval relative to execution time.
        /// Can be an interval string (e.g., "1 month", "7 days") or integer string for integer-based time columns.
        /// NULL equals earliest data.
        /// </summary>
        public string? StartOffset { get; init; }

        /// <summary>
        /// Gets or sets the window end as interval relative to execution time.
        /// Can be an interval string (e.g., "1 hour", "1 day") or integer string for integer-based time columns.
        /// NULL equals latest data.
        /// </summary>
        public string? EndOffset { get; init; }

        /// <summary>
        /// Gets or sets the interval between refresh executions in wall-clock time.
        /// Defaults to "24 hours" if not specified.
        /// </summary>
        /// <example>
        /// "1 hour", "30 minutes", "24 hours"
        /// </example>
        public string? ScheduleInterval { get; init; }

        /// <summary>
        /// Gets or sets the first time the policy job is scheduled to run.
        /// If not set, the first run is scheduled based on the schedule_interval.
        /// </summary>
        public DateTime? InitialStart { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to issue a notice instead of an error if the job already exists.
        /// Defaults to false.
        /// </summary>
        public bool IfNotExists { get; init; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to override tiered read settings.
        /// NULL means use default behavior.
        /// </summary>
        public bool? IncludeTieredData { get; init; }

        /// <summary>
        /// Gets or sets the number of buckets processed per batch transaction.
        /// Defaults to 1.
        /// </summary>
        public int BucketsPerBatch { get; init; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of batches per execution.
        /// 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaxBatchesPerExecution { get; init; } = 0;

        /// <summary>
        /// Gets or sets a value indicating the direction of incremental refresh.
        /// True means newest data first, false means oldest first.
        /// Defaults to true.
        /// </summary>
        public bool RefreshNewestFirst { get; init; } = true;
    }
}

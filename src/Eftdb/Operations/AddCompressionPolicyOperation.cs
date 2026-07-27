using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    /// <summary>
    /// Represents a migration operation that schedules automatic compression of chunks on a hypertable
    /// or continuous aggregate by calling <c>add_compression_policy()</c>.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="After"/> or <see cref="CreatedBefore"/> must be set.
    /// The target table must already have compression enabled (<c>timescaledb.compress</c>).
    /// </remarks>
    public class AddCompressionPolicyOperation : MigrationOperation
    {
        /// <summary>Gets or sets the table (or materialized view) name.</summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>Gets or sets the schema of the table or materialized view.</summary>
        public string Schema { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the interval after which chunks are compressed.
        /// Mutually exclusive with <see cref="CreatedBefore"/>; exactly one must be set.
        /// </summary>
        /// <example>"7 days"</example>
        public string? After { get; set; }

        /// <summary>
        /// Gets or sets the interval relative to chunk creation time; chunks created before this interval are compressed.
        /// Mutually exclusive with <see cref="After"/>; exactly one must be set.
        /// </summary>
        /// <example>"30 days"</example>
        public string? CreatedBefore { get; set; }

        /// <summary>
        /// Gets or sets the interval between policy job executions.
        /// Defaults to 12 hours when <c>chunk_time_interval</c> is at least 1 day, otherwise half the chunk interval.
        /// </summary>
        /// <example>"12 hours"</example>
        public string? ScheduleInterval { get; set; }

        /// <summary>
        /// Gets or sets the first time the policy job is scheduled to run.
        /// When null the scheduler derives the initial run from the schedule interval.
        /// </summary>
        public DateTime? InitialStart { get; set; }

        /// <summary>
        /// Gets or sets the PostgreSQL time zone used when computing the initial start time.
        /// </summary>
        /// <example>"Europe/Berlin"</example>
        public string? Timezone { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation should succeed silently when the policy
        /// already exists, instead of raising an error.
        /// </summary>
        public bool? IfNotExists { get; set; }
    }
}

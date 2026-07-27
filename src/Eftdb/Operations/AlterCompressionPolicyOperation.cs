using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    /// <summary>
    /// Represents a migration operation that modifies an existing compression policy on a hypertable
    /// or continuous aggregate by calling <c>remove_compression_policy()</c> followed by
    /// <c>add_compression_policy()</c>.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="After"/> or <see cref="CreatedBefore"/> must be set.
    /// The <c>Old*</c> properties capture the previous state so the down migration can restore it.
    /// </remarks>
    public class AlterCompressionPolicyOperation : MigrationOperation
    {
        /// <summary>Gets or sets the table (or materialized view) name.</summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>Gets or sets the schema of the table or materialized view.</summary>
        public string Schema { get; set; } = string.Empty;

        // ── New (target) state ──────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the new interval after which chunks are compressed.
        /// Mutually exclusive with <see cref="CreatedBefore"/>; exactly one must be set.
        /// </summary>
        public string? After { get; set; }

        /// <summary>
        /// Gets or sets the new interval relative to chunk creation; chunks created before this interval are compressed.
        /// Mutually exclusive with <see cref="After"/>; exactly one must be set.
        /// </summary>
        public string? CreatedBefore { get; set; }

        /// <summary>Gets or sets the new interval between policy job executions.</summary>
        public string? ScheduleInterval { get; set; }

        /// <summary>Gets or sets the new first time the policy job is scheduled to run.</summary>
        public DateTime? InitialStart { get; set; }

        /// <summary>Gets or sets the new PostgreSQL time zone used when computing the initial start time.</summary>
        public string? Timezone { get; set; }

        /// <summary>Gets or sets the new if-not-exists flag.</summary>
        public bool? IfNotExists { get; set; }

        // ── Old (source) state — used to generate the Down migration ───────────

        /// <summary>Gets or sets the previous value of <see cref="After"/>.</summary>
        public string? OldAfter { get; set; }

        /// <summary>Gets or sets the previous value of <see cref="CreatedBefore"/>.</summary>
        public string? OldCreatedBefore { get; set; }

        /// <summary>Gets or sets the previous schedule interval.</summary>
        public string? OldScheduleInterval { get; set; }

        /// <summary>Gets or sets the previous initial start time.</summary>
        public DateTime? OldInitialStart { get; set; }

        /// <summary>Gets or sets the previous time zone.</summary>
        public string? OldTimezone { get; set; }

        /// <summary>Gets or sets the previous if-not-exists flag.</summary>
        public bool? OldIfNotExists { get; set; }
    }
}

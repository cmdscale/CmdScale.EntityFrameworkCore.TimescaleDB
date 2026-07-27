namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy
{
    /// <summary>
    /// Schedules automatic compression of chunks on a hypertable or continuous aggregate.
    /// Generates a call to <c>add_compression_policy()</c> during migration.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="After"/> or <see cref="CreatedBefore"/> must be set.
    /// The entity's table must already have compression enabled via <c>timescaledb.compress</c>
    /// (see <c>[Hypertable(EnableCompression = true)]</c> or <c>.IsHypertable().WithCompression()</c>).
    /// All interval values are strings (e.g., "7 days"). No <see cref="TimeSpan"/> overloads exist
    /// because the underlying unit depends on the time-column type.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Hypertable("Time", EnableCompression = true)]
    /// [CompressionPolicy(After = "7 days")]
    /// public class Reading { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CompressionPolicyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the interval after which chunks are compressed.
        /// Mutually exclusive with <see cref="CreatedBefore"/>; exactly one must be set.
        /// </summary>
        /// <example>"7 days"</example>
        public string? After { get; set; }

        /// <summary>
        /// Gets or sets the interval relative to chunk creation time.
        /// Chunks created more than this interval ago are compressed.
        /// Mutually exclusive with <see cref="After"/>; exactly one must be set.
        /// </summary>
        /// <example>"30 days"</example>
        public string? CreatedBefore { get; set; }

        /// <summary>
        /// Gets or sets the interval between policy job executions.
        /// When not set TimescaleDB defaults to 12 hours (or half the chunk interval for sub-day intervals).
        /// </summary>
        /// <example>"12 hours"</example>
        public string? ScheduleInterval { get; set; }

        /// <summary>
        /// Gets or sets the first time the policy job is scheduled to run,
        /// as a UTC date-time string in ISO 8601 format.
        /// When not set the scheduler derives the initial run from the schedule interval.
        /// </summary>
        /// <example>"2025-10-01T03:00:00Z"</example>
        public string? InitialStart { get; set; }

        /// <summary>
        /// Gets or sets the PostgreSQL time zone used when computing the initial start time.
        /// </summary>
        /// <example>"Europe/Berlin"</example>
        public string? Timezone { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation should succeed silently when the policy
        /// already exists, instead of raising an error.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool IfNotExists { get; set; }
    }
}

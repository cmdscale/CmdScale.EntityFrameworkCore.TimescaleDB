namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RetentionPolicyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the interval after which chunks are dropped.
        /// Mutually exclusive with <see cref="DropCreatedBefore"/>; exactly one must be specified.
        /// </summary>
        /// <example>
        /// "7 days"
        /// </example>
        public string? DropAfter { get; set; }

        /// <summary>
        /// Gets or sets the interval before which chunks created are dropped.
        /// Mutually exclusive with <see cref="DropAfter"/>; exactly one must be specified.
        /// Only supported for hypertables, not continuous aggregates.
        /// </summary>
        /// <example>
        /// "30 days"
        /// </example>
        /// <remarks>
        /// When you use DropCreatedBefore, instead of DropAfter, arguments related to the alter_job function like MaxRuntime, MaxRetries, 
        /// or RetryPeriod are not supported.
        /// The reason for this is a bug in TimescaleDB itself. See <a href="https://github.com/timescale/timescaledb/issues/9446">this issue</a> for further information.
        /// </remarks>
        public string? DropCreatedBefore { get; set; }

        /// <summary>
        /// Gets or sets the first time the policy job is scheduled to run.
        /// Can be specified as a UTC date-time string in ISO 8601 format.
        /// If not set, the first run is scheduled based on the <c>schedule_interval</c>.
        /// </summary>
        /// <example>
        /// "2025-10-01T03:00:00Z"
        /// </example>
        public string? InitialStart { get; set; }

        /// <summary>
        /// Gets or sets the interval at which the retention policy job runs.
        /// If not set, it defaults to the TimescaleDB server default.
        /// </summary>
        /// <example>
        /// "1 day"
        /// </example>
        public string? ScheduleInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum amount of time the job is allowed to run before being stopped.
        /// If not set, there is no time limit.
        /// </summary>
        /// <example>
        /// "1 hour"
        /// </example>
        public string? MaxRuntime { get; set; }

        /// <summary>
        /// Gets or sets the number of times the job is retried if it fails.
        /// If not set, it defaults to -1 (retry indefinitely).
        /// </summary>
        public int MaxRetries { get; set; } = -1;

        /// <summary>
        /// Gets or sets the amount of time the scheduler waits between retries of a failed job.
        /// </summary>
        /// <example>
        /// "30 minutes"
        /// </example>
        public string? RetryPeriod { get; set; }

        /// <summary>
        /// Configures a retention policy using <c>drop_after</c>.
        /// </summary>
        /// <param name="dropAfter">The interval after which chunks are dropped (e.g., "7 days").</param>
        public RetentionPolicyAttribute(string dropAfter)
        {
            if (string.IsNullOrWhiteSpace(dropAfter))
            {
                throw new ArgumentException("DropAfter must be provided.", nameof(dropAfter));
            }

            DropAfter = dropAfter;
        }

        /// <summary>
        /// Configures a retention policy. Exactly one of <paramref name="dropAfter"/> or <paramref name="dropCreatedBefore"/> must be non-null.
        /// </summary>
        public RetentionPolicyAttribute(string? dropAfter = null, string? dropCreatedBefore = null)
        {
            bool hasDropAfter = !string.IsNullOrWhiteSpace(dropAfter);
            bool hasDropCreatedBefore = !string.IsNullOrWhiteSpace(dropCreatedBefore);

            if (hasDropAfter && hasDropCreatedBefore)
            {
                throw new InvalidOperationException("RetentionPolicy: 'DropAfter' and 'DropCreatedBefore' are mutually exclusive. Specify exactly one.");
            }

            if (!hasDropAfter && !hasDropCreatedBefore)
            {
                throw new InvalidOperationException("RetentionPolicy: Exactly one of 'DropAfter' or 'DropCreatedBefore' must be specified.");
            }

            DropAfter = dropAfter;
            DropCreatedBefore = dropCreatedBefore;
        }
    }
}

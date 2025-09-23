namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ReorderPolicyAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the existing index that the reorder policy will use to sort the data.
        /// </summary>
        /// <example>
        /// "IX_Readings_DeviceId_Time"
        /// </example>
        public string IndexName { get; set; } = string.Empty;

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
        /// Gets or sets the interval at which the reorder policy job runs.
        /// If not set, it defaults to '1 day'.
        /// </summary>
        /// <example>
        /// "2 days"
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
        /// If not set, it defaults to '00:05:00'.
        /// </summary>
        /// <example>
        /// "30 minutes"
        /// </example>
        public string? RetryPeriod { get; set; }

        public ReorderPolicyAttribute(string indexName)
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("IndexName must be provided.", nameof(indexName));
            }

            IndexName = indexName;
        }
    }
}

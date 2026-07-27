namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
    /// <summary>
    /// Default values for TimescaleDB properties
    /// </summary>
    public static class DefaultValues
    {
        public const string DefaultSchema = "public";
        public const string ChunkTimeInterval = "7 days";
        public const long ChunkTimeIntervalLong = 604_800_000_000L;
        public const string ReorderPolicyScheduleInterval = "1 day";
        public const int ReorderPolicyMaxRetries = -1;
        public const string ReorderPolicyMaxRuntime = "00:00:00";

        public const string RetentionPolicyScheduleInterval = "1 day";
        public const int RetentionPolicyMaxRetries = -1;
        public const string RetentionPolicyMaxRuntime = "00:00:00";

        /// <summary>
        /// The default <c>schedule_interval</c> for <c>add_compression_policy()</c> when the
        /// <c>chunk_time_interval</c> is at least 1 day.
        /// </summary>
        public const string CompressionPolicyScheduleInterval = "12 hours";

        /// <summary>
        /// The chunk time interval threshold below which the default <c>schedule_interval</c> for
        /// <c>add_compression_policy()</c> becomes <c>chunk_time_interval / 2</c> instead of
        /// <see cref="CompressionPolicyScheduleInterval"/>.
        /// </summary>
        public const string CompressionPolicyScheduleIntervalThreshold = "1 day";
    }
}

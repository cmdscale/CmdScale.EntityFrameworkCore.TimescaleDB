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
        public const string ReorderPolicyRetryPeriod = "00:05:00";
    }
}

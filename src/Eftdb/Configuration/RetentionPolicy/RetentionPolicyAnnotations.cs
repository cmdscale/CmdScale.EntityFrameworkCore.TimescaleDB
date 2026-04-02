namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB retention policy feature.
    /// </summary>
    public static class RetentionPolicyAnnotations
    {
        public const string HasRetentionPolicy = "TimescaleDB:HasRetentionPolicy";
        public const string DropAfter = "TimescaleDB:RetentionPolicy:DropAfter";
        public const string DropCreatedBefore = "TimescaleDB:RetentionPolicy:DropCreatedBefore";
        public const string InitialStart = "TimescaleDB:RetentionPolicy:InitialStart";

        public const string ScheduleInterval = "TimescaleDB:RetentionPolicy:ScheduleInterval";
        public const string MaxRuntime = "TimescaleDB:RetentionPolicy:MaxRuntime";
        public const string MaxRetries = "TimescaleDB:RetentionPolicy:MaxRetries";
        public const string RetryPeriod = "TimescaleDB:RetentionPolicy:RetryPeriod";
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB provider extension.
    /// </summary>
    public static class ReorderPolicyAnnotations
    {
        public const string HasReorderPolicy = "TimescaleDB:HasReorderPolicy";
        public const string IndexName = "TimescaleDB:ReorderPolicy:IndexName";
        public const string InitialStart = "TimescaleDB:ReorderPolicy:InitialStart";

        public const string ScheduleInterval = "TimescaleDB:ReorderPolicy:ScheduleInterval";
        public const string MaxRuntime = "TimescaleDB:ReorderPolicy:MaxRuntime";
        public const string MaxRetries = "TimescaleDB:ReorderPolicy:MaxRetries";
        public const string RetryPeriod = "TimescaleDB:ReorderPolicy:RetryPeriod";
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB compression policy feature.
    /// </summary>
    public static class CompressionPolicyAnnotations
    {
        public const string HasCompressionPolicy = "TimescaleDB:HasCompressionPolicy";
        public const string After = "TimescaleDB:CompressionPolicy:After";
        public const string CreatedBefore = "TimescaleDB:CompressionPolicy:CreatedBefore";
        public const string InitialStart = "TimescaleDB:CompressionPolicy:InitialStart";
        public const string ScheduleInterval = "TimescaleDB:CompressionPolicy:ScheduleInterval";
        public const string Timezone = "TimescaleDB:CompressionPolicy:Timezone";
        public const string IfNotExists = "TimescaleDB:CompressionPolicy:IfNotExists";
    }
}

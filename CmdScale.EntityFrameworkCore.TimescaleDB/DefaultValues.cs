namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
    /// <summary>
    /// Default values for TimescaleDB properties
    /// </summary>
    public static class DefaultValues
    {
        public const string ChunkTimeInterval = "7 days";
        public const long ChunkTimeIntervalLong = 604_800_000_000L;
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils
{
    /// <summary>
    /// Centralizes the TimescaleDB container images used by Testcontainers-backed tests.
    /// </summary>
    internal static class TimescaleImages
    {
        public const string Community = "timescale/timescaledb:latest-pg17";
        public const string Apache = "timescale/timescaledb:latest-pg17-oss";
    }
}

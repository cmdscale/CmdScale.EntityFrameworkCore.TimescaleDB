namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils
{
    /// <summary>
    /// Centralizes the TimescaleDB container images used by Testcontainers-backed tests.
    /// </summary>
    internal static class TimescaleImages
    {
        public const string Community = "timescale/timescaledb:latest-pg17";
        public const string Apache = "timescale/timescaledb:latest-pg17-oss";

        /// <summary>
        /// Ships the timescaledb_toolkit extension, which the plain images lack. Tests using it carry
        /// <see cref="ToolkitTrait"/> so they can be filtered.
        /// </summary>
        public const string Toolkit = "timescale/timescaledb-ha:pg17";

        public const string ToolkitTrait = "Toolkit";
    }
}

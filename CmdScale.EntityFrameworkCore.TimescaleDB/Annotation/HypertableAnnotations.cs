namespace CmdScale.EntityFrameworkCore.TimescaleDB.Annotation
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB provider extension.
    /// </summary>
    public static class HypertableAnnotations
    {
        public const string IsHypertable = "TimescaleDB:IsHypertable";
        public const string HypertableTimeColumn = "TimescaleDB:TimeColumnName";
        public const string EnableCompression = "TimescaleDB:EnableCompression";
        public const string ChunkTimeInterval ="TimescaleDB:ChunkTimeInterval";
        public const string ChunkSkipColumns = "TimescaleDB:ChunkSkipColumns";
    }
}

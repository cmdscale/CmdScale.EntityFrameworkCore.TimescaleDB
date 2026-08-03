namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB provider extension.
    /// </summary>
    public static class HypertableAnnotations
    {
        public const string IsHypertable = "TimescaleDB:IsHypertable";
        public const string HypertableTimeColumn = "TimescaleDB:TimeColumnName";
        public const string EnableCompression = "TimescaleDB:EnableCompression";
        public const string CompressionSegmentBy = "TimescaleDB:CompressionSegmentBy";
        public const string CompressionOrderBy = "TimescaleDB:CompressionOrderBy";
        public const string MigrateData = "TimescaleDB:MigrateData";
        public const string ChunkTimeInterval = "TimescaleDB:ChunkTimeInterval";
        public const string ChunkSkipColumns = "TimescaleDB:ChunkSkipColumns";
        public const string AdditionalDimensions = "TimescaleDB:AdditionalDimensions";

        /// <summary>
        /// Sparse index configuration for the columnstore.
        /// </summary>
        public const string CompressionSparseIndex = "TimescaleDB:CompressionSparseIndex";

        /// <summary>
        /// The minimum time interval to be used for merging chunks during compression.
        /// Must be a multiple of the hypertable's <c>chunk_time_interval</c>.
        /// </summary>
        public const string CompressChunkTimeInterval = "TimescaleDB:CompressChunkTimeInterval";
    }
}

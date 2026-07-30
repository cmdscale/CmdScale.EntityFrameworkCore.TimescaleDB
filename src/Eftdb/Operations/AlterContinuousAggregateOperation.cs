using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AlterContinuousAggregateOperation : MigrationOperation
    {
        public string Schema { get; set; } = string.Empty;
        public string MaterializedViewName { get; set; } = string.Empty;

        public string? ChunkInterval { get; set; }
        public string? OldChunkInterval { get; set; }

        public bool CreateGroupIndexes { get; set; }
        public bool OldCreateGroupIndexes { get; set; }

        public bool MaterializedOnly { get; set; }
        public bool OldMaterializedOnly { get; set; }

        /// <summary>
        /// Whether columnstore (compression) should be enabled after the alter.
        /// Corresponds to <c>ALTER MATERIALIZED VIEW ... SET (timescaledb.compress = true/false)</c>.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>Previous value of <see cref="EnableCompression"/>.</summary>
        public bool OldEnableCompression { get; set; }

        /// <summary>
        /// The columns to segment by for compression after the alter. Comma-separated database column names.
        /// Corresponds to <c>timescaledb.compress_segmentby</c>.
        /// </summary>
        public IReadOnlyList<string>? CompressionSegmentBy { get; set; }

        /// <summary>Previous value of <see cref="CompressionSegmentBy"/>.</summary>
        public IReadOnlyList<string>? OldCompressionSegmentBy { get; set; }

        /// <summary>
        /// The columns to order by within each compressed segment after the alter.
        /// Comma-separated SQL expressions. Corresponds to <c>timescaledb.compress_orderby</c>.
        /// </summary>
        public IReadOnlyList<string>? CompressionOrderBy { get; set; }

        /// <summary>Previous value of <see cref="CompressionOrderBy"/>.</summary>
        public IReadOnlyList<string>? OldCompressionOrderBy { get; set; }
    }
}

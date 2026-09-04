using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class CreateContinuousAggregateOperation : MigrationOperation
    {
        public string Schema { get; set; } = string.Empty;
        public string MaterializedViewName { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public string? ChunkInterval { get; set; }

        public bool WithNoData { get; set; }
        public bool CreateGroupIndexes { get; set; }
        public bool MaterializedOnly { get; set; }

        public string TimeBucketWidth { get; set; } = string.Empty;
        public string TimeBucketSourceColumn { get; set; } = string.Empty;
        public bool TimeBucketGroupBy { get; set; }
        public string TimeBucketColumnName { get; set; } = DefaultValues.ContinuousAggregateTimeBucketColumnName;

        public IReadOnlyList<string> AggregateFunctions { get; set; } = [];
        public IReadOnlyList<string> GroupByColumns { get; set; } = [];
        public string? WhereClause { get; set; }

        /// <summary>
        /// Raw SQL body for the materialized view. When non-null the generator uses this
        /// verbatim (CREATE MATERIALIZED VIEW ... AS {ViewDefinition}) and ignores the
        /// structured time-bucket/aggregate/group-by/where fields. Populated by the
        /// design-time scaffolder, which cannot reverse-engineer those structured fields
        /// from the TimescaleDB catalog.
        /// </summary>
        public string? ViewDefinition { get; set; }

        /// <summary>
        /// Whether to enable columnstore (compression) on the continuous aggregate immediately after creation.
        /// Corresponds to <c>ALTER MATERIALIZED VIEW ... SET (timescaledb.compress = true)</c>.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// The columns to segment by for compression. Comma-separated database column names.
        /// Corresponds to <c>timescaledb.compress_segmentby</c>.
        /// </summary>
        public IReadOnlyList<string>? CompressionSegmentBy { get; set; }

        /// <summary>
        /// The columns to order by within each compressed segment. Comma-separated SQL expressions
        /// (e.g., <c>"time DESC"</c>). Corresponds to <c>timescaledb.compress_orderby</c>.
        /// </summary>
        public IReadOnlyList<string>? CompressionOrderBy { get; set; }
    }
}

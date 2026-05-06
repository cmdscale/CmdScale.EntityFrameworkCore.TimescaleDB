namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB provider extension.
    /// </summary>
    public static class ContinuousAggregateAnnotations
    {
        public const string MaterializedViewName = "TimescaleDB:MaterializedViewName";
        public const string ParentName = "TimescaleDB:ParentName";
        public const string ChunkInterval = "TimescaleDB:ChunkInterval";

        public const string WithNoData = "TimescaleDB:WithNoData";
        public const string CreateGroupIndexes = "TimescaleDB:CreateGroupIndexes";
        public const string MaterializedOnly = "TimescaleDB:MaterializedOnly";

        public const string TimeBucketWidth = "TimescaleDB:TimeBucket:BucketWidth";
        public const string TimeBucketSourceColumn = "TimescaleDB:TimeBucket:SourceColumn";
        public const string TimeBucketGroupBy = "TimescaleDB:TimeBucket:GroupBy";

        public const string AggregateFunctions = "TimescaleDB:AggregateFunctions";
        public const string WhereClause = "TimescaleDB:WhereClause";
        public const string GroupByColumns = "TimescaleDB:GroupByColumns";

        /// <summary>
        /// Raw SQL view body captured by the design-time scaffolder. When present, the
        /// runtime extractor and generator use it verbatim as the SELECT clause of
        /// CREATE MATERIALIZED VIEW instead of synthesising from structured fields. The
        /// scaffolder cannot reliably reverse-engineer time_bucket / aggregate / group_by
        /// structure from the catalog, so this raw form is the round-trip fallback.
        /// </summary>
        public const string ViewDefinition = "TimescaleDB:ViewDefinition";
    }
}

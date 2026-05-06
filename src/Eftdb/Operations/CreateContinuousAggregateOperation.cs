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

        public List<string> AggregateFunctions { get; set; } = [];
        public List<string> GroupByColumns { get; set; } = [];
        public string? WhereClause { get; set; }

        /// <summary>
        /// Raw SQL body for the materialized view. When non-null the generator uses this
        /// verbatim (CREATE MATERIALIZED VIEW ... AS {ViewDefinition}) and ignores the
        /// structured time-bucket/aggregate/group-by/where fields. Populated by the
        /// design-time scaffolder, which cannot reverse-engineer those structured fields
        /// from the TimescaleDB catalog.
        /// </summary>
        public string? ViewDefinition { get; set; }
    }
}

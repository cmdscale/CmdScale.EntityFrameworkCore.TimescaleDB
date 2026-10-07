using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class DropContinuousAggregateOperation : MigrationOperation
    {
        /// <summary>
        /// Dropping a continuous aggregate destroys its materialized data. When the source
        /// hypertable's retention policy has already dropped the covered raw data, that history is
        /// unrecoverable, so scaffolding surfaces EF's data-loss warning for every drop.
        /// </summary>
        public DropContinuousAggregateOperation()
        {
            IsDestructiveChange = true;
        }

        public string Schema { get; set; } = string.Empty;
        public string MaterializedViewName { get; set; } = string.Empty;
    }
}

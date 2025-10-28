using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class DropContinuousAggregateOperation : MigrationOperation
    {
        public string MaterializedViewName { get; set; } = string.Empty;
    }
}

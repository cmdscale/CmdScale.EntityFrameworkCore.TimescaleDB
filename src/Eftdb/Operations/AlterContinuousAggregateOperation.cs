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
    }
}

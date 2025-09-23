using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AlterReorderPolicyOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;

        public string IndexName { get; set; } = string.Empty;
        public DateTime? InitialStart { get; set; } = null;

        public string OldIndexName { get; set; } = string.Empty;
        public DateTime? OldInitialStart { get; set; } = null;
    }
}

using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class DropReorderPolicyOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
    }
}

using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class CreateHypertableOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string TimeColumnName { get; set; } = string.Empty;
    }
}

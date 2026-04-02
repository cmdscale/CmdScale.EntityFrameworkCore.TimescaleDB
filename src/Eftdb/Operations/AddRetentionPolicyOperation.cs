using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AddRetentionPolicyOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string? DropAfter { get; set; }
        public string? DropCreatedBefore { get; set; }
        public DateTime? InitialStart { get; set; }
        public string? ScheduleInterval { get; set; }
        public string? MaxRuntime { get; set; }
        public int? MaxRetries { get; set; }
        public string? RetryPeriod { get; set; }
    }
}

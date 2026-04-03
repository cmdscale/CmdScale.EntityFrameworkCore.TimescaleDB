using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AlterRetentionPolicyOperation : MigrationOperation
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

        public string? OldDropAfter { get; set; }
        public string? OldDropCreatedBefore { get; set; }
        public DateTime? OldInitialStart { get; set; }
        public string? OldScheduleInterval { get; set; }
        public string? OldMaxRuntime { get; set; }
        public int? OldMaxRetries { get; set; }
        public string? OldRetryPeriod { get; set; }
    }
}

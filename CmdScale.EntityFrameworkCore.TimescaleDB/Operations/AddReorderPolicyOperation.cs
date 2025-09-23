using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Operations
{
    public class AddReorderPolicyOperation : MigrationOperation
    {
        public string TableName { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
        public DateTime? InitialStart { get; set; }
        public string? ScheduleInterval { get; set; }
        public string? MaxRuntime { get; set; }
        public int? MaxRetries { get; set; }
        public string? RetryPeriod { get; set; }
    }
}

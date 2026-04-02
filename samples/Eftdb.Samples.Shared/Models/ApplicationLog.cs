using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    [Hypertable(nameof(Time), ChunkTimeInterval = "1 day")]
    [PrimaryKey(nameof(Id), nameof(Time))]
    [RetentionPolicy("30 days",
        InitialStart = "2025-10-01T03:00:00Z",
        ScheduleInterval = "1 day",
        MaxRetries = 3,
        RetryPeriod = "5 minutes")]
    public class ApplicationLog
    {
        public Guid Id { get; set; }
        public DateTime Time { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ExceptionDetails { get; set; }
    }
}

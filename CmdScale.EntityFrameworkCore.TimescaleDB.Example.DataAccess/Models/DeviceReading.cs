using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    [Hypertable(nameof(Time), ChunkSkipColumns = new[] { "Time" }, ChunkTimeInterval = "1 day")]
    [ReorderPolicy("DeviceReadings_Time_idx", InitialStart = "2025-09-23T09:15:19.3905112Z", ScheduleInterval = "1 day", MaxRuntime = "00:00:00", RetryPeriod = "00:05:00")]
    [PrimaryKey(nameof(Id), nameof(Time))]
    public class DeviceReading
    {
        public Guid Id { get; set; }
        public DateTime Time { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Voltage { get; set; }
        public double Power { get; set; }
    }
}
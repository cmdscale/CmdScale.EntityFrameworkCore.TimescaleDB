using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    [Hypertable(nameof(Time), ChunkSkipColumns = new[] { "Time" }, ChunkTimeInterval = "1 day")]
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
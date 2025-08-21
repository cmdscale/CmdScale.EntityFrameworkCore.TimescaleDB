using CmdScale.EntityFrameworkCore.TimescaleDB.Annotation;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{

    [Hypertable(nameof(Time))]
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
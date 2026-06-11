using NodaTime;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// IoT sensor measurement whose time column is a NodaTime <see cref="Instant"/>.
    /// </summary>
    public class SensorMeasurement
    {
        public Guid Id { get; set; }
        public Instant RecordedAt { get; set; }
        public string SensorId { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }
}

using NodaTime;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// IoT sensor measurement in long format, whose time column is a NodaTime <see cref="Instant"/>.
    /// </summary>
    public class SensorMeasurement
    {
        public Guid Id { get; set; }
        public Instant RecordedAt { get; set; }

        /// <summary>Physical site the reading originates from (e.g. "berlin-dc1"). Low cardinality.</summary>
        public string Site { get; set; } = string.Empty;

        /// <summary>Kind of metric recorded (e.g. "temperature", "humidity"). Low cardinality.</summary>
        public string MetricType { get; set; } = string.Empty;

        /// <summary>Identifier of the individual sensor reporting the value. Higher cardinality.</summary>
        public string SensorId { get; set; } = string.Empty;

        /// <summary>The measured value for the given metric type.</summary>
        public double Value { get; set; }
    }
}

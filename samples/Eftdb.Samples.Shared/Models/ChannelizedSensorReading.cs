using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// An IoT sensor reading that exposes two measurement channels as EF Core complex-type
    /// properties.
    /// </summary>
    [PrimaryKey(nameof(Id), nameof(RecordedAt))]
    public class ChannelizedSensorReading
    {
        public Guid Id { get; set; }
        public DateTime RecordedAt { get; set; }
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Primary measurement channel (e.g. temperature in °C).
        /// Maps to columns <c>Primary_Name</c> and <c>Primary_Value</c> by default;
        /// snake_case convention yields <c>primary_name</c> / <c>primary_value</c>.
        /// </summary>
        public SensorChannel Primary { get; set; } = new();

        /// <summary>
        /// Secondary measurement channel (e.g. humidity in %).
        /// Maps to columns <c>Secondary_Name</c> and <c>Secondary_Value</c> by default;
        /// snake_case convention yields <c>secondary_name</c> / <c>secondary_value</c>.
        /// </summary>
        public SensorChannel Secondary { get; set; } = new();
    }
}

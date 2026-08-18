using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    [PrimaryKey(nameof(Id), nameof(RecordedAt))]
    public class StationReading
    {
        public Guid Id { get; set; }
        public DateTime RecordedAt { get; set; }
        public double Temperature { get; set; }

        /// <summary>
        /// Geographic location of the monitoring station.
        /// Contains a nested <see cref="Coordinates"/> complex type, producing columns
        /// such as <c>Location_Site</c>, <c>Location_Coordinates_Latitude</c>, and
        /// <c>Location_Coordinates_Longitude</c> on the <c>station_readings</c> table.
        /// </summary>
        public Location Location { get; set; } = new();
    }
}

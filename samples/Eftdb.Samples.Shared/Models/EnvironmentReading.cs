using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    [Hypertable(
        nameof(ObservedAt),
        ChunkTimeInterval = "1 day",
        EnableCompression = true,
        CompressionSegmentBy = new[] { "Station" },
        CompressionOrderBy = new[] { "ObservedAt DESC" },
        CompressChunkTimeInterval = "7 days")]
    [SparseIndex(ESparseIndexType.Bloom, nameof(EnvironmentReading.Station))]
    [SparseIndex(ESparseIndexType.MinMax, nameof(EnvironmentReading.Pressure))]
    [PrimaryKey(nameof(Id), nameof(ObservedAt))]
    public class EnvironmentReading
    {
        public Guid Id { get; set; }
        public LocalDateTime ObservedAt { get; set; }
        public string Station { get; set; } = string.Empty;
        public double Pressure { get; set; }
        public double WindSpeed { get; set; }
    }
}

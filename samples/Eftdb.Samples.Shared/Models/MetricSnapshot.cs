using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    [Hypertable(
        nameof(RecordedAt),
        ChunkTimeInterval = "1 day",
        EnableCompression = true,
        CompressionSegmentBy = [nameof(ServiceName)],
        CompressionOrderBy = [$"{nameof(MetricName)} ASC", $"{nameof(RecordedAt)} DESC"])]
    [CompressionPolicy(
        CreatedBefore = "30 days",
        ScheduleInterval = "1 day",
        Timezone = "UTC")]
    [PrimaryKey(nameof(Id), nameof(RecordedAt))]
    public class MetricSnapshot
    {
        /// <summary>Row identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>UTC timestamp when the metric was sampled.</summary>
        public DateTime RecordedAt { get; set; }

        /// <summary>Name of the service emitting the metric (e.g. "checkout-api").</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>Name of the metric (e.g. "request_count", "p99_latency_ms").</summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>Sampled numeric value.</summary>
        public double Value { get; set; }

        /// <summary>Optional unit label (e.g. "ms", "req/s").</summary>
        public string? Unit { get; set; }
    }
}

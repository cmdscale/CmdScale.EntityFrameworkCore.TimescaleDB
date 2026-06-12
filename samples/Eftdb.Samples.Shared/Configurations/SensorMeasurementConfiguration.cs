using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    public class SensorMeasurementConfiguration : IEntityTypeConfiguration<SensorMeasurement>
    {
        public void Configure(EntityTypeBuilder<SensorMeasurement> builder)
        {
            builder.ToTable("sensor_measurements");
            builder.HasKey(x => new { x.Id, x.RecordedAt });

            builder.IsHypertable(x => x.RecordedAt)
                   .WithChunkTimeInterval("1 day")
                   .WithCompressionSegmentBy(x => x.Site, x => x.MetricType)
                   .WithCompressionOrderBy(
                       s => s.By(x => x.SensorId),
                       s => s.ByDescending(x => x.RecordedAt));
        }
    }
}

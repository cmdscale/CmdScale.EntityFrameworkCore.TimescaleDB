using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    public class ChannelizedSensorReadingConfiguration : IEntityTypeConfiguration<ChannelizedSensorReading>
    {
        public void Configure(EntityTypeBuilder<ChannelizedSensorReading> builder)
        {
            builder.ToTable("channelized_sensor_readings");

            builder.IsHypertable(x => x.RecordedAt)
                   .WithChunkTimeInterval("1 day")
                   .WithCompressionSegmentBy(x => x.DeviceId)
                   .WithCompressionOrderBy(
                       s => s.ByDescending(x => x.RecordedAt));
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    /// <summary>
    /// Fluent API configuration for <see cref="StationReading"/>.
    /// Explicitly registers the two-level complex-type hierarchy.
    /// </summary>
    public class StationReadingConfiguration : IEntityTypeConfiguration<StationReading>
    {
        public void Configure(EntityTypeBuilder<StationReading> builder)
        {
            builder.ToTable("station_readings");
            builder.HasKey(x => new { x.Id, x.RecordedAt });

            builder.ComplexProperty(x => x.Location, l =>
                l.ComplexProperty(c => c.Coordinates));

            builder.IsHypertable(x => x.RecordedAt)
                   .WithChunkTimeInterval("1 day")
                   .WithCompressionSegmentBy(x => x.Location.Site)
                   .EnableCompression();
        }
    }
}

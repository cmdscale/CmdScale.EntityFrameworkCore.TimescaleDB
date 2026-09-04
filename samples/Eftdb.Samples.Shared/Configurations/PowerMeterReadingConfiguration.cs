using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    /// <summary>
    /// Fluent API configuration for <see cref="PowerMeterReading"/>, the source hypertable
    /// at the base of the hierarchical continuous aggregate chain
    /// (see <see cref="PowerUsageHourlyConfiguration"/> and <see cref="PowerUsageDailyConfiguration"/>).
    /// </summary>
    public class PowerMeterReadingConfiguration : IEntityTypeConfiguration<PowerMeterReading>
    {
        public void Configure(EntityTypeBuilder<PowerMeterReading> builder)
        {
            builder.ToTable("power_meter_readings");
            builder.HasKey(x => new { x.MeterId, x.Timestamp });

            builder.IsHypertable(x => x.Timestamp)
                   .WithChunkTimeInterval("1 day");
        }
    }
}

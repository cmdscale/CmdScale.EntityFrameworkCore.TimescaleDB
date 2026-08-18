using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    public class HourlySensorAggregateConfiguration : IEntityTypeConfiguration<HourlySensorAggregate>
    {
        public void Configure(EntityTypeBuilder<HourlySensorAggregate> builder)
        {
            builder.HasNoKey();

            builder.IsContinuousAggregate<HourlySensorAggregate, ChannelizedSensorReading>(
                    materializedViewName: "hourly_sensor_aggregates",
                    timeBucketWidth: "1 hour",
                    propertyExpression: source => source.RecordedAt,
                    timeBucketGroupBy: true)

                // Aggregate functions whose source columns are complex-type members.
                // The selector `source => source.Primary.Value` produces the path
                // "Primary.Value" which is resolved to the mapped column name at
                // migration generation time.
                .AddAggregateFunction(
                    agg => agg.AvgPrimaryValue,
                    source => source.Primary.Value,
                    EAggregateFunction.Avg)
                .AddAggregateFunction(
                    agg => agg.MinPrimaryValue,
                    source => source.Primary.Value,
                    EAggregateFunction.Min)
                .AddAggregateFunction(
                    agg => agg.MaxPrimaryValue,
                    source => source.Primary.Value,
                    EAggregateFunction.Max)

                // Cross-channel aggregate: secondary value average.
                .AddAggregateFunction(
                    agg => agg.AvgSecondaryValue,
                    source => source.Secondary.Value,
                    EAggregateFunction.Avg)

                .AddAggregateFunction(
                    agg => agg.ReadingCount,
                    source => source.RecordedAt,
                    EAggregateFunction.Count)

                // Group by a complex-type member: the channel name on the primary channel.
                // Resolves to the mapped column for Primary.Name (e.g. "primary_name").
                .AddGroupByColumn(source => source.Primary.Name)

                // Also group by device so each bucket is per-device, per-channel-name.
                .AddGroupByColumn(source => source.DeviceId);
        }
    }
}

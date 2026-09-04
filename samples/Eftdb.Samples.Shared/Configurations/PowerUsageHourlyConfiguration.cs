using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    /// <summary>
    /// Level 1 of the hierarchical continuous aggregate chain: an hourly rollup materialized
    /// directly from the <see cref="PowerMeterReading"/> hypertable.
    /// </summary>
    public class PowerUsageHourlyConfiguration : IEntityTypeConfiguration<PowerUsageHourly>
    {
        public void Configure(EntityTypeBuilder<PowerUsageHourly> builder)
        {
            builder.HasNoKey();

            builder.IsContinuousAggregate<PowerUsageHourly, PowerMeterReading>(
                    materializedViewName: "power_usage_hourly",
                    timeBucketWidth: "1 hour",
                    propertyExpression: source => source.Timestamp,
                    timeBucketGroupBy: true)
                // The generated view aliases its bucket column to this property's mapped column
                // name (hour_start under the snake_case convention) instead of the default
                // "time_bucket", so the level 2 daily aggregate references it by that name.
                .WithTimeBucketProperty(x => x.HourStart)
                .AddAggregateFunction(agg => agg.MinPowerKw, source => source.PowerKw, EAggregateFunction.Min)
                .AddAggregateFunction(agg => agg.MaxPowerKw, source => source.PowerKw, EAggregateFunction.Max)
                .AddAggregateFunction(agg => agg.TotalPowerKw, source => source.PowerKw, EAggregateFunction.Sum)
                .AddAggregateFunction(agg => agg.ReadingCount, source => source.Timestamp, EAggregateFunction.Count)
                .AddGroupByColumn(source => source.MeterId)
                .WithRefreshPolicy(startOffset: "3 days", endOffset: "1 hour", scheduleInterval: "1 hour");
        }
    }
}

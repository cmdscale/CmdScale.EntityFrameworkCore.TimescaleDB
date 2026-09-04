using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    /// <summary>
    /// Level 2 of the hierarchical continuous aggregate chain: a daily rollup whose source is
    /// the <see cref="PowerUsageHourly"/> continuous aggregate, not the raw hypertable. This is
    /// what makes the aggregate "hierarchical" — the source type parameter of
    /// <c>IsContinuousAggregate&lt;TChild, TParent&gt;</c> is another aggregate entity.
    /// </summary>
    public class PowerUsageDailyConfiguration : IEntityTypeConfiguration<PowerUsageDaily>
    {
        public void Configure(EntityTypeBuilder<PowerUsageDaily> builder)
        {
            builder.HasNoKey();

            builder.IsContinuousAggregate<PowerUsageDaily, PowerUsageHourly>(
                    materializedViewName: "power_usage_daily",
                    timeBucketWidth: "1 day",
                    propertyExpression: source => source.HourStart,
                    timeBucketGroupBy: true)
                .WithTimeBucketProperty(x => x.DayStart)
                // The daily view aliases its bucket column to this property's mapped column name
                // (day_start under the snake_case convention) instead of the default "time_bucket".
                .AddAggregateFunction(agg => agg.MinPowerKw, source => source.MinPowerKw, EAggregateFunction.Min)
                .AddAggregateFunction(agg => agg.MaxPowerKw, source => source.MaxPowerKw, EAggregateFunction.Max)
                .AddAggregateFunction(agg => agg.TotalPowerKw, source => source.TotalPowerKw, EAggregateFunction.Sum)
                .AddAggregateFunction(agg => agg.ReadingCount, source => source.ReadingCount, EAggregateFunction.Sum)
                .AddGroupByColumn(source => source.MeterId)
                .WithRefreshPolicy(startOffset: "30 days", endOffset: "1 day", scheduleInterval: "1 hour");
        }
    }
}

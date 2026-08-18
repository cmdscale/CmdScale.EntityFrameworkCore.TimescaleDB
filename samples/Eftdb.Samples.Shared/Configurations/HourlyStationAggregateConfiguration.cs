using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Configurations
{
    /// <summary>
    /// Fluent API configuration for the <see cref="HourlyStationAggregate"/> continuous aggregate.
    /// Demonstrates two-hop nested complex-type column resolution.
    /// </summary>
    public class HourlyStationAggregateConfiguration : IEntityTypeConfiguration<HourlyStationAggregate>
    {
        public void Configure(EntityTypeBuilder<HourlyStationAggregate> builder)
        {
            builder.HasNoKey();

            builder.IsContinuousAggregate<HourlyStationAggregate, StationReading>(
                    materializedViewName: "hourly_station_aggregates",
                    timeBucketWidth: "1 hour",
                    propertyExpression: source => source.RecordedAt,
                    timeBucketGroupBy: true)

                .AddAggregateFunction(
                    agg => agg.AvgLatitude,
                    source => source.Location.Coordinates.Latitude,
                    EAggregateFunction.Avg)

                .AddAggregateFunction(
                    agg => agg.AvgTemperature,
                    source => source.Temperature,
                    EAggregateFunction.Avg)

                .AddGroupByColumn(source => source.Location.Site);
        }
    }
}

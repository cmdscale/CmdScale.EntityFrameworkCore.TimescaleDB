using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    [ContinuousAggregate(
        MaterializedViewName = "weather_aggregates",
        ParentName = "weather",
        ChunkInterval = "1 month",
        WithNoData = true,
        CreateGroupIndexes = true,
        MaterializedOnly = false)]
    [TimeBucket("1 day", nameof(WeatherData.Time), GroupBy = true)]
    public class WeatherAggregates
    {
        [Aggregate(EAggregateFunction.Avg, nameof(WeatherData.Temperature))]
        public double AverageTemperature { get; set; }

        [Aggregate(EAggregateFunction.Max, nameof(WeatherData.Humidity))]
        public double MaxHumidity { get; set; }

        [Aggregate(EAggregateFunction.Min, nameof(WeatherData.Humidity))]
        public double MinHumidity { get; set; }
    }
}

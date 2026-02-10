using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// Example continuous aggregate showcasing all possible configuration properties and aggregate functions.
    /// This aggregates weather data into daily buckets with various statistical measures.
    /// </summary>
    [Keyless]
    [ContinuousAggregate(
        MaterializedViewName = "weather_aggregates",
        ParentName = nameof(WeatherData),
        ChunkInterval = "1 month",
        WithNoData = true,
        CreateGroupIndexes = true,
        MaterializedOnly = false,
        Where = "\"temperature\" > -50 AND \"humidity\" >= 0")]
    [TimeBucket("1 day", nameof(WeatherData.Time), GroupBy = true)]
    [ContinuousAggregatePolicy(
        StartOffset = "30 days",
        EndOffset = "1 day",
        ScheduleInterval = "1 hour",
        
        RefreshNewestFirst = true)]
    public class WeatherAggregate
    {
        // Avg aggregate function
        [Aggregate(EAggregateFunction.Avg, nameof(WeatherData.Temperature))]
        public double AverageTemperature { get; set; }

        // Max aggregate function
        [Aggregate(EAggregateFunction.Max, nameof(WeatherData.Humidity))]
        public double MaxHumidity { get; set; }

        // Min aggregate function
        [Aggregate(EAggregateFunction.Min, nameof(WeatherData.Humidity))]
        public double MinHumidity { get; set; }

        // Sum aggregate function
        [Aggregate(EAggregateFunction.Sum, nameof(WeatherData.Temperature))]
        public double TotalTemperature { get; set; }

        // Count aggregate function (using "*" for count all records)
        [Aggregate(EAggregateFunction.Count, "*")]
        public int RecordCount { get; set; }

        // First aggregate function (gets first temperature value in time bucket)
        [Aggregate(EAggregateFunction.First, nameof(WeatherData.Temperature))]
        public double FirstTemperature { get; set; }

        // Last aggregate function (gets last temperature value in time bucket)
        [Aggregate(EAggregateFunction.Last, nameof(WeatherData.Temperature))]
        public double LastTemperature { get; set; }
    }
}

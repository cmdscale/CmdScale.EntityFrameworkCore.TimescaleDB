# Continuous Aggregates

Continuous aggregates in TimescaleDB are materialized views designed specifically for time-series data. They automatically maintain pre-computed aggregations of data from a source hypertable, dramatically improving query performance for analytical workloads. Unlike standard materialized views, continuous aggregates refresh incrementally, only processing new data since the last refresh rather than recalculating the entire aggregate.

## Creating a Continuous Aggregate

To create a continuous aggregate, use the `.IsContinuousAggregate<TEntity, TSourceEntity>()` method in the entity configuration. This method requires specification of the aggregate entity type and the source hypertable entity type.

The continuous aggregate uses TimescaleDB's `time_bucket()` function to group time-series data into fixed intervals, enabling efficient rollups of metrics like averages, sums, minimums, and maximums.

[See also: CREATE MATERIALIZED VIEW (Continuous Aggregate)](https://docs.tigerdata.com/api/latest/continuous-aggregates/create_materialized_view/)

### Basic Configuration

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TradeAggregateConfiguration : IEntityTypeConfiguration<TradeAggregate>
{
    public void Configure(EntityTypeBuilder<TradeAggregate> builder)
    {
        builder.HasNoKey();

        // Create a continuous aggregate that groups trades into 1-hour buckets
        builder.IsContinuousAggregate<TradeAggregate, Trade>(
                "trade_hourly_stats",           // Materialized view name
                "1 hour",                        // Time bucket width
                x => x.Timestamp,                // Source time column
                timeBucketGroupBy: true,         // Include time bucket in GROUP BY
                chunkInterval: "7 days")         // Chunk interval for aggregate data
            .AddAggregateFunction(
                x => x.AveragePrice,             // Aggregate entity property
                x => x.Price,                    // Source entity column
                EAggregateFunction.Avg);         // Aggregate function
    }
}

public class TradeAggregate
{
    public decimal AveragePrice { get; set; }
}

public class Trade
{
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public string Ticker { get; set; } = string.Empty;
}
```

## Adding Aggregate Functions

Continuous aggregates support multiple aggregate functions that can be applied to source hypertable columns.

### Supported Aggregate Functions

The following aggregate functions are available through the `EAggregateFunction` enum:

- **Avg**: Calculate the average value
- **Sum**: Calculate the sum of values
- **Min**: Find the minimum value
- **Max**: Find the maximum value
- **Count**: Count the number of rows
- **First**: Get the first value in the time window
- **Last**: Get the last value in the time window

### Adding Multiple Aggregations

```csharp
public void Configure(EntityTypeBuilder<TradeAggregate> builder)
{
    builder.HasNoKey();

    builder.IsContinuousAggregate<TradeAggregate, Trade>(
            "trade_hourly_stats",
            "1 hour",
            x => x.Timestamp)
        .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
        .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
        .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Min)
        .AddAggregateFunction(x => x.TotalVolume, x => x.Size, EAggregateFunction.Sum)
        .AddAggregateFunction(x => x.TradeCount, x => x.Timestamp, EAggregateFunction.Count);
}
```

## Grouping Data

Beyond the time bucket, continuous aggregates can group data by additional columns from the source hypertable.

### Group By Column

```csharp
public void Configure(EntityTypeBuilder<TradeAggregate> builder)
{
    builder.HasNoKey();

    builder.IsContinuousAggregate<TradeAggregate, Trade>(
            "trade_hourly_stats_by_ticker",
            "1 hour",
            x => x.Timestamp)
        .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
        // Group by ticker symbol to get per-ticker statistics
        .AddGroupByColumn(x => x.Ticker);
}
```

### Group By Expression

For complex grouping scenarios, raw SQL expressions can be provided:

```csharp
public void Configure(EntityTypeBuilder<TradeAggregate> builder)
{
    builder.HasNoKey();

    builder.IsContinuousAggregate<TradeAggregate, Trade>(
            "trade_hourly_stats",
            "1 hour",
            x => x.Timestamp)
        .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
        // Group by ordinal positions in SELECT list
        .AddGroupByColumn("1, 2");
}
```

## Filtering Source Data

Apply filtering conditions to the source hypertable before aggregation using the `.Where()` method.

```csharp
public void Configure(EntityTypeBuilder<TradeAggregate> builder)
{
    builder.HasNoKey();

    builder.IsContinuousAggregate<TradeAggregate, Trade>(
            "nasdaq_trade_stats",
            "1 hour",
            x => x.Timestamp)
        .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
        .AddGroupByColumn(x => x.Ticker)
        // Only include trades from NASDAQ exchange
        .Where("\"exchange\" = 'NASDAQ'");
}
```

> :warning: **Note:** The WHERE clause should be a valid SQL expression without the "WHERE" keyword. Use double quotes for column identifiers if needed.

## Configuration Options

### WithNoData

By default, continuous aggregates are populated with data when created. Use `.WithNoData()` to create an empty aggregate that will be populated on the first refresh:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithNoData(true);
```

### CreateGroupIndexes

Control whether indexes are automatically created on GROUP BY columns. Enabled by default:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .AddGroupByColumn(x => x.Ticker)
    .CreateGroupIndexes(true);
```

### MaterializedOnly

By default, queries to a continuous aggregate combine materialized data with recent unmaterialized data from the source hypertable. Use `.MaterializedOnly()` to return only the pre-computed materialized data:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .MaterializedOnly(true);
```

## Complete Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.HasKey(x => new { x.Ticker, x.Timestamp });

        // Configure the source hypertable
        builder.IsHypertable(x => x.Timestamp)
            .WithChunkTimeInterval("7 days");
    }
}

public class TradeAggregateConfiguration : IEntityTypeConfiguration<TradeAggregate>
{
    public void Configure(EntityTypeBuilder<TradeAggregate> builder)
    {
        builder.HasNoKey();

        // Configure comprehensive continuous aggregate
        builder.IsContinuousAggregate<TradeAggregate, Trade>(
                "trade_hourly_stats",
                "1 hour",
                x => x.Timestamp,
                timeBucketGroupBy: true,
                chunkInterval: "7 days")
            .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
            .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
            .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Min)
            .AddAggregateFunction(x => x.TotalVolume, x => x.Size, EAggregateFunction.Sum)
            .AddAggregateFunction(x => x.TradeCount, x => x.Timestamp, EAggregateFunction.Count)
            .AddGroupByColumn(x => x.Ticker)
            .AddGroupByColumn(x => x.Exchange)
            .Where("\"price\" > 0 AND \"size\" > 0")
            .CreateGroupIndexes(true)
            .MaterializedOnly(false);
    }
}

public class Trade
{
    public DateTime Timestamp { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Size { get; set; }
}

public class TradeAggregate
{
    public decimal AveragePrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal TotalVolume { get; set; }
    public int TradeCount { get; set; }
}
```

## Refresh Policies

Continuous aggregates can be configured with automatic refresh policies that run on a schedule to keep the materialized view up-to-date. The refresh policy executes TimescaleDB's `add_continuous_aggregate_policy()` function, which automatically refreshes data within a specified time window.

[See also: add_continuous_aggregate_policy](https://docs.tigerdata.com/api/latest/continuous-aggregates/add_continuous_aggregate_policy/)

### Basic Refresh Policy Configuration

Use the `.WithRefreshPolicy()` method to add an automatic refresh policy to a continuous aggregate:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TradeAggregateConfiguration : IEntityTypeConfiguration<TradeAggregate>
{
    public void Configure(EntityTypeBuilder<TradeAggregate> builder)
    {
        builder.HasNoKey();

        builder.IsContinuousAggregate<TradeAggregate, Trade>(
                "trade_hourly_stats",
                "1 hour",
                x => x.Timestamp,
                timeBucketGroupBy: true,
                chunkInterval: "7 days")
            .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
            .WithRefreshPolicy(
                startOffset: "7 days",      // Refresh data from the last 7 days
                endOffset: "1 hour",         // Exclude the most recent hour (still incoming)
                scheduleInterval: "1 hour"); // Run refresh every hour
    }
}
```

### Refresh Policy Parameters

The `.WithRefreshPolicy()` method accepts the following parameters:

- **startOffset**: Window start as an interval relative to execution time. NULL or empty string equals earliest data. Examples: "1 month", "7 days", "100000" (for integer-based time columns).
- **endOffset**: Window end as an interval relative to execution time. NULL or empty string equals latest data. Examples: "1 hour", "1 day", "1000" (for integer-based time columns).
- **scheduleInterval**: Interval between refresh executions in wall-clock time. Defaults to "24 hours" if not specified. Examples: "1 hour", "30 minutes".

### Advanced Refresh Policy Options

The `.WithRefreshPolicy()` method returns a `ContinuousAggregatePolicyBuilder` that provides additional configuration methods for fine-tuning the refresh behavior:

#### WithInitialStart

Sets the first time the policy job is scheduled to run:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
    .WithInitialStart(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
```

#### WithIfNotExists

Issues a notice instead of an error if the policy job already exists:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
    .WithIfNotExists(true);
```

#### WithIncludeTieredData

Overrides tiered read settings for the refresh policy:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
    .WithIncludeTieredData(true);
```

#### WithBucketsPerBatch

Sets the number of time buckets processed per batch transaction. Defaults to 1, minimum value is 1:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
    .WithBucketsPerBatch(10);
```

#### WithMaxBatchesPerExecution

Sets the maximum number of batches executed per run. 0 means unlimited. Defaults to 0:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
    .WithMaxBatchesPerExecution(5);
```

#### WithRefreshNewestFirst

Sets the direction of incremental refresh. True refreshes newest data first, false refreshes oldest first. Defaults to true:

```csharp
builder.IsContinuousAggregate<TradeAggregate, Trade>(
        "trade_hourly_stats",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour")
    .WithRefreshNewestFirst(true);
```

### Complete Refresh Policy Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TradeAggregateConfiguration : IEntityTypeConfiguration<TradeAggregate>
{
    public void Configure(EntityTypeBuilder<TradeAggregate> builder)
    {
        builder.HasNoKey();

        builder.IsContinuousAggregate<TradeAggregate, Trade>(
                "trade_hourly_stats",
                "1 hour",
                x => x.Timestamp,
                timeBucketGroupBy: true,
                chunkInterval: "7 days")
            .AddAggregateFunction(x => x.AveragePrice, x => x.Price, EAggregateFunction.Avg)
            .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
            .AddAggregateFunction(x => x.MinPrice, x => x.Price, EAggregateFunction.Min)
            .AddAggregateFunction(x => x.TotalVolume, x => x.Size, EAggregateFunction.Sum)
            .AddGroupByColumn(x => x.Ticker)
            .WithRefreshPolicy(
                startOffset: "30 days",
                endOffset: "1 hour",
                scheduleInterval: "1 hour")
            .WithInitialStart(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc))
            .WithIfNotExists(true)
            .WithBucketsPerBatch(5)
            .WithMaxBatchesPerExecution(10)
            .WithRefreshNewestFirst(true);
    }
}

public class Trade
{
    public DateTime Timestamp { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Size { get; set; }
}

public class TradeAggregate
{
    public decimal AveragePrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal TotalVolume { get; set; }
}
```

> :warning: **Note:** The refresh policy runs as a background job managed by TimescaleDB. Ensure the TimescaleDB background worker is enabled in your database configuration.

## Important Notes

- Continuous aggregate entities should use `.HasNoKey()` since they represent views, not tables.
- The source entity must be a TimescaleDB hypertable.
- The time bucket width determines the aggregation granularity (e.g., "1 hour", "1 day", "15 minutes").
- Chunk interval for the aggregate's underlying materialized hypertable defaults to 10 times the source hypertable's chunk interval if not specified.
- Continuous aggregates support hierarchical aggregation (aggregating from another continuous aggregate).
- Refresh policies can be configured to automatically keep the aggregate up-to-date.

## Common Use Cases

### Hourly Metrics Dashboard

Pre-compute hourly statistics for real-time dashboards without querying raw data:

```csharp
builder.IsContinuousAggregate<MetricsHourly, SensorReading>(
        "sensor_metrics_hourly",
        "1 hour",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.AvgTemperature, x => x.Temperature, EAggregateFunction.Avg)
    .AddAggregateFunction(x => x.MaxTemperature, x => x.Temperature, EAggregateFunction.Max)
    .AddGroupByColumn(x => x.DeviceId);
```

### Daily Rollups

Create daily summaries for long-term trend analysis:

```csharp
builder.IsContinuousAggregate<DailySummary, OrderEvent>(
        "orders_daily",
        "1 day",
        x => x.OrderDate)
    .AddAggregateFunction(x => x.TotalRevenue, x => x.Amount, EAggregateFunction.Sum)
    .AddAggregateFunction(x => x.OrderCount, x => x.OrderId, EAggregateFunction.Count)
    .AddGroupByColumn(x => x.Region);
```

### Downsampling High-Frequency Data

Reduce storage and improve query performance for high-frequency sensor data:

```csharp
builder.IsContinuousAggregate<SensorMinute, SensorReading>(
        "sensor_per_minute",
        "1 minute",
        x => x.Timestamp)
    .AddAggregateFunction(x => x.FirstValue, x => x.Value, EAggregateFunction.First)
    .AddAggregateFunction(x => x.LastValue, x => x.Value, EAggregateFunction.Last)
    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
    .AddGroupByColumn(x => x.SensorId);
```

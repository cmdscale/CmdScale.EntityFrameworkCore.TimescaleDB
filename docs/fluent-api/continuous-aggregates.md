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

Beyond the time bucket, continuous aggregates can group data by additional columns from the source hypertable. In Data Annotations configuration, the same is expressed with the property-level [`[GroupByColumn]` attribute](../data-annotations/continuous-aggregates#grouping-by-additional-columns); raw SQL expressions are Fluent-API-only.

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

## Naming the Time-Bucket Column

The materialized view's bucket column is named `time_bucket` by default, matching the TimescaleDB `time_bucket()` function-name default. Querying the aggregate entity requires a property mapped to that column — either explicitly via `.HasColumnName("time_bucket")`, or implicitly through a naming convention on a property named `TimeBucket`.

`.WithTimeBucketProperty(agg => agg.Prop)` designates a property as the bucket target. The generated view then aliases the bucket column to that property's mapped column name, so no `.HasColumnName("time_bucket")` magic string is needed, and custom names such as `hour_start` work. Resolution respects the active naming convention: a `HourStart` property under snake_case maps to `hour_start`, and the view's bucket is aliased accordingly.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PowerUsageHourlyConfiguration : IEntityTypeConfiguration<PowerUsageHourly>
{
    public void Configure(EntityTypeBuilder<PowerUsageHourly> builder)
    {
        builder.HasNoKey();

        builder.IsContinuousAggregate<PowerUsageHourly, PowerMeterReading>(
                "power_usage_hourly",
                "1 hour",
                x => x.Timestamp)
            // The view aliases its bucket column to HourStart's mapped column name
            // (hour_start under snake_case) instead of the default "time_bucket".
            .WithTimeBucketProperty(x => x.HourStart)
            .AddAggregateFunction(x => x.AvgPowerKw, x => x.PowerKw, EAggregateFunction.Avg);
    }
}

public class PowerMeterReading
{
    public string MeterId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double PowerKw { get; set; }
}

public class PowerUsageHourly
{
    public DateTime HourStart { get; set; }
    public double AvgPowerKw { get; set; }
}
```

The string-based builder used by scaffolded code exposes an equivalent `.WithTimeBucketProperty("HourStart")` overload.

> :warning: **Note:** The bucket column name is part of the view's structural definition. Designating a property whose mapped column differs from `time_bucket` on an **existing** aggregate changes that column name, which forces a drop and recreate of the aggregate (materialized data is rebuilt). In a hierarchy the drop cascades to every descendant aggregate. See [Migration Ordering](#migration-ordering).

> :warning: **Note:** Undesignated aggregates are unaffected: without `.WithTimeBucketProperty(...)` the bucket column stays `time_bucket`, byte-for-byte identical to earlier versions.

## Model Validation

Structured aggregates (those configured through the builders rather than a raw view definition) are validated at model finalization:

- Duplicate output column names are rejected with an `InvalidOperationException`. The check compares the bucket column, all GROUP BY columns, and every aggregate alias after resolving them to database column names. A source column that collides with the bucket column name is caught at model build.
- A property designated via `.WithTimeBucketProperty(...)` that does not exist on the entity raises an `InvalidOperationException`.
- An aggregate with no time-bucket designation (`.WithTimeBucketProperty(...)`) and no property mapping to the default bucket column `time_bucket` emits a warning through the configured EF logger. The view still exposes a `time_bucket` column, but it cannot be queried through the entity; previously this surfaced only at query time as an opaque Postgres "column does not exist" error. Remedy by designating a property or mapping one to `time_bucket`. This is a warning rather than an exception because deliberately not exposing the bucket is legal.

> :warning: **Note:** Entities scaffolded with a raw view definition are exempt from all checks, because the structured projection fields are unused on that path.

## Hierarchical Continuous Aggregates

A continuous aggregate can aggregate from another continuous aggregate rather than from the raw hypertable, forming a rollup chain (for example hourly &rarr; daily). This reduces the work of coarse-grained rollups: the daily aggregate reads pre-computed hourly buckets instead of every raw row.

The child is configured with the ordinary `.IsContinuousAggregate<TChild, TParentAggregate>()` overload. The source type parameter is the **parent aggregate entity** (not the raw hypertable), and the time-bucket selector picks the parent aggregate's bucket property.

The child's `time_bucket()` call references the parent's bucket column by name, so the parent's bucket property must resolve to a known column. Two equivalent options exist:

- Designate the parent's bucket property with `.WithTimeBucketProperty(x => x.HourStart)` (see [Naming the Time-Bucket Column](#naming-the-time-bucket-column)). The designated name flows through resolution automatically: the child's `propertyExpression: parent => parent.HourStart` selector picks the same property, and the generated SQL agrees on the column name.
- Map the bucket property to the default column explicitly via `.Property(x => x.TimeBucket).HasColumnName("time_bucket")`. The view exposes its bucket under `time_bucket`, and the child references it by that name.

Either mapping is also what makes LINQ queries against an aggregate's bucket column work.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;

public class MarketDataContext : DbContext
{
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<TradeHourly> TradesHourly => Set<TradeHourly>();
    public DbSet<TradeDaily> TradesDaily => Set<TradeDaily>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Source hypertable
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(x => new { x.Ticker, x.Timestamp });
            entity.IsHypertable(x => x.Timestamp);
        });

        // Level 1: hourly aggregate over the raw hypertable
        modelBuilder.Entity<TradeHourly>(entity =>
        {
            entity.HasNoKey();
            entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            entity.IsContinuousAggregate<TradeHourly, Trade>("trade_hourly", "1 hour", x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);
        });

        // Level 2: daily aggregate whose source is the hourly aggregate
        modelBuilder.Entity<TradeDaily>(entity =>
        {
            entity.HasNoKey();
            entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
            entity.IsContinuousAggregate<TradeDaily, TradeHourly>("trade_daily", "1 day", x => x.TimeBucket)
                .AddAggregateFunction(x => x.AvgPrice, x => x.AvgPrice, EAggregateFunction.Avg);
        });
    }
}

public class Trade
{
    public string Ticker { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
}

public class TradeHourly
{
    public DateTime TimeBucket { get; set; }
    public decimal AvgPrice { get; set; }
}

public class TradeDaily
{
    public DateTime TimeBucket { get; set; }
    public decimal AvgPrice { get; set; }
}
```

### Migration Ordering

Ordering across the chain is handled automatically:

- Parents are created before their children; children are dropped before their parents.
- A structural change to a parent (bucket width, bucket column name, aggregate functions, GROUP BY, or WHERE) drops and recreates all of its descendants as well, and their refresh policies are re-added afterwards.

### Scaffolding

Database-first scaffolding of hierarchical aggregates is supported. The scaffolder resolves the child's parent to the parent aggregate's view (not the internal `_materialized_hypertable_N` table), so the generated `ParentName` refers to the parent aggregate entity.

> :warning: **Note:** TimescaleDB imposes server-side constraints on the child bucket width: it must be greater than, and an integer multiple of, the parent's bucket width. Calendar-based buckets (months, years, time zones) have additional rules. See the [TimescaleDB documentation on hierarchical continuous aggregates](https://docs.tigerdata.com/use-timescale/latest/continuous-aggregates/hierarchical-continuous-aggregates/) for the exact rules.

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
    public long TradeCount { get; set; }
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
- Continuous aggregates support [hierarchical aggregation](#hierarchical-continuous-aggregates) (aggregating from another continuous aggregate).
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

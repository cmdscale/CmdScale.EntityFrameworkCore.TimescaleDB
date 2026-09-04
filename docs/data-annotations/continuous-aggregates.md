# Continuous Aggregates

Continuous aggregates in TimescaleDB are materialized views designed specifically for time-series data. They automatically maintain pre-computed aggregations of data from a source hypertable, dramatically improving query performance for analytical workloads. Unlike standard materialized views, continuous aggregates refresh incrementally, only processing new data since the last refresh rather than recalculating the entire aggregate.

## Creating a Continuous Aggregate

To create a continuous aggregate using Data Annotations, apply the `[ContinuousAggregate]` attribute to the entity class. This attribute requires specification of the materialized view name, parent hypertable name, and time bucketing configuration.

Use the `[TimeBucket]` attribute to configure the time bucketing function that groups time-series data into fixed intervals, and the `[Aggregate]` attribute on properties to define aggregation functions.

[See also: CREATE MATERIALIZED VIEW (Continuous Aggregate)](https://docs.tigerdata.com/api/latest/continuous-aggregates/create_materialized_view/)

### Basic Configuration

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;

[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "trade_hourly_stats",
    ParentName = nameof(Trade))]
[TimeBucket("1 hour", nameof(Trade.Timestamp))]
public class TradeAggregate
{
    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AveragePrice { get; set; }
}

public class Trade
{
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public string Ticker { get; set; } = string.Empty;
}
```

> :warning: **Note:** Continuous aggregate entities must be marked with `[Keyless]` since they represent views, not tables.

## Aggregate Functions

The `[Aggregate]` attribute defines which aggregation function to apply to a source column and maps the result to the aggregate entity property.

### Supported Aggregate Functions

The following aggregate functions are available through the `EAggregateFunction` enum:

- **Avg**: Calculate the average value
- **Sum**: Calculate the sum of values
- **Min**: Find the minimum value
- **Max**: Find the maximum value
- **Count**: Count the number of rows
- **First**: Get the first value in the time window
- **Last**: Get the last value in the time window

### Defining Multiple Aggregations

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;

[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "trade_hourly_stats",
    ParentName = nameof(Trade))]
[TimeBucket("1 hour", nameof(Trade.Timestamp))]
public class TradeAggregate
{
    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AveragePrice { get; set; }

    [Aggregate(EAggregateFunction.Max, nameof(Trade.Price))]
    public decimal MaxPrice { get; set; }

    [Aggregate(EAggregateFunction.Min, nameof(Trade.Price))]
    public decimal MinPrice { get; set; }

    [Aggregate(EAggregateFunction.Sum, nameof(Trade.Size))]
    public decimal TotalVolume { get; set; }

    [Aggregate(EAggregateFunction.Count, "*")]
    public long TradeCount { get; set; }
}
```

> :warning: **Note:** For `COUNT(*)`, use the wildcard `"*"` as the source column.

## Time Bucketing Configuration

The `[TimeBucket]` attribute configures how time-series data is grouped into fixed intervals.

### TimeBucket Properties

- **BucketWidth** (required): The time interval for bucketing (e.g., "1 hour", "15 minutes", "1 day")
- **SourceColumn** (required): The name of the time column in the source hypertable
- **GroupBy** (optional): Whether to include the time bucket in the GROUP BY clause (default: true)

```csharp
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "sensor_daily_stats",
    ParentName = nameof(SensorReading))]
[TimeBucket("1 day", nameof(SensorReading.Timestamp), GroupBy = true)]
public class SensorDailyAggregate
{
    [Aggregate(EAggregateFunction.Avg, nameof(SensorReading.Temperature))]
    public double AverageTemperature { get; set; }
}
```

### Naming the Time-Bucket Column

The materialized view's bucket column is named `time_bucket` by default, matching the TimescaleDB `time_bucket()` function-name default. Querying the aggregate entity requires a property mapped to that column — either explicitly via `[Column("time_bucket")]`, or implicitly through a naming convention on a property named `TimeBucket`.

The `[TimeBucket]` attribute can be placed on either the class or a property:

- **Class placement** configures only the bucket width, source column, and GROUP BY behavior. The bucket column keeps the default `time_bucket` name.
- **Property placement** additionally designates that property as the bucket target. The generated view aliases its bucket column to the property's mapped column name — so custom names such as `hour_start` work — and no separate `[Column("time_bucket")]` is needed.

A property-level `[TimeBucket]` wins over a class-level one if both are present.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;

[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "power_usage_hourly",
    ParentName = nameof(PowerMeterReading))]
public class PowerUsageHourly
{
    // Property-level [TimeBucket]: configures the bucket AND aliases the view's
    // bucket column to HourStart's mapped column name (hour_start under snake_case).
    [TimeBucket("1 hour", nameof(PowerMeterReading.Timestamp))]
    public DateTime HourStart { get; set; }

    [Aggregate(EAggregateFunction.Avg, nameof(PowerMeterReading.PowerKw))]
    public double AvgPowerKw { get; set; }
}

public class PowerMeterReading
{
    public string MeterId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double PowerKw { get; set; }
}
```

> :warning: **Note:** The bucket column name is part of the view's structural definition. Moving `[TimeBucket]` onto a property whose mapped column differs from `time_bucket` on an **existing** aggregate changes that column name, which forces a drop and recreate of the aggregate (materialized data is rebuilt). In a hierarchy the drop cascades to every descendant aggregate. See [Migration Ordering](../fluent-api/continuous-aggregates#migration-ordering).

> :warning: **Note:** Class-level `[TimeBucket]` is unaffected: the bucket column stays `time_bucket`, byte-for-byte identical to earlier versions.

### Model Validation

Structured aggregates (those configured through attributes rather than a raw view definition) are validated at model finalization:

- Duplicate output column names are rejected with an `InvalidOperationException`. The check compares the bucket column, all `[GroupByColumn]` columns, and every `[Aggregate]` alias after resolving them to database column names. A source column that collides with the bucket column name — previously surfacing only at `migrate` time — is now caught at model build.
- A property designated by a property-level `[TimeBucket]` that does not exist on the entity raises an `InvalidOperationException` (this cannot occur through attributes alone, but the same check guards Fluent-configured models).

> :warning: **Note:** Entities scaffolded with a raw view definition are exempt from both checks, because the structured projection fields are unused on that path.

## Configuration Options

The `[ContinuousAggregate]` attribute provides several configuration properties:

### MaterializedViewName

The name of the materialized view created in the database (required):

```csharp
[ContinuousAggregate(MaterializedViewName = "hourly_metrics", ParentName = nameof(Metric))]
```

### ParentName

The name of the source hypertable entity (required):

```csharp
[ContinuousAggregate(MaterializedViewName = "trade_stats", ParentName = nameof(Trade))]
```

### ChunkInterval

The chunk interval for the continuous aggregate's underlying materialized hypertable. Defaults to 10 times the parent hypertable's chunk interval if not specified:

```csharp
[ContinuousAggregate(
    MaterializedViewName = "trade_stats",
    ParentName = nameof(Trade),
    ChunkInterval = "30 days")]
```

### WithNoData

By default, continuous aggregates are populated with data when created. Set to `true` to create an empty aggregate that will be populated on the first refresh:

```csharp
[ContinuousAggregate(
    MaterializedViewName = "trade_stats",
    ParentName = nameof(Trade),
    WithNoData = true)]
```

### CreateGroupIndexes

Controls whether indexes are automatically created on GROUP BY columns. Enabled by default:

```csharp
[ContinuousAggregate(
    MaterializedViewName = "trade_stats",
    ParentName = nameof(Trade),
    CreateGroupIndexes = true)]
```

### MaterializedOnly

By default, queries combine materialized data with recent unmaterialized data from the source hypertable. Set to `true` to return only pre-computed materialized data:

```csharp
[ContinuousAggregate(
    MaterializedViewName = "trade_stats",
    ParentName = nameof(Trade),
    MaterializedOnly = true)]
```

### Where

Apply filtering conditions to the source hypertable before aggregation:

```csharp
[ContinuousAggregate(
    MaterializedViewName = "valid_sensor_readings",
    ParentName = nameof(SensorReading),
    Where = "\"temperature\" > -50 AND \"humidity\" >= 0")]
```

> :warning: **Note:** The WHERE clause should be a valid SQL expression without the "WHERE" keyword. Use double quotes for column identifiers if needed.

## Complete Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;

// Source hypertable
[Hypertable(nameof(Timestamp), ChunkTimeInterval = "7 days")]
[PrimaryKey(nameof(Ticker), nameof(Timestamp))]
public class Trade
{
    public DateTime Timestamp { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Size { get; set; }
}

// Continuous aggregate with comprehensive configuration
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "trade_hourly_stats",
    ParentName = nameof(Trade),
    ChunkInterval = "30 days",
    WithNoData = false,
    CreateGroupIndexes = true,
    MaterializedOnly = false,
    Where = "\"price\" > 0 AND \"size\" > 0")]
[TimeBucket("1 hour", nameof(Trade.Timestamp), GroupBy = true)]
public class TradeHourlyAggregate
{
    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AveragePrice { get; set; }

    [Aggregate(EAggregateFunction.Max, nameof(Trade.Price))]
    public decimal MaxPrice { get; set; }

    [Aggregate(EAggregateFunction.Min, nameof(Trade.Price))]
    public decimal MinPrice { get; set; }

    [Aggregate(EAggregateFunction.Sum, nameof(Trade.Size))]
    public decimal TotalVolume { get; set; }

    [Aggregate(EAggregateFunction.Count, "*")]
    public long TradeCount { get; set; }

    [Aggregate(EAggregateFunction.First, nameof(Trade.Price))]
    public decimal OpeningPrice { get; set; }

    [Aggregate(EAggregateFunction.Last, nameof(Trade.Price))]
    public decimal ClosingPrice { get; set; }
}
```

## Hierarchical Continuous Aggregates

A continuous aggregate can aggregate from another continuous aggregate. The child sets `ParentName` to the parent aggregate entity and its `[TimeBucket]` source column references the parent's bucket column. The parent's bucket property must resolve to a known column so the child can reference it by name. Two equivalent options exist:

- Place `[TimeBucket]` on the parent's bucket property (see [Naming the Time-Bucket Column](#naming-the-time-bucket-column)). The designated name flows through resolution automatically: set the child's `[TimeBucket]` source column to that property's mapped column name (for example `hour_start`).
- Map the parent's bucket property to the default column with `[Column("time_bucket")]`, keeping a class-level `[TimeBucket]`, and set the child's source column to `"time_bucket"`.

The example below uses the second option; the property-level approach mirrors [Naming the Time-Bucket Column](#naming-the-time-bucket-column).

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

[Keyless]
[ContinuousAggregate(MaterializedViewName = "trade_hourly", ParentName = nameof(Trade))]
[TimeBucket("1 hour", nameof(Trade.Timestamp))]
public class TradeHourly
{
    [Column("time_bucket")]
    public DateTime TimeBucket { get; set; }

    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AvgPrice { get; set; }
}

[Keyless]
[ContinuousAggregate(MaterializedViewName = "trade_daily", ParentName = nameof(TradeHourly))]
[TimeBucket("1 day", "time_bucket")]
public class TradeDaily
{
    [Column("time_bucket")]
    public DateTime TimeBucket { get; set; }

    [Aggregate(EAggregateFunction.Avg, nameof(TradeHourly.AvgPrice))]
    public decimal AvgPrice { get; set; }
}
```

Migration ordering, descendant recreation, and scaffolding behave identically to the Fluent API. See [Hierarchical Continuous Aggregates](../fluent-api/continuous-aggregates#hierarchical-continuous-aggregates) for the shared behavior and the TimescaleDB server-side bucket-width constraints.

## Grouping by Additional Columns

Use the `[GroupByColumn]` attribute on a property of the aggregate entity to add it to the GROUP BY clause. Without an argument, the property's own name is used as the source column; pass a source column explicitly when the names differ:

```csharp
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "trade_hourly_stats",
    ParentName = nameof(Trade))]
[TimeBucket("1 hour", nameof(Trade.Timestamp))]
public class TradeHourlyAggregate
{
    // Grouped by the parent's Exchange column (same name as this property).
    [GroupByColumn]
    public string Exchange { get; set; } = string.Empty;

    // Grouped by the parent's Ticker column, exposed under a different property name.
    [GroupByColumn(nameof(Trade.Ticker))]
    public string Symbol { get; set; } = string.Empty;

    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AveragePrice { get; set; }
}
```

> :warning: **Note:** Raw SQL GROUP BY expressions (e.g. `EXTRACT(HOUR FROM time)`) cannot be expressed as an attribute. Use the [Fluent API](../fluent-api/continuous-aggregates#grouping-data)'s `AddGroupByColumn(string)` overload for those.

## Refresh Policies

Continuous aggregates can be configured with automatic refresh policies that run on a schedule to keep the materialized view up-to-date. The `[ContinuousAggregatePolicy]` attribute configures TimescaleDB's `add_continuous_aggregate_policy()` function, which automatically refreshes data within a specified time window.

[See also: add_continuous_aggregate_policy](https://docs.tigerdata.com/api/latest/continuous-aggregates/add_continuous_aggregate_policy/)

### Basic Refresh Policy Configuration

Apply the `[ContinuousAggregatePolicy]` attribute to a continuous aggregate entity to configure automatic refresh:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore;

[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "trade_hourly_stats",
    ParentName = nameof(Trade))]
[TimeBucket("1 hour", nameof(Trade.Timestamp))]
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour")]
public class TradeAggregate
{
    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AveragePrice { get; set; }
}

public class Trade
{
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public string Ticker { get; set; } = string.Empty;
}
```

### ContinuousAggregatePolicy Properties

The `[ContinuousAggregatePolicy]` attribute provides the following configuration properties:

#### StartOffset

Window start as an interval relative to execution time. NULL or empty string equals earliest data:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "1 month",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour")]
```

Valid values include interval strings like "1 month", "7 days", or integer strings for integer-based time columns like "100000".

#### EndOffset

Window end as an interval relative to execution time. NULL or empty string equals latest data:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour")]
```

Valid values include interval strings like "1 hour", "1 day", or integer strings for integer-based time columns like "1000".

#### ScheduleInterval

Interval between refresh executions in wall-clock time. Defaults to "24 hours" if not specified:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "30 minutes")]
```

Valid values include interval strings like "1 hour", "30 minutes", "24 hours".

#### InitialStart

The first time the policy job is scheduled to run. Specified as a UTC date-time string in ISO 8601 format. If not set, the first run is scheduled based on the schedule interval:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    InitialStart = "2026-02-01T00:00:00Z")]
```

#### IfNotExists

Issues a notice instead of an error if the policy job already exists. Defaults to false:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    IfNotExists = true)]
```

#### IncludeTieredData

Overrides tiered read settings for the refresh policy. NULL means use default behavior:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    IncludeTieredData = true)]
```

#### BucketsPerBatch

The number of time buckets processed per batch transaction. Defaults to 1, minimum value is 1:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    BucketsPerBatch = 10)]
```

#### MaxBatchesPerExecution

The maximum number of batches executed per run. 0 means unlimited. Defaults to 0:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    MaxBatchesPerExecution = 5)]
```

#### RefreshNewestFirst

The direction of incremental refresh. True refreshes newest data first, false refreshes oldest first. Defaults to true:

```csharp
[ContinuousAggregatePolicy(
    StartOffset = "7 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    RefreshNewestFirst = true)]
```

### Complete Refresh Policy Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Timestamp), ChunkTimeInterval = "7 days")]
[PrimaryKey(nameof(Ticker), nameof(Timestamp))]
public class Trade
{
    public DateTime Timestamp { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Size { get; set; }
}

[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "trade_hourly_stats",
    ParentName = nameof(Trade),
    ChunkInterval = "30 days",
    WithNoData = false,
    CreateGroupIndexes = true,
    MaterializedOnly = false,
    Where = "\"price\" > 0 AND \"size\" > 0")]
[TimeBucket("1 hour", nameof(Trade.Timestamp), GroupBy = true)]
[ContinuousAggregatePolicy(
    StartOffset = "30 days",
    EndOffset = "1 hour",
    ScheduleInterval = "1 hour",
    InitialStart = "2026-02-01T00:00:00Z",
    IfNotExists = true,
    BucketsPerBatch = 5,
    MaxBatchesPerExecution = 10,
    RefreshNewestFirst = true)]
public class TradeHourlyAggregate
{
    [Aggregate(EAggregateFunction.Avg, nameof(Trade.Price))]
    public decimal AveragePrice { get; set; }

    [Aggregate(EAggregateFunction.Max, nameof(Trade.Price))]
    public decimal MaxPrice { get; set; }

    [Aggregate(EAggregateFunction.Min, nameof(Trade.Price))]
    public decimal MinPrice { get; set; }

    [Aggregate(EAggregateFunction.Sum, nameof(Trade.Size))]
    public decimal TotalVolume { get; set; }

    [Aggregate(EAggregateFunction.Count, "*")]
    public long TradeCount { get; set; }
}
```

> :warning: **Note:** The refresh policy runs as a background job managed by TimescaleDB. Ensure the TimescaleDB background worker is enabled in your database configuration.

## Important Notes

- Continuous aggregate entities must be marked with `[Keyless]`.
- The source entity specified in `ParentName` must be a TimescaleDB hypertable.
- The time bucket width determines aggregation granularity (e.g., "1 hour", "1 day", "15 minutes").
- All aggregate properties must have the `[Aggregate]` attribute with appropriate function and source column.
- Use `nameof()` to reference source entity properties for type safety.
- Refresh policies can be configured to automatically keep the aggregate up-to-date.

## Common Use Cases

### Hourly Metrics Dashboard

Pre-compute hourly statistics for real-time dashboards without querying raw data:

```csharp
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "sensor_metrics_hourly",
    ParentName = nameof(SensorReading))]
[TimeBucket("1 hour", nameof(SensorReading.Timestamp))]
public class SensorMetricsHourly
{
    [Aggregate(EAggregateFunction.Avg, nameof(SensorReading.Temperature))]
    public double AvgTemperature { get; set; }

    [Aggregate(EAggregateFunction.Max, nameof(SensorReading.Temperature))]
    public double MaxTemperature { get; set; }

    [Aggregate(EAggregateFunction.Min, nameof(SensorReading.Temperature))]
    public double MinTemperature { get; set; }
}
```

### Daily Rollups

Create daily summaries for long-term trend analysis:

```csharp
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "orders_daily",
    ParentName = nameof(OrderEvent))]
[TimeBucket("1 day", nameof(OrderEvent.OrderDate))]
public class DailySummary
{
    [Aggregate(EAggregateFunction.Sum, nameof(OrderEvent.Amount))]
    public decimal TotalRevenue { get; set; }

    [Aggregate(EAggregateFunction.Count, nameof(OrderEvent.OrderId))]
    public long OrderCount { get; set; }
}
```

### Downsampling High-Frequency Data

Reduce storage and improve query performance for high-frequency sensor data:

```csharp
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "sensor_per_minute",
    ParentName = nameof(SensorReading))]
[TimeBucket("1 minute", nameof(SensorReading.Timestamp))]
public class SensorMinute
{
    [Aggregate(EAggregateFunction.First, nameof(SensorReading.Value))]
    public double FirstValue { get; set; }

    [Aggregate(EAggregateFunction.Last, nameof(SensorReading.Value))]
    public double LastValue { get; set; }

    [Aggregate(EAggregateFunction.Avg, nameof(SensorReading.Value))]
    public double AvgValue { get; set; }
}
```

### Weather Data Analysis

Track daily weather statistics with quality filtering:

```csharp
[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "weather_daily",
    ParentName = nameof(WeatherReading),
    Where = "\"temperature\" > -50 AND \"humidity\" >= 0 AND \"humidity\" <= 100")]
[TimeBucket("1 day", nameof(WeatherReading.Time))]
public class WeatherDaily
{
    [Aggregate(EAggregateFunction.Avg, nameof(WeatherReading.Temperature))]
    public double AvgTemperature { get; set; }

    [Aggregate(EAggregateFunction.Max, nameof(WeatherReading.Temperature))]
    public double MaxTemperature { get; set; }

    [Aggregate(EAggregateFunction.Min, nameof(WeatherReading.Temperature))]
    public double MinTemperature { get; set; }

    [Aggregate(EAggregateFunction.Avg, nameof(WeatherReading.Humidity))]
    public double AvgHumidity { get; set; }

    [Aggregate(EAggregateFunction.Count, "*")]
    public long ReadingCount { get; set; }
}
```

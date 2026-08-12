# Compression Policies

A compression policy schedules automatic conversion of uncompressed hypertable or continuous aggregate chunks to the columnstore format. The policy runs as a background job and compresses chunks that fall outside the configured age window.

Each hypertable or continuous aggregate supports at most one compression policy. Compression must be enabled on the target table before a compression policy can be created.

[See also: add_columnstore_policy](https://www.tigerdata.com/docs/learn/columnar-storage/compression-methods)

## Prerequisites

Compression must be enabled on the hypertable before applying `[CompressionPolicy]`. Set `EnableCompression = true`, `CompressionSegmentBy`, or `CompressionOrderBy` on `[Hypertable]` first:

```csharp
[Hypertable(nameof(Time), CompressionSegmentBy = new[] { "DeviceId" })]
[CompressionPolicy("7 days")]
public class DeviceReading { ... }
```

For continuous aggregates, set `EnableCompression = true` on `[ContinuousAggregate]`. The convention validates this at model finalization and raises an `InvalidOperationException` if compression is not configured.

## Compress Modes

Two mutually exclusive compress modes are available:

- **`After`**: Compresses chunks whose data is older than the specified interval. This is the standard mode.
- **`CreatedBefore`**: Compresses chunks created more than the specified interval ago, regardless of the data they contain.

Exactly one of `After` or `CreatedBefore` must be set. Providing both or neither raises an exception.

## Interval Values

All interval parameters are strings passed verbatim to PostgreSQL, so every format the server accepts in raw SQL works here too:

- **PostgreSQL interval syntax**: `"7 days"`, `"24:00:00"`, `"1 month"`, `"2 weeks 3 days"`
- **ISO 8601 durations**: `"P7D"`, `"PT12H"`
- **Plain numbers** for hypertables with integer time columns (`bigint`, `int`): an `After` value that parses as an integer is emitted as `{value}::bigint` in the column's own unit instead of an `INTERVAL` literal. `CreatedBefore` is always a wall-clock interval based on chunk creation time and never takes a number.

## Attribute Constructor Forms

The `[CompressionPolicy]` attribute supports three usage forms:

### Positional `after` string

The most common form. Passes the compress-after interval as the first positional argument:

```csharp
[CompressionPolicy("7 days")]
```

This constructor validates that the argument is not null or whitespace and sets `After`.

### Named `createdBefore`

Use named argument syntax to configure by chunk creation time:

```csharp
[CompressionPolicy(createdBefore: "30 days")]
```

The two-argument constructor `(string? after = null, string? createdBefore = null)` validates mutual exclusivity: providing both or neither raises an `InvalidOperationException` at attribute construction time.

### Property initializer

All properties can be set via object initializer syntax. Mutual exclusivity of `After` and `CreatedBefore` is validated by the convention at model-build time rather than at attribute construction time, since the parameterless constructor cannot enforce it:

```csharp
[CompressionPolicy(After = "7 days", ScheduleInterval = "12 hours")]
```

## Validation Timing

| Form | Validation point |
|------|-----------------|
| `[CompressionPolicy("7 days")]` | At attribute construction (argument must be non-empty) |
| `[CompressionPolicy(after: "7 days", createdBefore: "30 days")]` | At attribute construction (mutual exclusivity) |
| `[CompressionPolicy(After = "7 days")]` | At model build via convention |

## Basic Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time), ChunkTimeInterval = "1 day", CompressionSegmentBy = new[] { "DeviceId" })]
[PrimaryKey(nameof(Id), nameof(Time))]
[CompressionPolicy("7 days")]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
}
```

## Using `CreatedBefore`

```csharp
[Hypertable(nameof(Time), CompressionSegmentBy = new[] { "DeviceId" })]
[PrimaryKey(nameof(Id), nameof(Time))]
[CompressionPolicy(createdBefore: "30 days")]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
}
```

## Complete Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time),
    ChunkTimeInterval = "1 day",
    CompressionSegmentBy = new[] { "DeviceId" },
    CompressionOrderBy = new[] { "Time DESC" })]
[PrimaryKey(nameof(Id), nameof(Time))]
[CompressionPolicy("7 days",
    ScheduleInterval = "12 hours",
    InitialStart = "2025-10-01T02:00:00Z",
    Timezone = "Europe/Berlin",
    IfNotExists = true)]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
}
```

## Compression Policy on Continuous Aggregates

Continuous aggregates support `[CompressionPolicy]` in the same way as hypertables. The `[ContinuousAggregate]` attribute must have `EnableCompression = true` (or `CompressionSegmentBy`/`CompressionOrderBy` set) before the policy can be applied.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;

[Keyless]
[ContinuousAggregate(
    MaterializedViewName = "hourly_trade_stats",
    ParentName = nameof(Trade),
    EnableCompression = true,
    CompressionOrderBy = new[] { "Bucket DESC" })]
[TimeBucket("1 hour", nameof(Trade.Time))]
[CompressionPolicy("30 days")]
public class HourlyTradeStat
{
    public DateTime Bucket { get; set; }
    public decimal AveragePrice { get; set; }
    public string Ticker { get; set; } = string.Empty;
}
```

## SQL Generation

By default, the library emits the TimescaleDB 2.18+ columnstore API (`add_columnstore_policy`). To use legacy function names, configure via `UseTimescaleDb(o => o.UseLegacyCompressionSql())`. See the [Fluent API Compression Policies](../fluent-api/compression-policies.md) page for details.

## Supported Properties

| Property           | Description                                                                                                     | Type      | Database Type | Default                                           |
| ------------------ | --------------------------------------------------------------------------------------------------------------- | --------- | ------------- | ------------------------------------------------- |
| `After`            | Interval after which chunks are compressed. Mutually exclusive with `CreatedBefore`.                            | `string?` | `INTERVAL`    | —                                                 |
| `CreatedBefore`    | Interval relative to chunk creation time. Mutually exclusive with `After`.                                      | `string?` | `INTERVAL`    | —                                                 |
| `ScheduleInterval` | Interval between policy job executions.                                                                         | `string?` | `INTERVAL`    | 12 hours (or half the chunk interval for sub-day) |
| `InitialStart`     | First scheduled run of the policy job, as a UTC date-time string in ISO 8601 format.                            | `string?` | `TIMESTAMPTZ` | `null`                                            |
| `Timezone`         | PostgreSQL time zone used when computing the initial start time (e.g., `"Europe/Berlin"`).                      | `string?` | —             | `null`                                            |
| `IfNotExists`      | When `true`, no error is raised if the policy already exists.                                                   | `bool`    | —             | `false`                                           |

# Compression Policies

A compression policy schedules automatic conversion of uncompressed hypertable or continuous aggregate chunks to the columnstore format. The policy runs as a background job and compresses chunks that fall outside the configured age window.

Each hypertable or continuous aggregate supports at most one compression policy. Compression must be enabled on the target table before a compression policy can be created.

[See also: add_columnstore_policy](https://www.tigerdata.com/docs/learn/columnar-storage/compression-methods)

## Prerequisites

Compression must be enabled on the hypertable before configuring a compression policy. Use `.EnableCompression()`, `.WithCompressionSegmentBy()`, or `.WithCompressionOrderBy()` first:

```csharp
builder.IsHypertable(x => x.Time)
       .WithCompressionSegmentBy(x => x.DeviceId)
       .WithCompressionOrderBy(OrderByBuilder.For<DeviceReading>(x => x.Time).Descending());

builder.WithCompressionPolicy(
    after: "7 days",
    scheduleInterval: "12 hours");
```


## Compress Modes

Two mutually exclusive compress modes are available:

- **`after`**: Compresses chunks whose data is older than the specified interval. This is the standard mode.
- **`createdBefore`**: Compresses chunks created more than the specified interval ago, regardless of the data they contain.

Exactly one of `after` or `createdBefore` must be specified. Providing both or neither raises an exception at configuration time.

## SQL Generation

By default, the library emits the TimescaleDB 2.18+ columnstore API:

```sql
CALL add_columnstore_policy('readings', after => INTERVAL '7 days');
```

To use the legacy function names (`add_compression_policy`, `remove_compression_policy`), opt in via:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseTimescaleDb(o => o.UseLegacyCompressionSql()));
```

With the legacy option enabled, the generated SQL uses:

```sql
SELECT add_compression_policy('readings', compress_after => INTERVAL '7 days');
```

## Interval Values

All interval parameters are strings passed verbatim to PostgreSQL, so every format the server accepts in raw SQL works here too:

- **PostgreSQL interval syntax**: `"7 days"`, `"24:00:00"`, `"1 month"`, `"2 weeks 3 days"`
- **ISO 8601 durations**: `"P7D"`, `"PT12H"` — NodaTime users can pass `Period.FromDays(7).ToString()` directly, since `Period`'s default format is ISO 8601
- **Plain numbers** for hypertables with integer time columns (`bigint`, `int`): an `after` value that parses as an integer is emitted as `{value}::bigint` in the column's own unit instead of an `INTERVAL` literal. `createdBefore` is always a wall-clock interval based on chunk creation time and never takes a number.

## Using `createdBefore`

```csharp
builder.WithCompressionPolicy(createdBefore: "30 days");
```

## Complete Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DeviceReadingConfiguration : IEntityTypeConfiguration<DeviceReading>
{
    public void Configure(EntityTypeBuilder<DeviceReading> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day")
               .WithCompressionSegmentBy(x => x.DeviceId)
               .WithCompressionOrderBy(
                   OrderByBuilder.For<DeviceReading>(x => x.Time).Descending());

        builder.WithCompressionPolicy(
            after: "7 days",
            scheduleInterval: "12 hours",
            initialStart: new DateTime(2025, 10, 1, 2, 0, 0, DateTimeKind.Utc),
            timezone: "Europe/Berlin",
            ifNotExists: true);
    }
}

public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
}
```

## Chaining with Other Policies

`WithCompressionPolicy` returns `EntityTypeBuilder<TEntity>`, allowing further method chaining. When used after a scaffold-generated string-based builder (e.g., `WithRetentionPolicy` returning a `RetentionPolicyStringBuilder`), a dedicated overload accepts the builder as the receiver:

```csharp
builder.WithRetentionPolicy(dropAfter: "90 days", scheduleInterval: "1 day", timezone: null, ifNotExists: null)
       .WithCompressionPolicy(after: "7 days", createdBefore: null, scheduleInterval: null, timezone: null, ifNotExists: null)
       .WithInitialStart(new DateTime(2025, 10, 1, 3, 0, 0, DateTimeKind.Utc));
```

> :warning: **Note:** The chained overload that accepts a `RetentionPolicyStringBuilder` or `ContinuousAggregateStringBuilder` as the receiver returns a `CompressionPolicyStringBuilder<TEntity>` with a `.WithInitialStart(DateTime)` method. This is required because attributes and scaffold generators cannot represent `DateTime` as a literal constructor argument.

## Compression Policy on Continuous Aggregates

Continuous aggregates also support compression policies. The continuous aggregate must have compression enabled before adding a compression policy. The convention validates this at model finalization and raises an `InvalidOperationException` if compression is not configured.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class HourlyTradeStatConfiguration : IEntityTypeConfiguration<HourlyTradeStat>
{
    public void Configure(EntityTypeBuilder<HourlyTradeStat> builder)
    {
        builder.HasNoKey();

        builder.IsContinuousAggregate<HourlyTradeStat, Trade>(
                "hourly_trade_stats",
                "1 hour",
                x => x.Time)
            .WithCompression()
            .WithCompressionSegmentBy(x => x.Ticker);

        builder.WithCompressionPolicy(after: "30 days");
    }
}
```

## Supported Parameters

| Parameter          | Description                                                                                                     | Type        | Database Type | Default                                           |
| ------------------ | --------------------------------------------------------------------------------------------------------------- | ----------- | ------------- | ------------------------------------------------- |
| `after`            | Interval after which chunks are compressed. Mutually exclusive with `createdBefore`.                            | `string?`   | `INTERVAL`    | —                                                 |
| `createdBefore`    | Interval relative to chunk creation time. Mutually exclusive with `after`.                                      | `string?`   | `INTERVAL`    | —                                                 |
| `scheduleInterval` | Interval between policy job executions.                                                                         | `string?`   | `INTERVAL`    | 12 hours (or half the chunk interval for sub-day) |
| `initialStart`     | First scheduled run of the policy job, as a UTC `DateTime`. When `null`, derived from `scheduleInterval`.       | `DateTime?` | `TIMESTAMPTZ` | `null`                                            |
| `timezone`         | PostgreSQL time zone used when computing the initial start time (e.g., `"Europe/Berlin"`).                      | `string?`   | —             | `null`                                            |
| `ifNotExists`      | When `true`, no error is raised if the policy already exists.                                                   | `bool?`     | —             | `false`                                           |

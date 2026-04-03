# Retention Policies

A retention policy automatically drops old chunks from a hypertable or continuous aggregate on a scheduled basis. This keeps storage consumption bounded without requiring manual intervention and is the standard approach for managing time-series data lifecycle in TimescaleDB.

Each hypertable or continuous aggregate supports at most one retention policy.

[See also: add_retention_policy](https://docs.tigerdata.com/api/latest/data_retention/add_retention_policy/)

## Drop Modes

Two mutually exclusive drop modes are available:

- **`dropAfter`**: Drops chunks whose data falls outside a time window relative to the current time. This is the standard mode.
- **`dropCreatedBefore`**: Drops chunks created before a specified interval ago, regardless of the data they contain.

Exactly one of `dropAfter` or `dropCreatedBefore` must be specified. Providing both or neither raises an exception.

## Basic Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ApplicationLogConfiguration : IEntityTypeConfiguration<ApplicationLog>
{
    public void Configure(EntityTypeBuilder<ApplicationLog> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day");

        // Drop chunks older than 30 days, running the job daily
        builder.WithRetentionPolicy(
            dropAfter: "30 days",
            scheduleInterval: "1 day");
    }
}
```

## Using `dropCreatedBefore`

```csharp
public class ApiRequestLogConfiguration : IEntityTypeConfiguration<ApiRequestLog>
{
    public void Configure(EntityTypeBuilder<ApiRequestLog> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day");

        // Drop chunks created more than 30 days ago
        builder.WithRetentionPolicy(
            dropCreatedBefore: "30 days",
            scheduleInterval: "1 day");
    }
}
```

> :warning: **Note:** Due to a known bug in TimescaleDB ([#9446](https://github.com/timescale/timescaledb/issues/9446)), `alter_job` fails when used with `drop_created_before` policies. The library works around this by skipping the `alter_job` call for `drop_created_before` policies. As a result, job scheduling parameters (`scheduleInterval`, `maxRuntime`, `maxRetries`, `retryPeriod`) are accepted by the API but have no effect at the database level when `dropCreatedBefore` is used.

## Complete Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ApplicationLogConfiguration : IEntityTypeConfiguration<ApplicationLog>
{
    public void Configure(EntityTypeBuilder<ApplicationLog> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day");

        builder.WithRetentionPolicy(
            dropAfter: "30 days",
            initialStart: new DateTime(2025, 10, 1, 3, 0, 0, DateTimeKind.Utc),
            scheduleInterval: "1 day",
            maxRuntime: "30 minutes",
            maxRetries: 3,
            retryPeriod: "5 minutes");
    }
}

public class ApplicationLog
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
```

## Supported Parameters

| Parameter           | Description                                                                                                                                                                                      | Type        | Database Type | Default Value                               |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------- | ------------- | ------------------------------------------- |
| `dropAfter`         | The interval after which chunks are dropped. Mutually exclusive with `dropCreatedBefore`.                                                                                                        | `string?`   | `INTERVAL`    | —                                           |
| `dropCreatedBefore` | The interval before which chunks created are dropped. Based on chunk creation time. Only supports `INTERVAL`. Not available for integer-based time columns. Mutually exclusive with `dropAfter`. | `string?`   | `INTERVAL`    | —                                           |
| `initialStart`      | The first time the policy job is scheduled to run, as a UTC `DateTime`. If `null`, the first run is based on the `scheduleInterval`.                                                             | `DateTime?` | `TIMESTAMPTZ` | `null`                                      |
| `scheduleInterval`  | The interval at which the retention policy job runs.                                                                                                                                             | `string?`   | `INTERVAL`    | `'1 day'`                                   |
| `maxRuntime`        | The maximum amount of time the job is allowed to run before being stopped. If `null`, there is no time limit.                                                                                    | `string?`   | `INTERVAL`    | `'00:00:00'`                                |
| `maxRetries`        | The number of times the job is retried if it fails.                                                                                                                                              | `int?`      | `INTEGER`     | `-1`                                        |
| `retryPeriod`       | The amount of time the scheduler waits between retries of a failed job.                                                                                                                          | `string?`   | `INTERVAL`    | Equal to the `scheduleInterval` by default. |

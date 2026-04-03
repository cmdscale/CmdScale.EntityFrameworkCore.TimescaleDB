# Retention Policies

A retention policy automatically drops old chunks from a hypertable or continuous aggregate on a scheduled basis. This keeps storage consumption bounded without requiring manual intervention and is the standard approach for managing time-series data lifecycle in TimescaleDB.

Each hypertable or continuous aggregate supports at most one retention policy.

[See also: add_retention_policy](https://docs.tigerdata.com/api/latest/data_retention/add_retention_policy/)

## Drop Modes

Two mutually exclusive drop modes are available:

- **`DropAfter`**: Drops chunks whose data falls outside a time window relative to the current time. This is the standard mode.
- **`DropCreatedBefore`**: Drops chunks created before a specified interval ago, regardless of the data they contain.

Exactly one of `DropAfter` or `DropCreatedBefore` must be specified. Providing both or neither raises an exception.

## Basic Example

Here is a complete example of configuring a retention policy on an `ApplicationLog` hypertable using `DropAfter`.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time), ChunkTimeInterval = "1 day")]
[PrimaryKey(nameof(Id), nameof(Time))]
[RetentionPolicy("30 days")]
public class ApplicationLog
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
```

## Using `DropCreatedBefore`

Pass `null` as the first argument and provide `dropCreatedBefore` as a named argument:

```csharp
[Hypertable(nameof(Time), ChunkTimeInterval = "1 day")]
[PrimaryKey(nameof(Id), nameof(Time))]
[RetentionPolicy(dropCreatedBefore: "30 days")]
public class ApiRequestLog
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
}
```

> :warning: **Note:** Due to a known bug in TimescaleDB ([#9446](https://github.com/timescale/timescaledb/issues/9446)), `alter_job` fails when used with `DropCreatedBefore` policies. The library works around this by skipping the `alter_job` call for `DropCreatedBefore` policies. As a result, job scheduling parameters (`ScheduleInterval`, `MaxRuntime`, `MaxRetries`, `RetryPeriod`) are accepted by the API but have no effect at the database level when `DropCreatedBefore` is used.

## Complete Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time), ChunkTimeInterval = "1 day")]
[PrimaryKey(nameof(Id), nameof(Time))]
[RetentionPolicy("30 days",
    InitialStart = "2025-10-01T03:00:00Z",
    ScheduleInterval = "1 day",
    MaxRuntime = "30 minutes",
    MaxRetries = 3,
    RetryPeriod = "5 minutes")]
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

| Parameter           | Description                                                                                                                                                                                      | Type      | Database Type | Default Value                               |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------- | ------------- | ------------------------------------------- |
| `DropAfter`         | The interval after which chunks are dropped. Mutually exclusive with `DropCreatedBefore`. Can be passed as the first positional argument.                                                        | `string?` | `INTERVAL`    | —                                           |
| `DropCreatedBefore` | The interval before which chunks created are dropped. Based on chunk creation time. Only supports `INTERVAL`. Not available for integer-based time columns. Mutually exclusive with `DropAfter`. | `string?` | `INTERVAL`    | —                                           |
| `InitialStart`      | The first time the policy job is scheduled to run, specified as a UTC date-time string in ISO 8601 format. If `null`, the first run is based on the `ScheduleInterval`.                          | `string?` | `TIMESTAMPTZ` | `null`                                      |
| `ScheduleInterval`  | The interval at which the retention policy job runs.                                                                                                                                             | `string?` | `INTERVAL`    | `'1 day'`                                   |
| `MaxRuntime`        | The maximum amount of time the job is allowed to run before being stopped. If `null`, there is no time limit.                                                                                    | `string?` | `INTERVAL`    | `'00:00:00'`                                |
| `MaxRetries`        | The number of times the job is retried if it fails.                                                                                                                                              | `int`     | `INTEGER`     | `-1`                                        |
| `RetryPeriod`       | The amount of time the scheduler waits between retries of a failed job.                                                                                                                          | `string?` | `INTERVAL`    | Equal to the `scheduleInterval` by default. |

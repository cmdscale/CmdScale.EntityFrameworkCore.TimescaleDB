# NodaTime Support

This library supports [NodaTime](https://nodatime.org/) time-column types for both hypertables and continuous aggregates The fluent API methods `IsHypertable` and `IsContinuousAggregate`, as well as the `[Hypertable]` data annotation, accept any .NET time-column type. Correctness is enforced at model finalization by validating the resolved PostgreSQL store type of the time column rather than the .NET type, so a NodaTime type is valid precisely when its Npgsql mapping resolves to a supported TimescaleDB time dimension.

---

## Setup

The core library takes no dependency on NodaTime. NodaTime support is opt-in and provided by the official Npgsql plugin.

### Install the NodaTime Plugin Package

Add the [Npgsql NodaTime plugin](https://www.npgsql.org/doc/types/nodatime.html?tabs=datasource) to the project alongside the TimescaleDB packages:

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime
```

### Enable the Plugin

Enable the NodaTime plugin when configuring the PostgreSQL provider, then chain `.UseTimescaleDb()`:

```csharp
string? connectionString = builder.Configuration.GetConnectionString("Timescale");

builder.Services.AddDbContext<TimescaleContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseNodaTime()).UseTimescaleDb());
```

Once the plugin is enabled, NodaTime types such as `Instant` and `LocalDateTime` map to PostgreSQL time types and can be used as hypertable or continuous-aggregate time columns.

---

## Using the Fluent API

A NodaTime time column requires no special configuration. Overload resolution selects the generic `IsHypertable` overload automatically, so no explicit type arguments are needed; `builder.IsHypertable(x => x.RecordedAt)` works directly when `RecordedAt` is a NodaTime `Instant`.

```csharp
using NodaTime;

public class SensorMeasurement
{
    public Guid Id { get; set; }
    public Instant RecordedAt { get; set; }
    public string SensorId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
}
```

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SensorMeasurementConfiguration : IEntityTypeConfiguration<SensorMeasurement>
{
    public void Configure(EntityTypeBuilder<SensorMeasurement> builder)
    {
        builder.ToTable("sensor_measurements");
        builder.HasKey(x => new { x.Id, x.RecordedAt });

        builder.IsHypertable(x => x.RecordedAt)
               .WithChunkTimeInterval("1 day")
               .EnableCompression()
               .WithCompressionSegmentBy(x => x.SensorId)
               .WithCompressionOrderBy(s => [s.ByDescending(x => x.RecordedAt)]);
    }
}
```

The same applies to continuous aggregates: a NodaTime source time column works with `IsContinuousAggregate` without explicit type arguments.

---

## Using Data Annotations

The `[Hypertable]` attribute names the time column and works with any time column, including NodaTime types. The example below uses a NodaTime `LocalDateTime`

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using NodaTime;

[Hypertable(nameof(ObservedAt), ChunkTimeInterval = "1 day", EnableCompression = true, CompressionSegmentBy = new[] { "Station" }, CompressionOrderBy = new[] { "ObservedAt DESC" })]
[PrimaryKey(nameof(Id), nameof(ObservedAt))]
public class EnvironmentReading
{
    public Guid Id { get; set; }
    public LocalDateTime ObservedAt { get; set; }
    public string Station { get; set; } = string.Empty;
    public double Pressure { get; set; }
    public double WindSpeed { get; set; }
}
```

---

## Supported and Unsupported NodaTime Types

A NodaTime type is supported when its Npgsql store mapping resolves to a valid TimescaleDB time dimension. The supported PostgreSQL time-dimension store types are `timestamp without time zone` / `timestamp`, `timestamp with time zone` / `timestamptz`, `date`, `smallint` / `int2`, `integer` / `int` / `int4`, and `bigint` / `int8`.

### Supported

| NodaTime Type    | PostgreSQL Store Type            |
| ---------------- | -------------------------------- |
| `Instant`        | `timestamp with time zone`       |
| `LocalDateTime`  | `timestamp without time zone`    |
| `LocalDate`      | `date`                           |
| `ZonedDateTime`  | `timestamp with time zone`       |
| `OffsetDateTime` | `timestamp with time zone`       |

### Unsupported

The following NodaTime types do not map to a valid time dimension. Using one as a time column causes model building to throw:

| NodaTime Type  | PostgreSQL Store Type      |
| -------------- | -------------------------- |
| `LocalTime`    | `time without time zone`   |
| `OffsetTime`   | `time with time zone`      |
| `Duration`     | `interval`                 |
| `Period`       | `interval`                 |
| `Interval`     | `tstzrange`                |
| `DateInterval` | `daterange`                |

> :warning: **Note:** Validation is performed against the resolved PostgreSQL store type of the time column at model finalization, not against the .NET type. 
A NodaTime type is valid precisely because its Npgsql mapping resolves to a supported store type. When the resolved store type is not a valid TimescaleDB time dimension, model building throws an `InvalidOperationException`.
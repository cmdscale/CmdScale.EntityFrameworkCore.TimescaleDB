# TimeBucket

The `time_bucket` function in TimescaleDB partitions time-series rows into fixed-width intervals, enabling efficient `GROUP BY`, `SELECT`, and `ORDER BY` operations over time. The `CmdScale.EntityFrameworkCore.TimescaleDB` package translates `EF.Functions.TimeBucket(...)` LINQ calls directly to `time_bucket(...)` SQL, so no raw SQL strings are required in application queries.

[See also: time_bucket](https://docs.timescale.com/api/latest/hyperfunctions/time_bucket/)

## Available Overloads

The following overloads are available on `EF.Functions`. Each maps to the corresponding `time_bucket` SQL signature.

| C# Overload | SQL Translation |
|---|---|
| `TimeBucket(TimeSpan bucket, DateTime timestamp)` | `time_bucket(interval, timestamp)` |
| `TimeBucket(TimeSpan bucket, DateTimeOffset timestamp)` | `time_bucket(interval, timestamptz)` |
| `TimeBucket(TimeSpan bucket, DateOnly date)` | `time_bucket(interval, date)` |
| `TimeBucket(TimeSpan bucket, DateTime timestamp, TimeSpan offset)` | `time_bucket(interval, timestamp, offset)` |
| `TimeBucket(TimeSpan bucket, DateTimeOffset timestamp, TimeSpan offset)` | `time_bucket(interval, timestamptz, offset)` |
| `TimeBucket(TimeSpan bucket, DateTimeOffset timestamp, string timezone)` | `time_bucket(interval, timestamptz, timezone)` |
| `TimeBucket(int bucket, int value)` | `time_bucket(integer, integer)` |
| `TimeBucket(int bucket, int value, int offset)` | `time_bucket(integer, integer, offset)` |
| `TimeBucket(long bucket, long value)` | `time_bucket(bigint, bigint)` |
| `TimeBucket(long bucket, long value, long offset)` | `time_bucket(bigint, bigint, offset)` |

## Usage Patterns

### SELECT Projection

Project each row into its bucket boundary:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

List<DateTime> buckets = await context.Metrics
    .Select(m => EF.Functions.TimeBucket(TimeSpan.FromMinutes(5), m.Timestamp))
    .Distinct()
    .ToListAsync();
```

### GROUP BY with Aggregation

Group rows into time buckets and compute aggregate values:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

var results = await context.Metrics
    .GroupBy(m => EF.Functions.TimeBucket(TimeSpan.FromMinutes(5), m.Timestamp))
    .Select(g => new
    {
        Bucket = g.Key,
        Total = g.Sum(m => m.Value),
        Count = g.Count()
    })
    .OrderBy(r => r.Bucket)
    .ToListAsync();
```

### WHERE Filtering

Filter rows based on their computed bucket:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

TimeSpan bucket = TimeSpan.FromHours(1);
DateTime threshold = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

List<Metric> recent = await context.Metrics
    .Where(m => EF.Functions.TimeBucket(bucket, m.Timestamp) >= threshold)
    .ToListAsync();
```

### ORDER BY

Sort rows by their bucket boundary:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

List<Metric> ordered = await context.Metrics
    .OrderBy(m => EF.Functions.TimeBucket(TimeSpan.FromMinutes(5), m.Timestamp))
    .ToListAsync();
```

### With Offset

Shift bucket boundaries by a fixed duration. Useful when the natural bucket origin (midnight UTC) does not align with business hours:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

// Buckets start at :01, :06, :11, ... instead of :00, :05, :10, ...
var results = await context.Metrics
    .Select(m => EF.Functions.TimeBucket(
        TimeSpan.FromMinutes(5),
        m.Timestamp,
        TimeSpan.FromMinutes(1)))
    .Distinct()
    .ToListAsync();
```

### With Timezone

For `DateTimeOffset` columns, specify a timezone name to align bucket boundaries to local time zone rules, including daylight saving time transitions:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

var results = await context.Events
    .GroupBy(e => EF.Functions.TimeBucket(
        TimeSpan.FromHours(1),
        e.Timestamp,
        "Europe/Berlin"))
    .Select(g => new
    {
        Bucket = g.Key,
        Count = g.Count()
    })
    .ToListAsync();
```

### Integer Bucketing

For hypertables using integer or bigint time columns, bucket by a fixed numeric width:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

// Group sequence numbers into buckets of 5
var results = await context.Metrics
    .GroupBy(m => EF.Functions.TimeBucket(5, m.SequenceNumber))
    .Select(g => new
    {
        Bucket = g.Key,
        Count = g.Count()
    })
    .OrderBy(r => r.Bucket)
    .ToListAsync();
```

The `long` variants work identically for `bigint` columns:

```csharp
var results = await context.Metrics
    .GroupBy(m => EF.Functions.TimeBucket(1000L, m.EpochMilliseconds))
    .Select(g => new { Bucket = g.Key, Count = g.Count() })
    .ToListAsync();
```

## Complete Example

The following example demonstrates a complete setup including entity model, `DbContext` configuration, and a GROUP BY aggregation query using `EF.Functions.TimeBucket`.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

// Entity model
public class SensorReading
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string SensorId { get; set; } = string.Empty;
    public double Temperature { get; set; }
}

// DbContext
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorReading>(entity =>
        {
            entity.HasKey(x => new { x.Id, x.Timestamp });
            entity.IsHypertable(x => x.Timestamp)
                  .WithChunkTimeInterval("1 day");
        });
    }
}

// Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString).UseTimescaleDb());

// Query: hourly averages per sensor over the last 24 hours
DateTime since = DateTime.UtcNow.AddHours(-24);

var hourlyAverages = await context.SensorReadings
    .Where(r => r.Timestamp >= since)
    .GroupBy(r => new
    {
        Bucket = EF.Functions.TimeBucket(TimeSpan.FromHours(1), r.Timestamp),
        r.SensorId
    })
    .Select(g => new
    {
        g.Key.Bucket,
        g.Key.SensorId,
        AvgTemperature = g.Average(r => r.Temperature),
        ReadingCount = g.Count()
    })
    .OrderBy(r => r.Bucket)
    .ThenBy(r => r.SensorId)
    .ToListAsync();
```

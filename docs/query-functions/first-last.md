# First and Last

The `first` and `last` aggregates in TimescaleDB return the value of a column at the smallest or largest value of another column, typically the time column. They answer questions like "what was the opening and closing price of each day" without a window function or a sub-query. The `CmdScale.EntityFrameworkCore.TimescaleDB` package translates `EF.Functions.First(...)` and `EF.Functions.Last(...)` LINQ calls inside a `GroupBy` directly to `first(value, time)` and `last(value, time)` SQL.

[See also: first](https://docs.timescale.com/api/latest/hyperfunctions/first/) and [last](https://docs.timescale.com/api/latest/hyperfunctions/last/)

## Available Overloads

Both methods are generic over the value type and the ordering type, and are available on `EF.Functions`.

| C# Method | SQL Translation |
|---|---|
| `First<TValue, TTime>(IEnumerable<(TValue Value, TTime Time)> values)` | `first(value, time)` |
| `Last<TValue, TTime>(IEnumerable<(TValue Value, TTime Time)> values)` | `last(value, time)` |

The value and the ordering column are passed together as a `ValueTuple.Create(value, time)` pair, projected from the group:

```csharp
EF.Functions.First(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp)))
```

The result has the type of the value. For a nullable column use a nullable value type (for example `double?`): the result is `null` when the value of the first or last row is `null`, and for a group whose values are all `null`. A `Where` on the group that matches no row of a group also gives `null`, so use a nullable result type (for example `double?`) together with `Where`; with a non-nullable type such as `double`, materializing that group throws.

Any value type with a database mapping is supported. The ordering type can be any type PostgreSQL can order, such as `DateTime`, `DateTimeOffset`, `int` or `long`.

## Usage Patterns

### GROUP BY

Open and close values per sensor:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

var results = await context.SensorReadings
    .GroupBy(r => r.SensorId)
    .Select(g => new
    {
        SensorId = g.Key,
        Opening = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Temperature, r.Timestamp))),
        Closing = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Temperature, r.Timestamp)))
    })
    .ToListAsync();
```

### With TimeBucket

Combine with [TimeBucket](time-bucket.md) to get the first and last value per interval:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

var results = await context.SensorReadings
    .GroupBy(r => EF.Functions.TimeBucket(TimeSpan.FromHours(1), r.Timestamp))
    .Select(g => new
    {
        Bucket = g.Key,
        Opening = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Temperature, r.Timestamp))),
        Closing = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Temperature, r.Timestamp))),
        Count = g.Count()
    })
    .OrderBy(r => r.Bucket)
    .ToListAsync();
```

### With a Filter

A `Where` on the group becomes a `FILTER (WHERE ...)` clause on the aggregate, so rows can be excluded from `first` and `last` without affecting other aggregates in the same query:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

var results = await context.SensorReadings
    .GroupBy(r => r.SensorId)
    .Select(g => new
    {
        SensorId = g.Key,
        FirstHot = EF.Functions.First(g.Where(r => r.Temperature > 30).Select(r => ValueTuple.Create(r.Temperature, r.Timestamp))),
        Count = g.Count()
    })
    .ToListAsync();
```

## Unsupported Shapes

`first` and `last` are defined by the ordering column, so `Distinct()` and `OrderBy(...)` inside the grouping have no meaning for them. The translator refuses them with an `InvalidOperationException` ("`EF.Functions.First does not support Distinct or OrderBy inside the grouping; the time argument already orders the rows.`") while the query is compiled, before anything is sent to the database, instead of silently ignoring the call. On an entity with a primary key EF Core drops a plain `Distinct()` as a no-op before the translator sees it, so it has no effect there. The same applies to a selector that is not a `ValueTuple.Create(value, time)` pair.

The only valid source is the grouping of a `GroupBy` (`g` in the examples above). Calling the methods on any other sequence, or on the client, is not translated and throws an `InvalidOperationException`.

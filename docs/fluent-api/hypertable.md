# Hypertables

Hypertables are the core feature of TimescaleDB, automatically partitioning your data by time and other dimensions into smaller, more manageable child tables called "chunks." This architecture is the key to achieving fast ingest rates and query performance on large time-series datasets.

## Creating a Hypertable

To convert a standard entity into a hypertable, use the `.IsHypertable()` method in your entity configuration. You must specify a time column, which will serve as the primary partitioning dimension.
By default, chunks are created to cover a time interval of 7 days. You can customize this using the `.WithChunkTimeInterval()` method.

- [See also: the create_hypertable](https://docs.tigerdata.com/api/latest/hypertable/create_hypertable/)
- [See also: set_chunk_time_interval](https://docs.tigerdata.com/api/latest/hypertable/set_chunk_time_interval/)

```csharp
public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        // Convert the table to a hypertable partitioned by 'Time'
        // and set the chunk interval to 1 day.
        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day");
    }
}
```

## Advanced Partitioning with Dimensions

For very large datasets, you can add secondary partitioning dimensions to further divide your data. This is especially useful for improving query performance by allowing the query planner to prune chunks based on non-time predicates.

Dimensions can be:

- Range Partitions: Based on a continuous value like another timestamp or a numeric value.
- Hash Partitions: Based on a discrete value like a device ID or location, spreading the data across a fixed number of partitions.

[See also: add_dimension](https://docs.tigerdata.com/api/latest/hypertable/add_dimension/)

```csharp
public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        builder.HasKey(e => new { e.Id, e.EventTimestamp, e.OrderPlacedTimestamp, e.WarehouseId });

    builder.IsHypertable(e => e.EventTimestamp)
            .WithChunkTimeInterval("7 days")
            // Add a second time-based dimension
            .HasDimension(Dimension.CreateRange("OrderPlacedTimestamp", "1 month"))
            // Add a space-based dimension for warehouse ID
            .HasDimension(Dimension.CreateHash("WarehouseId", 4));
    }
}
```

## Compression

Time-series data can be compressed to reduce the amount of storage required, and increase the speed of some queries. This is a cornerstone feature of TimescaleDB. When new data is added to your database, it is in the form of uncompressed rows. TimescaleDB uses a built-in job scheduler to convert this data to the form of compressed columns. This occurs across chunks of TimescaleDB hypertables.

[See also: TimescaleDB Compression](https://docs.tigerdata.com/use-timescale/latest/compression/)

> :information_source: Generated migrations use the TimescaleDB 2.18+ columnstore option names (`timescaledb.enable_columnstore`, `timescaledb.columnstore_segmentby`, ...) by default. On servers older than 2.18, opt into the legacy names via `UseTimescaleDb(o => o.UseLegacyCompressionSql())`.

### Enabling Compression

Use the `.EnableCompression()` method to enable compression on a hypertable:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });
        builder.IsHypertable(x => x.Time).EnableCompression();
    }
}
```

### Compression SegmentBy

Compression can be optimized by specifying columns to group by when compressing data. This maps to TimescaleDB's `timescaledb.compress_segmentby` setting. Columns specified for segmenting are not compressed themselves but are used as keys to group rows within compressed chunks.

Good candidates for segmentation are columns with low cardinality, such as device identifiers, tenant identifiers, or location codes. Segmenting by these columns allows TimescaleDB to decompress only the relevant segments when querying by those columns, improving query performance.

Use the `.WithCompressionSegmentBy()` method to specify segmentation columns:

```csharp
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
            .WithCompressionSegmentBy(x => x.DeviceId);
    }
}

public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

> :warning: **Note:** Using `.WithCompressionSegmentBy()` automatically enables compression on the hypertable.

Multiple columns can be specified for segmentation:

```csharp
builder.IsHypertable(x => x.Time)
    .WithCompressionSegmentBy(x => x.DeviceId, x => x.TenantId);
```

### Compression OrderBy

Compression can be further optimized by specifying the order in which data is stored within each compressed segment. This maps to TimescaleDB's `timescaledb.compress_orderby` setting. Ordering data optimally can improve compression ratios and query performance for range scans.

The `.WithCompressionOrderBy()` method provides multiple approaches for specifying column ordering:

#### Using OrderBy Array

The most explicit approach uses an array of `OrderBy` instances created with `OrderByBuilder`:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DeviceReadingConfiguration : IEntityTypeConfiguration<DeviceReading>
{
    public void Configure(EntityTypeBuilder<DeviceReading> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
            .WithCompressionSegmentBy(x => x.DeviceId)
            .WithCompressionOrderBy(
                OrderByBuilder.For<DeviceReading>(x => x.Time).Descending(),
                OrderByBuilder.For<DeviceReading>(x => x.Voltage).Ascending());
    }
}
```

#### Using OrderBySelector

A more concise approach uses the `OrderBySelector` with a lambda expression:

```csharp
builder.IsHypertable(x => x.Time)
    .WithCompressionSegmentBy(x => x.DeviceId)
    .WithCompressionOrderBy(s => new[]
    {
        s.ByDescending(x => x.Time),
        s.ByAscending(x => x.Voltage)
    });
```

#### Using String Extensions

Column names can be used directly with string extension methods:

```csharp
builder.IsHypertable(x => x.Time)
    .WithCompressionSegmentBy(x => x.DeviceId)
    .WithCompressionOrderBy(
        "Time".Descending(),
        "Voltage".Ascending(nullsFirst: true));
```

#### OrderBy Configuration Options

The `OrderByBuilder` supports three configuration methods:

- `.Default()`: Uses database default ordering (typically ascending)
- `.Ascending(nullsFirst)`: Orders ascending, with optional null sorting behavior
- `.Descending(nullsFirst)`: Orders descending, with optional null sorting behavior

The `nullsFirst` parameter controls null value placement:

- `true`: NULL values appear first
- `false`: NULL values appear last
- `null`: Uses database default (NULLS LAST for ASC, NULLS FIRST for DESC)

```csharp
builder.IsHypertable(x => x.Time)
    .WithCompressionOrderBy(
        OrderByBuilder.For<DeviceReading>(x => x.Time).Descending(nullsFirst: false),
        OrderByBuilder.For<DeviceReading>(x => x.Voltage).Ascending(nullsFirst: true));
```

> :warning: **Note:** Using `.WithCompressionOrderBy()` automatically enables compression on the hypertable.

### Complete Compression Example

```csharp
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
            .WithCompressionSegmentBy(x => x.DeviceId, x => x.TenantId)
            .WithCompressionOrderBy(
                OrderByBuilder.For<DeviceReading>(x => x.Time).Descending(),
                OrderByBuilder.For<DeviceReading>(x => x.Voltage).Ascending());
    }
}

public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

## Chunk skipping

Enable range statistics for a specific column in a compressed hypertable. This tracks a range of values for that column per chunk. Used for chunk skipping during query optimization and applies only to the chunks created after chunk skipping is enabled.

> :warning: **Note:** When you use chunk skipping, compression is enabled automatically on the hypertable, as it is a prerequisite.

[See also: enable_chunk_skipping](https://docs.tigerdata.com/api/latest/hypertable/enable_chunk_skipping/)

```csharp
public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        // Enable chunk skipping on the 'Time' column.
        // This will also automatically enable compression.
        builder.IsHypertable(x => x.Time)
               .WithChunkSkipping(x => x.Time);
    }
}
```

## Sparse Indexes

Sparse indexes are columnstore metadata structures that allow TimescaleDB to skip chunks whose data cannot satisfy a query predicate. Two index types are supported: `bloom` (probabilistic, supports composite columns) and `minmax` (range-based, single column only).

> :warning: **Note:** Sparse indexes require `compress_orderby` to be configured. The convention validates this at model finalization and raises an `InvalidOperationException` if `compress_orderby` is absent.

[See also: Columnstore indexes](https://docs.tigerdata.com/api/latest/compression/compress_chunk/)

### Using Typed Selectors

The `SparseIndexSelector<TEntity>` provides type-safe lambda expressions for building index entries:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SensorMeasurementConfiguration : IEntityTypeConfiguration<SensorMeasurement>
{
    public void Configure(EntityTypeBuilder<SensorMeasurement> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day")
               .WithCompressionSegmentBy(x => x.DeviceId)
               .WithCompressionOrderBy(
                   OrderByBuilder.For<SensorMeasurement>(x => x.Time).Descending())
               .WithSparseIndex(
                   s => s.Bloom(x => x.SensorType),
                   s => s.MinMax(x => x.Value));
    }
}
```

### Using Explicit SparseIndex Objects

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;

builder.IsHypertable(x => x.Time)
       .WithCompressionOrderBy(OrderByBuilder.For<SensorMeasurement>(x => x.Time).Descending())
       .WithSparseIndex(
           new SparseIndex(ESparseIndexType.Bloom, ["SensorType", "Region"]),
           new SparseIndex(ESparseIndexType.MinMax, ["Value"]));
```

### Using a Raw String

A comma-separated string of `bloom(col)` and `minmax(col)` entries is also accepted:

```csharp
builder.WithSparseIndex("bloom(sensor_type,region), minmax(value)");
```

> :warning: **Note:** When using the raw string overload, column names must match the database column names, not the property names.

### Composite Bloom Indexes

`bloom` supports multiple columns in a single entry. Use this for compound predicate filtering:

```csharp
builder.WithSparseIndex(s => s.Bloom(x => x.SensorType, x => x.Region));
```

### Disabling Auto Sparse Indexes

TimescaleDB automatically creates sparse indexes on `compress_orderby` columns. To suppress this behaviour:

```csharp
builder.WithoutAutoSparseIndexes();
```

This sets `timescaledb.sparse_index = ''` on the table.

### Validation Rules

The `SparseIndexValidationConvention` enforces these rules at model finalization:

- `compress_orderby` must be configured before any sparse index entry is processed.
- `bloom` and `minmax` entries cannot include `compress_segmentby` columns — segmentby columns are not compressed and cannot have a sparse index.
- A single-column `bloom(col)` entry is rejected when `col` is already a `compress_orderby` column, because `compress_orderby` columns receive an implicit sparse index automatically. Use a composite `bloom(col, other)` if additional coverage is needed.
- `minmax` accepts exactly one column. Specifying multiple columns raises an error.
- Two single-column entries targeting the same column are rejected as duplicates.

## Compress Chunk Time Interval

The `WithCompressChunkTimeInterval` method sets the minimum time interval for merging chunks during compression. When this is set, TimescaleDB can combine adjacent chunks into a single larger compressed chunk during the compression job.

The value must be a string interval and must be a multiple of the hypertable's `chunk_time_interval`.

> :warning: **Warning:** Chunk merges are irreversible. Decreasing `compress_chunk_time_interval` later cannot un-merge already merged chunks.

```csharp
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
               .WithCompressChunkTimeInterval("7 days");
    }
}
```

### Removal Quirk

PostgreSQL rejects `RESET` for the `timescaledb.compress_chunk_time_interval` storage option (`"only columnstore options segmentby and orderby can be reset"`). When the value is removed from the model, the migration instead emits:

```sql
ALTER TABLE "device_readings" SET (timescaledb.compress_chunk_time_interval = '0');
```

Setting the interval to `'0'` clears the merge behaviour without using `RESET`.

## Migrating Existing Data

When converting an existing PostgreSQL table with data into a hypertable, the `.WithMigrateData()` method controls whether existing rows should be migrated to the hypertable structure. By default, this option is set to `false`, meaning existing data remains in place without migration.

Setting this to `true` is useful when converting tables that already contain time-series data, ensuring all existing rows are properly partitioned into chunks according to the hypertable configuration.

[See also: create_hypertable - migrate_data parameter](https://docs.tigerdata.com/api/latest/hypertable/create_hypertable/)

```csharp
public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        builder.HasKey(x => new { x.Id, x.Time });

        // Enable migration of existing data when converting to a hypertable
        builder.IsHypertable(x => x.Time)
               .WithChunkTimeInterval("1 day")
               .WithMigrateData(true);
    }
}
```

> :warning: **Note:** Migrating large datasets can be a time-consuming operation. Consider the size of the existing table before enabling this option in production environments.

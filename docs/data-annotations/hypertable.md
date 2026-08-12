# Hypertables

Hypertables are the core feature of TimescaleDB, automatically partitioning your data by time and other dimensions into smaller, more manageable child tables called "chunks." This architecture is the key to achieving fast ingest rates and query performance on large time-series datasets.

## Creating a Hypertable

To convert a standard entity into a hypertable, use the `Hypertable` attribute in your entity. You must specify a time column, which will serve as the primary partitioning dimension.
By default, chunks are created to cover a time interval of 7 days. You can customize this using the `ChunkTimeInterval` property.

- [See also: the create_hypertable](https://docs.tigerdata.com/api/latest/hypertable/create_hypertable/)
- [See also: set_chunk_time_interval](https://docs.tigerdata.com/api/latest/hypertable/set_chunk_time_interval/)

```csharp
[Hypertable(nameof(Time), ChunkTimeInterval = "1 day")]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

## Advanced Partitioning with Dimensions

To add partitioning with dimensions, refere to the [Fluent API](../fluent-api/hypertable#advanced-partitioning-with-dimensions).

## Compression

Time-series data can be compressed to reduce the amount of storage required, and increase the speed of some queries. This is a cornerstone feature of TimescaleDB. When new data is added to your database, it is in the form of uncompressed rows. TimescaleDB uses a built-in job scheduler to convert this data to the form of compressed columns. This occurs across chunks of TimescaleDB hypertables.

[See also: TimescaleDB Compression](https://docs.tigerdata.com/use-timescale/latest/compression/)

> :information_source: Generated migrations use the TimescaleDB 2.18+ columnstore option names (`timescaledb.enable_columnstore`, `timescaledb.columnstore_segmentby`, ...) by default. On servers older than 2.18, opt into the legacy names via `UseTimescaleDb(o => o.UseLegacyCompressionSql())`.

### Enabling Compression

Set the `EnableCompression` property to true to enable compression on a hypertable:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time), EnableCompression = true)]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

### Compression SegmentBy

Compression can be optimized by specifying columns to group by when compressing data. This maps to TimescaleDB's `timescaledb.compress_segmentby` setting. Columns specified for segmenting are not compressed themselves but are used as keys to group rows within compressed chunks.

Good candidates for segmentation are columns with low cardinality, such as device identifiers, tenant identifiers, or location codes. Segmenting by these columns allows TimescaleDB to decompress only the relevant segments when querying by those columns, improving query performance.

Use the `CompressionSegmentBy` property to specify segmentation columns:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time),
    EnableCompression = true,
    CompressionSegmentBy = new[] { "DeviceId" })]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

> :warning: **Note:** When `CompressionSegmentBy` is specified, `EnableCompression` is automatically set to true.

Multiple columns can be specified for segmentation:

```csharp
[Hypertable(nameof(Time),
    CompressionSegmentBy = new[] { "DeviceId", "TenantId" })]
```

### Compression OrderBy

Compression can be further optimized by specifying the order in which data is stored within each compressed segment. This maps to TimescaleDB's `timescaledb.compress_orderby` setting. Ordering data optimally can improve compression ratios and query performance for range scans.

Use the `CompressionOrderBy` property to specify column ordering. Since attributes cannot use expressions, the full SQL syntax must be specified for direction and null handling:

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time),
    EnableCompression = true,
    CompressionSegmentBy = new[] { "DeviceId" },
    CompressionOrderBy = new[] { "Time DESC", "Voltage ASC" })]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

> :warning: **Note:** When `CompressionOrderBy` is specified, `EnableCompression` is automatically set to true.

#### OrderBy Syntax

Each string in the `CompressionOrderBy` array follows SQL syntax for ordering:

- **Column name only**: Uses database default (typically ascending) - `"Time"`
- **Ascending**: Column name followed by `ASC` - `"Time ASC"`
- **Descending**: Column name followed by `DESC` - `"Time DESC"`
- **Null handling**: Add `NULLS FIRST` or `NULLS LAST` - `"Time DESC NULLS LAST"`, `"Voltage ASC NULLS FIRST"`

Examples:

```csharp
// Default ordering (ascending)
CompressionOrderBy = new[] { "Time" }

// Descending with nulls last
CompressionOrderBy = new[] { "Time DESC NULLS LAST" }

// Multiple columns with mixed directions
CompressionOrderBy = new[] { "Time DESC", "Voltage ASC NULLS FIRST", "Power DESC NULLS LAST" }
```

### Complete Compression Example

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time),
    ChunkTimeInterval = "1 day",
    EnableCompression = true,
    CompressionSegmentBy = new[] { "DeviceId", "TenantId" },
    CompressionOrderBy = new[] { "Time DESC", "Voltage ASC" })]
[PrimaryKey(nameof(Id), nameof(Time))]
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
[Hypertable(nameof(Time), ChunkSkipColumns = new[] { "Time", "DeviceId" })]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

## Sparse Indexes

Sparse indexes are columnstore metadata structures that allow TimescaleDB to skip chunks whose data cannot satisfy a query predicate. Two index types are supported: `bloom` (probabilistic, supports composite columns) and `minmax` (range-based, single column only).

> :warning: **Note:** Sparse indexes require `compress_orderby` to be configured. The convention validates this at model finalization and raises an `InvalidOperationException` if `compress_orderby` is absent.

[See also: Columnstore indexes](https://docs.tigerdata.com/api/latest/compression/compress_chunk/)

### Using `[SparseIndex]`

Apply `[SparseIndex]` one or more times to declare individual sparse index entries.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time),
    ChunkTimeInterval = "1 day",
    CompressionSegmentBy = new[] { "DeviceId" },
    CompressionOrderBy = new[] { "Time DESC" })]
[PrimaryKey(nameof(Id), nameof(Time))]
[SparseIndex(ESparseIndexType.Bloom, nameof(SensorType))]
[SparseIndex(ESparseIndexType.MinMax, nameof(Value))]
public class SensorMeasurement
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public double Value { get; set; }
}
```

Use `nameof()` for each column to maintain refactoring safety. Property names are resolved to their database column names during model finalization.

### Composite Bloom Index

`bloom` supports multiple columns in a single attribute:

```csharp
[SparseIndex(ESparseIndexType.Bloom, nameof(SensorType), nameof(Region))]
```

### `minmax` Single-Column Restriction

`minmax` accepts exactly one column. Specifying multiple columns in `[SparseIndex(ESparseIndexType.MinMax, ...)]` is not caught at attribute construction time — the violation is detected by the `SparseIndexValidationConvention` at model finalization and raises an `InvalidOperationException`.

### Disabling Auto Sparse Indexes

TimescaleDB automatically creates sparse indexes on `compress_orderby` columns. To suppress this behaviour, set `DisableAutoSparseIndexes = true` on `[Hypertable]`:

```csharp
[Hypertable(nameof(Time),
    CompressionOrderBy = new[] { "Time DESC" },
    DisableAutoSparseIndexes = true)]
[PrimaryKey(nameof(Id), nameof(Time))]
public class SensorMeasurement { ... }
```

> :warning: **Note:** `DisableAutoSparseIndexes = true` and `[SparseIndex]` attributes are mutually exclusive. Having both on the same entity raises an `InvalidOperationException` at model build.

### Validation Rules

The `SparseIndexValidationConvention` enforces these rules at model finalization:

- `compress_orderby` must be configured before any sparse index entry is processed.
- `bloom` and `minmax` entries cannot include `compress_segmentby` columns — segmentby columns are not compressed and cannot have a sparse index.
- A single-column `bloom(col)` entry is rejected when `col` is already a `compress_orderby` column, because `compress_orderby` columns receive an implicit sparse index automatically.
- `minmax` accepts exactly one column. Specifying multiple columns raises an error.
- Two single-column entries targeting the same column are rejected as duplicates.

## Compress Chunk Time Interval

The `CompressChunkTimeInterval` property sets the minimum time interval for merging chunks during compression. When set, TimescaleDB can combine adjacent chunks into a single larger compressed chunk during the compression job.

The value must be a string interval and must be a multiple of the hypertable's `ChunkTimeInterval`. Setting this property implicitly enables compression.

> :warning: **Warning:** Chunk merges are irreversible. Decreasing `CompressChunkTimeInterval` later cannot un-merge already merged chunks.

```csharp
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;

[Hypertable(nameof(Time),
    ChunkTimeInterval = "1 day",
    CompressionSegmentBy = new[] { "DeviceId" },
    CompressChunkTimeInterval = "7 days")]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Temperature { get; set; }
}
```

### Removal Quirk

PostgreSQL rejects `RESET` for the `timescaledb.compress_chunk_time_interval` storage option. When the value is removed from the model, the migration emits `SET (timescaledb.compress_chunk_time_interval = '0')` to clear it instead. See [Fluent API — Hypertables](../fluent-api/hypertable.md#compress-chunk-time-interval) for details.

## Migrating Existing Data

When converting an existing PostgreSQL table with data into a hypertable, the `MigrateData` property controls whether existing rows should be migrated to the hypertable structure. By default, this property is set to `false`, meaning existing data remains in place without migration.

Setting `MigrateData` to `true` is useful when converting tables that already contain time-series data, ensuring all existing rows are properly partitioned into chunks according to the hypertable configuration.

[See also: create_hypertable - migrate_data parameter](https://docs.tigerdata.com/api/latest/hypertable/create_hypertable/)

```csharp
[Hypertable(nameof(Time), ChunkTimeInterval = "1 day", MigrateData = true)]
[PrimaryKey(nameof(Id), nameof(Time))]
public class DeviceReading
{
    public Guid Id { get; set; }
    public DateTime Time { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Voltage { get; set; }
    public double Power { get; set; }
}
```

> :warning: **Note:** Migrating large datasets can be a time-consuming operation. Consider the size of the existing table before enabling this option in production environments.

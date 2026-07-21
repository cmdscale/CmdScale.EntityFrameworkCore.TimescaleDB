# Scaffolding Behavior and Limitations

Scaffolding a TimescaleDB database with the `CmdScale.EntityFrameworkCore.TimescaleDB.Design` package emits typed Fluent API calls or data annotations for all TimescaleDB features. The scaffolded model is functionally equivalent to the database — migrating from it produces an identical schema, a contract enforced by roundtrip integration tests. See [dotnet ef tools](./01-dotnet-tools.md) for setup and command usage.

## Positional-null policy forms

Scaffolded retention and reorder policy calls use all positional arguments, filling omitted parameters with `null`:

```csharp
.WithRetentionPolicy("90 days", null, null, null, 3, "15 minutes")
.WithReorderPolicy("trade_time_idx", null, null, 2, "10 minutes")
```

This is intentional: the user-facing overloads take `initialStart` (`DateTime?`) as an early positional parameter, and a trimmed call would bind later arguments to the wrong parameters. The parameter order is:

```csharp
.WithRetentionPolicy(dropAfter, dropCreatedBefore, scheduleInterval, maxRuntime, maxRetries, retryPeriod)
.WithReorderPolicy(indexName, scheduleInterval, maxRuntime, maxRetries, retryPeriod)
```

`InitialStart` is always chained as a separate `.WithInitialStart(DateTime)` call. Rewriting a scaffolded call to named-argument style by hand is safe — the result is annotation-equivalent.

The continuous aggregate refresh policy is not affected and scaffolds with trailing nulls trimmed (`.WithRefreshPolicy("7 days", "1 hour")`).

## Normalization contract

Scaffolding reproduces what the database stores, which is not always the literal text the original code wrote:

- **Intervals are humanized when an exact single-unit reduction exists**: `01:00:00` → `1 hour`, `86400000000` (bigint µs) → `1 day`, `2 day` → `2 days`. Values without a single-unit reduction (`01:30:00`) are kept as-is. Calendar units collapse to the fixed duration TimescaleDB stores: `1 month` → `30 days`.
- **WHERE clauses come back PostgreSQL-normalized**: `"\"ticker\" = 'MCRS'"` scaffolds as `"(ticker = 'MCRS'::text)"`.
- **GROUP BY is canonicalized by PostgreSQL** before scaffolding sees it: positional notation (`.AddGroupByColumn("1, 2")`) is resolved to the referenced expressions. Simple columns become typed group-by entries, the `time_bucket(...)` expression is represented by the time-bucket configuration, and non-column expressions are preserved verbatim as raw strings. Grouping semantics always survive; the positional notation does not.
- **Values equal to TimescaleDB defaults are omitted** and filled back in by the runtime.
- **Implicitly enabled compression is reported explicitly**: chunk skipping requires compression, so a hypertable configured only with `.WithChunkSkipping(...)` scaffolds with `EnableCompression` set.
- **TimescaleDB's auto-created indexes are suppressed** (`<table>_<timecol>_idx` and per-dimension composites) — they are recreated automatically on hypertable creation. A user-defined, non-unique index exactly matching this name and column pattern is indistinguishable and is suppressed as well.
- **TimescaleDB internal schemas** (`_timescaledb_internal`, `_timescaledb_catalog`, `_timescaledb_config`, `_timescaledb_cache`) are excluded automatically unless explicitly requested via `--schema`.

---

## Known limitations

- **`WithNoData` and `CreateGroupIndexes`** are creation-time-only options of a continuous aggregate and are not queryable from the catalog. Scaffolded aggregates use the defaults (populated on creation, group indexes created). The schema is identical; only the initial-population timing differs.
- **CLR type fidelity is not preserved**: custom mapped types (e.g. NodaTime `LocalDateTime`) scaffold as BCL types with an explicit store type (`[Column(TypeName = "timestamp without time zone")] DateTime`). The column definition is identical; re-apply the custom type mapping manually if needed.
- **`InitialStart` sub-microsecond precision is lost**: PostgreSQL stores microseconds, so .NET's seventh fractional digit (100 ns ticks) does not survive — `…19.3905112Z` scaffolds as `…19.3905110Z`.
- **Non-column GROUP BY expressions have no data-annotations representation**: in `--data-annotations` mode they are reported as a warning and must be configured via the Fluent API's `AddGroupByColumn(string)`.
- **Unparseable view definitions degrade gracefully**: when a continuous aggregate's view SQL cannot be parsed into typed configuration, a warning is reported and the configuration is preserved as `.HasAnnotation(...)` calls; migrations still recreate the view correctly from the raw SQL definition.

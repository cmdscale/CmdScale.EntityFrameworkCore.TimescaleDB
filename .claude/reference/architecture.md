# Architecture Reference

Structure and non-obvious implementation knowledge for the library. For file *locations*, use the per-feature formula below instead of a listing — the codebase follows it strictly.

## Projects

| Project | Purpose |
|---------|---------|
| `src/Eftdb/` | Core runtime (`CmdScale.EntityFrameworkCore.TimescaleDB`): migrations, SQL generation, fluent API, attributes, differs |
| `src/Eftdb.Design/` | Design-time (`...TimescaleDB.Design`): C# migration code generation, database scaffolding; registered via MSBuild `.targets` + `DesignTimeServicesReference` |
| `tests/Eftdb.Tests/` | Unit tests (xUnit, Moq) |
| `tests/Eftdb.FunctionalTests/` | EF Core specification tests + integration tests (Testcontainers) |
| `benchmarks/Eftdb.Benchmarks/` | BenchmarkDotNet |
| `samples/Eftdb.Samples.Shared` / `.CodeFirst` / `.DatabaseFirst` | Shared models, code-first migrations, db-first scaffolding examples |

## Per-Feature File Formula

Features: `Hypertable`, `ReorderPolicy`, `RetentionPolicy`, `CompressionPolicy`, `ContinuousAggregate`, `ContinuousAggregatePolicy`. Every feature places its files identically:

```
src/Eftdb/
  Configuration/{Feature}/        {Feature}Attribute, {Feature}Convention, {Feature}Annotations,
                                  {Feature}TypeBuilder (fluent API),
                                  {Feature}StringBuilder (string-based builder for scaffolded OnModelCreating),
                                  {Feature}BuilderCore (shared logic of typed + string builder, where present)
  Internals/Features/{Feature}s/  {Feature}Differ, {Feature}ModelExtractor
  Generators/                     {Feature}SqlGenerator     (runtime SQL)
  MigrationExtensions/            {Feature}MigrationExtensions  (typed migrationBuilder.* methods)
  Operations/                     Create|Add / Alter / Drop|Remove {Feature}Operation

src/Eftdb.Design/
  Features/{Feature}/             {Feature}CSharpGenerator      (typed migration calls),
                                  {Feature}AnnotationRenderer   (scaffold → fluent API / attributes),
                                  {Feature}ScaffoldingExtractor (+ {Feature}Info record),
                                  {Feature}AnnotationApplier
```

### Visibility Policy

Implementation types are `internal` in both packages (tests reach them via `InternalsVisibleTo`); keep new ones internal:

- **Runtime public surface** = the consumer contract only: attributes, type builders + string builders, `OrderBy*`/`SparseIndex*` fluent types, `Abstractions/`, `EF.Functions` extensions, bulk copy, `UseTimescaleDb()`/`TimescaleDbOptions`, `MigrationExtensions`, `Operations` (appear in `OperationBuilder<T>` signatures), plus `{Feature}Annotations` and `DefaultValues` (kept public so consumers can read config off a built model). Differs, model extractors, SQL generators, conventions, `SqlBuilderHelper`, `PolicyJobSqlBuilder`, and the `Timescale*` differ/SQL-generator/convention-plugin classes are internal.
- **Design public surface** = only the pipeline entry types (`TimescaleDBDesignTimeServices`, `TimescaleDatabaseModelFactory`, `TimescaleDbCodeGenerator`, `TimescaleCSharpMigrationOperationGenerator`, and the `Generators/Timescale*` classes). All per-feature Design types are internal.

Hypertable extras: `DimensionAttribute`, `SparseIndex` + `SparseIndexAttribute` + `SparseIndexValidationConvention` (validates bloom/minmax arity, segmentby/orderby prerequisites, duplicates at model finalization). ContinuousAggregate extras: property-level `TimeBucketAttribute`, `AggregateAttribute`, `GroupByColumnAttribute`; generic `ContinuousAggregateBuilder<TEntity, TSource>`.

## Entry Points

- `TimescaleDbContextOptionsBuilderExtensions` — `UseTimescaleDb()` / `UseTimescaleDb(o => o.UseLegacyCompressionSql())` registers everything
- `TimescaleDbOptions` — `UseLegacyCompressionSql()` opts into pre-2.18 compression SQL (`add_compression_policy` instead of `CALL add_columnstore_policy`)
- `TimescaleDbServiceCollectionExtensions` — registers `IMigrationsModelDiffer`, `IConventionSetPlugin`, `IMethodCallTranslatorPlugin`
- `TimescaleDbMigrationsSqlGenerator` — runtime dispatch: switches on operation type → `{Feature}SqlGenerator.Generate(op)` → `SqlBuilderHelper.BuildQueryString(...)`. `CreateContinuousAggregateOperation` runs with `suppressTransaction: true` (CA DDL cannot run in a transaction)
- `DefaultValues.cs` — centralized constants (`DefaultSchema = "public"`, `ChunkTimeInterval = "7 days"`, reorder policy schedule defaults)

## Shared Helpers (the non-formulaic files)

Runtime (`src/Eftdb/`):
- `Configuration/ConventionValidationHelper` — `ValidateExclusiveFields` (XOR constraints like `After`/`CreatedBefore`), `ParseInitialStart`
- `Configuration/PolicyJobBuilderCore` — base class for reorder/retention/CA-policy builder cores (ScheduleInterval, MaxRuntime, MaxRetries, RetryPeriod, InitialStart annotations)
- `Configuration/TimeColumnStoreTypeValidationConvention` + `Internals/TimeColumnStoreTypeValidator` — model-finalized validation that time columns resolve to timestamp/timestamptz/date/integer store types
- `Internals/ColumnNameResolver` — **single resolution authority** for column names: accepts CLR property name, dot-separated complex-type path, or the column name itself; recursive complex-type traversal both directions; complex collections skipped
- `Internals/ExpressionHelper` — `GetPropertyName` from selector lambdas; chained member access yields dot-paths for `ColumnNameResolver`
- `Internals/ParentEntityTypeResolver` — resolves a CA's parent entity by CLR name, EF short name, or table name
- `Internals/CompressionAnnotationExtractor` — segment-by/order-by/sparse-index extraction with property→column resolution (hypertable + CA extractors)
- `Internals/Features/CompressionDiffHelper` — compression list comparison/rewrite helpers (hypertable + CA differs)
- `Internals/Features/CompressionPolicies/CompressionPolicyDefaultHelper` — dynamic schedule_interval default (12h if chunk interval ≥ 1 day, else half of it)
- `Generators/SqlBuilderHelper` — `Regclass()`, `QualifiedIdentifier()`, `QuoteIdentifier()`, `EscapeStringLiteral`, `FormatTimestamp`, command grouping, SELECT→PERFORM rewriting for idempotent scripts
- `Generators/PolicyJobSqlBuilder` — shared `alter_job` clause builder
- `Generators/CompressionSettingsSqlHelper` — `SET (timescaledb.enable_columnstore = ...)` vs legacy `timescaledb.compress` clause, changed-settings diff
- `Query/` — `EF.Functions.TimeBucket()` overloads + `Internal/` translator plugin mapping to `time_bucket(...)`; runtime-only, throw outside LINQ

Design (`src/Eftdb.Design/`):
- `TimescaleDBDesignTimeServices` — registers `TimescaleCSharpMigrationOperationGenerator`, `TimescaleDatabaseModelFactory`, `TimescaleDbAnnotationCodeGenerator`, `TimescaleModelCodeGeneratorSelector`
- `Generators/MigrationCallWriter`, `Generators/CSharpGeneratorHelper` — emit `.Method(arg: value, …)` calls, collection-expression/static-call literals
- `Generators/TimescaleCSharpHelper` — extends `UnknownLiteral` for `NameOfCodeFragment`, `SparseIndexSelectorCodeFragment` (→ `s => s.Bloom(...)`), `ColumnListCodeFragment` (→ `nameof(...)` or constant interpolated string), mixed `object?[]` arrays
- `Generators/AnnotationRendererHelper` — `Find`, `GetString`, `SplitColumns`, `Consume`, `ResolvePropertyName`, `TryResolvePropertyName`, `ResolveColumns`, `ColumnReference`, `OrderByReference`, `ToArgumentArray`
- `Generators/PolicyJobRendererHelper` — optional policy-job argument rendering shared by all policy renderers
- `Generators/IFeatureAnnotationRenderer` + code fragments `NameOfCodeFragment` (`nameof(X)` / `$"{nameof(X)} DESC"`), `SparseIndexSelectorCodeFragment`, `ColumnListCodeFragment`
- `Scaffolding/ScaffoldingExtractorHelper` — `UsingConnection` execute-around, `ViewExists`, `TimescaleInternalSchemaExclusion`
- `Scaffolding/IntervalParsingHelper` — `NormalizeInterval` (`"01:00:00"` → `"1 hour"`); **all interval reads must be normalized** to avoid phantom migrations
- `Scaffolding/ViewDefinitionParser` — best-effort cached parse of CA view SQL (time bucket, aggregates, GROUP BY, WHERE)
- `Scaffolding/CompressionSettingsScaffoldingHelper` — reads `timescaledb_information.hypertable_columnstore_settings` (2.18+) with `compression_settings` fallback

## Diffing

`TimescaleMigrationsModelDiffer` (extends EF's `MigrationsModelDiffer`) runs the base differ first, builds a `FeatureDiffContext`, invokes each `IFeatureDiffer`, and orders the results via `GetOperationPriority()`.

`FeatureDiffContext` carries what differs cannot derive alone:
- **TableRenames / IndexRenames / ColumnRenames** — built from EF's rename operations so differs treat renames as renames, not drop-and-create. Resolve via `ResolveTable()` / `ResolveIndex()` / `ResolveColumn()`. Schemas normalized to `DefaultValues.DefaultSchema`.
- **RecreatedAggregates** — CAs being dropped and recreated this diff; recreation cascades to drop refresh/retention policies, so dependent policy differs re-add them even when unchanged.
- `FeatureDiffContext.Empty` — identity context for un-orchestrated runs (unit tests).

### Operation Priority

Drops negative (before EF table drops, reverse dependency order); adds/alters positive (after EF table creation, dependency order):

| Priority | Operation |
|----------|-----------|
| -60 | DropRetentionPolicy |
| -50 | RemoveContinuousAggregatePolicy |
| -45 | DropCompressionPolicy |
| -40 | DropContinuousAggregate |
| -20 | DropReorderPolicy |
| 0 | standard EF operations |
| 10 / 15 | CreateHypertable / AlterHypertable |
| 20 | Add/AlterReorderPolicy |
| 30 / 40 | Create/AlterContinuousAggregate |
| 45 | AddContinuousAggregatePolicy |
| 50 | Add/AlterCompressionPolicy |
| 60 | Add/AlterRetentionPolicy |

## Scaffolding Pipeline (`dotnet ef dbcontext scaffold`)

**Phase 1 — Database extraction.** `TimescaleDatabaseModelFactory` (overrides NpgsqlDatabaseModelFactory) runs each `{Feature}ScaffoldingExtractor` + `{Feature}AnnotationApplier` pair to layer TimescaleDB metadata onto the `DatabaseModel` as annotations (same format the runtime uses). Extractors query `timescaledb_information.*` views (jobs joined with `_timescaledb_config.bgw_job` for timezone). Appliers suppress default schedule intervals; all intervals normalized via `IntervalParsingHelper`.

**Phase 2 — Code generation.** `TimescaleDbAnnotationCodeGenerator` dispatches to registered `IFeatureAnnotationRenderer`s: `GenerateFluentApiCalls` (default) or `GenerateDataAnnotationAttributes` (`UseDataAnnotations = true`). **Registration order matters**: policy renderers (CA policy, retention, reorder, compression) must run after their parent renderer (hypertable or CA) — their `ShouldRender` guard checks the parent annotation was consumed. `TimescaleCSharpModelGenerator` (selected by `TimescaleModelCodeGeneratorSelector`) post-processes generated files to inject missing TimescaleDB `using` directives.

## Continuous Aggregate Notes

- Operation properties: `MaterializedViewName`, `ParentName` (entity name, resolved via EF metadata), `TimeBucketWidth`, `TimeBucketSourceColumn`, `AggregateFunctions` (colon-delimited wire format, see patterns.md), `GroupByColumns`, `WhereClause` (raw SQL, emitted verbatim — quoted identifiers must match resolved column names)
- `first()`/`last()` take the time-bucket column as second argument: `last("price", "timestamp")`
- Aggregate column aliases must match property names for EF mapping

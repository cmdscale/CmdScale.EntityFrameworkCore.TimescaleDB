# Architecture Reference

This document provides detailed architectural information for the CmdScale.EntityFrameworkCore.TimescaleDB library.

## Project Structure

### 2 Main Packages

1. **CmdScale.EntityFrameworkCore.TimescaleDB** - Core runtime library
   - Migrations and SQL generation
   - Fluent API and data annotations
   - Feature differs and model extractors

2. **CmdScale.EntityFrameworkCore.TimescaleDB.Design** - Design-time services
   - C# code generation for migrations (`dotnet ef migrations add`)
   - Database scaffolding (`dotnet ef dbcontext scaffold`)
   - Registered via MSBuild `.targets` file with `DesignTimeServicesReference` attribute

### Supporting Projects

- **CmdScale.EntityFrameworkCore.TimescaleDB.Tests** - Unit tests (xUnit, Moq)
- **CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests** - EF Core specification tests (Testcontainers)
- **CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks** - Performance benchmarks (BenchmarkDotNet)

### Sample Projects

1. **samples/Eftdb.Samples.Shared/** - Shared models and configurations
2. **samples/Eftdb.Samples.CodeFirst/** - Code-first migration examples
3. **samples/Eftdb.Samples.DatabaseFirst/** - Database-first scaffolding examples

## Core Library Structure

### Root Namespace - Entry Points

| File | Purpose |
|------|---------|
| `TimescaleDbServiceCollectionExtensions.cs` | Registers `IMigrationsModelDiffer`, `IConventionSetPlugin` |
| `TimescaleDbContextOptionsBuilderExtensions.cs` | Service registration via `UseTimescaleDb()` and `UseTimescaleDb(o => o.UseLegacyCompressionSql())` |
| `TimescaleDbOptions.cs` | Provider options: `UseLegacyCompressionSql()` opts into pre-2.18 compression API naming |
| `TimescaleDbMigrationsSqlGenerator.cs` | Runtime SQL generator for `dotnet ef database update` |

### Configuration/ - Feature Subsystems

> When adding new features, follow the same directory structure pattern.

#### Hypertable/ (8 files)
- `HypertableAttribute.cs` - Data annotation: `[Hypertable("TimeColumn", ChunkTimeInterval = "1 day", CompressChunkTimeInterval = "7 days", DisableAutoSparseIndexes = true)]`
- `DimensionAttribute.cs` - Data annotation for additional partitioning dimensions: `[Dimension("Col", EDimensionType.Range, "1 month")]`
- `HypertableConvention.cs` - IEntityTypeAddedConvention implementation; processes `[Hypertable]`, `[Dimension]`, and `[SparseIndex]` attributes
- `HypertableAnnotations.cs` - Annotation constants
- `HypertableTypeBuilder.cs` - Fluent API: `IsHypertable()`, `WithChunkTimeInterval()`, `WithSparseIndex()`, `WithoutAutoSparseIndexes()`, `WithCompressChunkTimeInterval()`, `HasRangeDimension()`, `HasHashDimension()`, etc.
- `SparseIndex.cs` - `SparseIndex` value type and `SparseIndexSelector<TEntity>` typed fluent builder for `.Bloom()`/`.MinMax()` entries
- `SparseIndexAttribute.cs` - `[SparseIndex(ESparseIndexType.Bloom, nameof(Col))]` data annotation; AllowMultiple; `DisableAutoSparseIndexes` on `[Hypertable]` for disabling auto-generated indexes
- `SparseIndexValidationConvention.cs` - IModelFinalizedConvention validating sparse index entries against compression segmentby/orderby constraints, arity rules, and duplicate detection

#### ReorderPolicy/ (5 files)
- `ReorderPolicyAttribute.cs` - Data annotation: `[ReorderPolicy("index_name")]`
- `ReorderPolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `ReorderPolicyAnnotations.cs` - Annotation constants
- `ReorderPolicyTypeBuilder.cs` - Fluent API: `WithReorderPolicy()`; includes a scaffold-targeting overload that takes 5 positional string/int parameters and returns `ReorderPolicyStringBuilder<TEntity>`; also provides a chained overload on `RetentionPolicyStringBuilder<TEntity>` for co-located policy configuration
- `ReorderPolicyStringBuilder.cs` - String-based builder used by scaffolded `OnModelCreating` code; exposes `WithInitialStart(DateTime)` as a chained method (DateTime cannot be rendered as a positional literal via `MethodCallCodeFragment`)

#### RetentionPolicy/ (5 files)
- `RetentionPolicyAttribute.cs` - Data annotation: `[RetentionPolicy(DropAfter = "30 days")]`
- `RetentionPolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `RetentionPolicyAnnotations.cs` - Annotation constants
- `RetentionPolicyTypeBuilder.cs` - Fluent API: `WithRetentionPolicy()`; includes a scaffold-targeting overload that takes 6 positional string parameters and returns `RetentionPolicyStringBuilder<TEntity>`
- `RetentionPolicyStringBuilder.cs` - String-based builder used by scaffolded `OnModelCreating` code; exposes `WithInitialStart(DateTime)` as a chained method (DateTime cannot be rendered as a positional literal via `MethodCallCodeFragment`)

#### CompressionPolicy/ (6 files)
- `CompressionPolicyAttribute.cs` - Data annotation: `[CompressionPolicy(After = "7 days")]`; exactly one of `After`/`CreatedBefore` is required (XOR validated)
- `CompressionPolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `CompressionPolicyAnnotations.cs` - Annotation constants
- `CompressionPolicyTypeBuilder.cs` - Fluent API: `WithCompressionPolicy(after: "7 days", ...)`; optional `scheduleInterval`, `initialStart`, `timezone`, `ifNotExists`; includes scaffold-targeting overload returning `CompressionPolicyStringBuilder<TEntity>`
- `CompressionPolicyStringBuilder.cs` - String-based builder used by scaffolded `OnModelCreating` code; exposes `WithInitialStart(DateTime)` as a chained method
- `CompressionPolicyPrerequisiteValidationConvention.cs` - IModelFinalizedConvention that validates compression is enabled on any continuous aggregate before a compression policy is applied; runs at finalization so all fluent API is visible

#### ContinuousAggregate/ (11 files)
- `ContinuousAggregateAttribute.cs` - Entity-level attribute defining materialized view
- `TimeBucketAttribute.cs` - Property-level attribute for time bucketing
- `AggregateAttribute.cs` - Property-level attribute with `EAggregateFunction` enum
- `GroupByColumnAttribute.cs` - Property-level attribute marking a property as a GROUP BY column
- `ContinuousAggregateConvention.cs` - Processes all attributes above
- `ContinuousAggregateAnnotations.cs` - Annotation constants
- `ContinuousAggregateBuilder<TEntity, TSourceEntity>.cs` - Type-safe generic builder for code-first configuration
- `ContinuousAggregateStringBuilder<TEntity>.cs` - String-based builder used by scaffolded `OnModelCreating` code
- `ContinuousAggregateBuilderCore.cs` - Internal shared annotation-writing logic for both builder types
- `ContinuousAggregateTypeBuilder.cs` - Fluent API extensions (`IsContinuousAggregate`)

#### ContinuousAggregatePolicy/ (7 files)
- `ContinuousAggregatePolicyAttribute.cs` - Data annotation: `[ContinuousAggregatePolicy]`
- `ContinuousAggregatePolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `ContinuousAggregatePolicyAnnotations.cs` - Annotation constants
- `ContinuousAggregatePolicyBuilder.cs` - Typed fluent API builder (code-first)
- `ContinuousAggregatePolicyBuilderCore.cs` - Shared annotation-writing logic for both builder types (mirrors `ContinuousAggregateBuilderCore`)
- `ContinuousAggregatePolicyStringBuilder.cs` - String-based builder used by scaffolded `OnModelCreating` code
- `ContinuousAggregateBuilderPolicyExtensions.cs` - Extension methods for builder

#### Cross-cutting (ContinuousAggregatePolicy + ReorderPolicy + RetentionPolicy)
- `PolicyJobBuilderCore.cs` - Shared base class providing annotation helpers for policy-job fields common to all three policy builder cores (ScheduleInterval, MaxRuntime, MaxRetries, RetryPeriod, InitialStart)

#### Cross-cutting
- `TimeColumnStoreTypeValidationConvention.cs` - IModelFinalizedConvention validating that hypertable and continuous-aggregate time columns resolve to a PostgreSQL time-dimension store type (timestamp/timestamptz/date/integer); backed by `Internals/TimeColumnStoreTypeValidator.cs`

### Abstractions/ - Domain Objects

| File | Purpose |
|------|---------|
| `Dimension.cs` | Represents range/hash partitioning with factory methods |
| `EDimensionType.cs` | Enum: `Range`, `Hash` |
| `EAggregateFunction.cs` | Enum: `Avg`, `Sum`, `Min`, `Max`, `Count`, `First`, `Last` |
| `ESparseIndexType.cs` | Enum: `Bloom`, `MinMax` — identifies the sparse index function |
| `ContinuousAggregateFunction.cs` | Strongly-typed `(Alias, Function, SourceColumn)` for continuous-aggregate columns; `ToAnnotationValue()` serializes to the `alias:Function:sourceColumn` wire format |

### Operations/ - Migration Operations

All inherit `MigrationOperation` and contain feature-specific properties:

- `CreateHypertableOperation.cs` / `AlterHypertableOperation.cs`
- `AddReorderPolicyOperation.cs` / `AlterReorderPolicyOperation.cs` / `DropReorderPolicyOperation.cs`
- `AddRetentionPolicyOperation.cs` / `AlterRetentionPolicyOperation.cs` / `DropRetentionPolicyOperation.cs`
- `CreateContinuousAggregateOperation.cs` / `AlterContinuousAggregateOperation.cs` / `DropContinuousAggregateOperation.cs`
- `AddContinuousAggregatePolicyOperation.cs` / `RemoveContinuousAggregatePolicyOperation.cs`
- `AddCompressionPolicyOperation.cs` / `AlterCompressionPolicyOperation.cs` / `DropCompressionPolicyOperation.cs`

### Query/ - EF.Functions Extensions and LINQ Translators

Provides `EF.Functions` extension methods that translate to TimescaleDB SQL functions at query time.
These are runtime-only — they have no in-memory implementation and throw when called outside LINQ.

| File | Purpose |
|------|---------|
| `TimescaleDbFunctionsExtensions.cs` | Partial class entry point; defines the `Throw<T>()` helper |
| `TimescaleDbFunctionsExtensions.TimeBucket.cs` | 10 `TimeBucket()` overloads covering `DateTime`, `DateTimeOffset`, `DateOnly`, `int`, `long` |
| `Internal/TimescaleDbMethodCallTranslatorPlugin.cs` | `IMethodCallTranslatorPlugin` — registers all translators with EF Core's query pipeline |
| `Internal/TimescaleDbTimeBucketTranslator.cs` | `IMethodCallTranslator` — maps each `TimeBucket` overload to `time_bucket(...)` SQL |

The plugin is registered in `TimescaleDbServiceCollectionExtensions.AddEntityFrameworkTimescaleDb()` via `.TryAdd<IMethodCallTranslatorPlugin, TimescaleDbMethodCallTranslatorPlugin>()`.

### Generators/ - Runtime SQL Generation

Each `*SqlGenerator` exposes `static List<string> Generate(XxxOperation operation)` and returns TimescaleDB SQL statements. `TimescaleDbMigrationsSqlGenerator` switches on the operation type, calls the matching generator, and passes the statements to `SqlBuilderHelper.BuildQueryString(statements, builder, suppressTransaction, usePerform)`. `CreateContinuousAggregateOperation` is emitted with `suppressTransaction: true` (continuous-aggregate DDL cannot run inside a transaction block).

| File | Purpose |
|------|---------|
| `HypertableSqlGenerator.cs` | `create_hypertable()`, `set_chunk_time_interval()`, `add_dimension()`, compression/chunk-skipping SQL |
| `ReorderPolicySqlGenerator.cs` | `add_reorder_policy()`, `remove_reorder_policy()`, `alter_job` tuning |
| `RetentionPolicySqlGenerator.cs` | `add_retention_policy()`, `remove_retention_policy()`, `alter_job` tuning |
| `ContinuousAggregateSqlGenerator.cs` | `CREATE MATERIALIZED VIEW ... WITH (timescaledb.continuous)` plus drop/alter SQL |
| `ContinuousAggregatePolicySqlGenerator.cs` | `add_continuous_aggregate_policy()` / `remove_continuous_aggregate_policy()` |
| `CompressionPolicySqlGenerator.cs` | `CALL add_columnstore_policy()` / `CALL remove_columnstore_policy()` (2.18+ default); falls back to `add_compression_policy` / `remove_compression_policy` when `UseLegacyCompressionSql()` is set |
| `CompressionSettingsSqlHelper.cs` | Shared SQL-building helpers for compression settings: builds the `SET (timescaledb.enable_columnstore = ...)` / `SET (timescaledb.compress = ...)` clause depending on legacy mode, computes changed-settings list for alter operations; used by both hypertable and continuous-aggregate SQL generators |
| `PolicyJobSqlBuilder.cs` | Shared `alter_job` clause builder (schedule interval, max runtime, retries, retry period) used by reorder/retention/CA refresh policies |
| `SqlBuilderHelper.cs` | `Regclass()`, `QualifiedIdentifier()`, `QuoteIdentifier()`, statement grouping, and `SELECT`→`PERFORM` rewriting for idempotent scripts |

### MigrationExtensions/ - Typed migrationBuilder API

Generated migrations call strongly-typed extension methods that construct a `MigrationOperation` and add it to `migrationBuilder.Operations`. Methods are declared in the `Microsoft.EntityFrameworkCore.Migrations` namespace so they are available in migration files without extra `using` directives.

| File | Methods |
|------|---------|
| `HypertableMigrationExtensions.cs` | `CreateHypertable(...)`, `AlterHypertable(...)` |
| `ReorderPolicyMigrationExtensions.cs` | `AddReorderPolicy(...)`, `AlterReorderPolicy(...)`, `DropReorderPolicy(...)` |
| `RetentionPolicyMigrationExtensions.cs` | `AddRetentionPolicy(...)`, `AlterRetentionPolicy(...)`, `DropRetentionPolicy(...)` |
| `ContinuousAggregateMigrationExtensions.cs` | `CreateContinuousAggregate(...)`, `AlterContinuousAggregate(...)`, `DropContinuousAggregate(...)` |
| `ContinuousAggregatePolicyMigrationExtensions.cs` | `AddContinuousAggregatePolicy(...)`, `RemoveContinuousAggregatePolicy(...)` |
| `CompressionPolicyMigrationExtensions.cs` | `AddCompressionPolicy(...)`, `AlterCompressionPolicy(...)`, `DropCompressionPolicy(...)` |

### Internals/ - Core Diffing Logic

- `TimescaleMigrationsModelDiffer.cs` - Extends EF Core's MigrationsModelDiffer; orchestrates the feature differs, builds the `FeatureDiffContext`, implements `GetOperationPriority()`
- `Features/IFeatureDiffer.cs` - Interface: `GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)`
- `Features/FeatureDiffContext.cs` - Cross-cutting diff state passed to every feature differ
- `Features/CompressionDiffHelper.cs` - Shared comparison and rewrite helpers for compression differ logic; used by both hypertable and continuous-aggregate differs; provides `AreStringListsEqual`, `AreOrderByListsEqual`, `NormalizeOrderByEntry`, `RewriteColumns`, and `RewriteOrderByColumns`
- `CompressionAnnotationExtractor.cs` - Shared helpers for extracting segment-by, order-by, and sparse-index column lists from entity-type annotations with CLR property → database column name resolution; used by both hypertable and continuous-aggregate model extractors
- `ExpressionHelper.cs` - Shared static helper: `GetPropertyName<T, TProperty>(Expression)` consolidates lambda-to-property-name extraction across the fluent API
- `ParentEntityTypeResolver.cs` - Resolves a continuous aggregate's parent `IEntityType` by matching CLR class name, EF Core short name, or database table name; handles both code-first and scaffolded models

**Feature-specific:**
- `Features/Hypertables/` - `HypertableDiffer.cs`, `HypertableModelExtractor.cs`
- `Features/ReorderPolicies/` - `ReorderPolicyDiffer.cs`, `ReorderPolicyModelExtractor.cs`
- `Features/RetentionPolicies/` - `RetentionPolicyDiffer.cs`, `RetentionPolicyModelExtractor.cs`
- `Features/ContinuousAggregates/` - `ContinuousAggregateDiffer.cs`, `ContinuousAggregateModelExtractor.cs`
- `Features/ContinuousAggregatePolicies/` - `ContinuousAggregatePolicyDiffer.cs`, `ContinuousAggregatePolicyModelExtractor.cs`
- `Features/CompressionPolicies/` - `CompressionPolicyDiffer.cs`, `CompressionPolicyModelExtractor.cs`, `CompressionPolicyDefaultHelper.cs`

#### FeatureDiffContext

`TimescaleMigrationsModelDiffer` runs EF Core's base differ first, builds a `FeatureDiffContext` from the resulting operations, and passes it to every feature differ. It carries:

- **TableRenames / IndexRenames / ColumnRenames** - maps built from EF's `RenameTableOperation` / `RenameIndexOperation` / `RenameColumnOperation` so feature differs treat a rename as a rename rather than drop-and-create. Schemas are normalized to `DefaultValues.DefaultSchema`. Resolve via `ResolveTable()`, `ResolveIndex()`, `ResolveColumn()`.
- **RecreatedAggregates** - continuous aggregates being dropped and recreated in this diff, populated by `PopulateRecreatedAggregates` after the continuous-aggregate differ runs. Recreating a continuous aggregate cascades to drop its refresh and retention policies, so dependent policy differs re-add those policies even when their config is unchanged.
- `FeatureDiffContext.Empty` - identity context used when a differ runs without orchestration (e.g. unit tests).

### DefaultValues.cs - Centralized Constants

```csharp
DefaultSchema = "public"
ChunkTimeInterval = "7 days" // ChunkTimeIntervalLong = 604_800_000_000L
ReorderPolicyScheduleInterval = "1 day"
ReorderPolicyMaxRetries = -1 // indefinite
ReorderPolicyMaxRuntime = "00:00:00" // no limit
```

## Design Library Structure

### TimescaleDBDesignTimeServices.cs

- Configured with `[assembly: DesignTimeProviderServices(...)]` attribute
- Registers:
  - `ICSharpMigrationOperationGenerator` → `TimescaleCSharpMigrationOperationGenerator`
  - `IDatabaseModelFactory` → `TimescaleDatabaseModelFactory`
  - `IAnnotationCodeGenerator` → `TimescaleDbAnnotationCodeGenerator`
  - `IModelCodeGeneratorSelector` → `TimescaleModelCodeGeneratorSelector`

### TimescaleCSharpMigrationOperationGenerator.cs

- Generates C# code for `dotnet ef migrations add`
- Switches on the operation type and delegates to the matching `*CSharpGenerator` (constructed with `Dependencies.CSharpHelper`)
- Emits typed `migrationBuilder.CreateHypertable(...)` / `AddRetentionPolicy(...)` / etc. calls in migration Up/Down methods

### Generators/ - Design-Time C# Generation

#### Migration code generation

Each `*CSharpGenerator.Generate(XxxOperation, IndentedStringBuilder)` emits one typed `migrationBuilder` call, with one named argument per line.

| File | Purpose |
|------|---------|
| `HypertableCSharpGenerator.cs` | Emits `CreateHypertable(...)` / `AlterHypertable(...)` |
| `ReorderPolicyCSharpGenerator.cs` | Emits `AddReorderPolicy(...)` / `AlterReorderPolicy(...)` / `DropReorderPolicy(...)` |
| `RetentionPolicyCSharpGenerator.cs` | Emits `AddRetentionPolicy(...)` / `AlterRetentionPolicy(...)` / `DropRetentionPolicy(...)` |
| `ContinuousAggregateCSharpGenerator.cs` | Emits `CreateContinuousAggregate(...)` / `AlterContinuousAggregate(...)` / `DropContinuousAggregate(...)` |
| `ContinuousAggregatePolicyCSharpGenerator.cs` | Emits `AddContinuousAggregatePolicy(...)` / `RemoveContinuousAggregatePolicy(...)` |
| `CompressionPolicyCSharpGenerator.cs` | Emits `AddCompressionPolicy(...)` / `AlterCompressionPolicy(...)` / `DropCompressionPolicy(...)` |
| `MigrationCallWriter.cs` | `IDisposable` helper that writes a `.Method(` call and named `arg: value` lines |
| `CSharpGeneratorHelper.cs` | `LiteralStringList()` for `["a", "b"]` collection expressions and `StaticCall()` for `Type.Method(args)` literals |

#### Annotation code generation (scaffolding phase 2)

Converts `DatabaseModel` annotations to C# fluent API calls or data annotation attributes in scaffolded entity files.

| File | Purpose |
|------|---------|
| `TimescaleModelCodeGeneratorSelector.cs` | Selects `TimescaleCSharpModelGenerator` over EF Core's default `CSharpModelGenerator` |
| `TimescaleCSharpModelGenerator.cs` | Wraps base model generator; injects TimescaleDB `using` directives when `UseDataAnnotations = true` |
| `TimescaleDbAnnotationCodeGenerator.cs` | `IAnnotationCodeGenerator` implementation; dispatches to `IFeatureAnnotationRenderer` instances |
| `TimescaleCSharpHelper.cs` | Extends `ICSharpHelper.UnknownLiteral` to render `NameOfCodeFragment` and mixed `object?[]` arrays |
| `AnnotationRenderers/IFeatureAnnotationRenderer.cs` | Per-feature renderer interface: `GenerateFluentApiCalls` + `GenerateDataAnnotationAttributes` |
| `AnnotationRenderers/HypertableAnnotationRenderer.cs` | Renders hypertable and dimension annotations to fluent API or data annotation attributes |
| `AnnotationRenderers/ContinuousAggregateAnnotationRenderer.cs` | Renders continuous aggregate annotations; parses the stored view definition via `ViewDefinitionParser` to reconstruct structured configuration |
| `AnnotationRenderers/ContinuousAggregatePolicyAnnotationRenderer.cs` | Renders continuous aggregate policy annotations to `WithRefreshPolicy(...)` fluent API or `[ContinuousAggregatePolicy]` attribute |
| `AnnotationRenderers/RetentionPolicyAnnotationRenderer.cs` | Renders retention policy annotations to `WithRetentionPolicy(...)` fluent API or `[RetentionPolicy]` attribute; `ShouldRender` guard requires the parent renderer (hypertable or continuous aggregate) to have already consumed its annotation |
| `AnnotationRenderers/ReorderPolicyAnnotationRenderer.cs` | Renders reorder policy annotations to `WithReorderPolicy(...)` fluent API or `[ReorderPolicy]` attribute; `ShouldRender` guard requires the hypertable renderer to have already consumed its annotation |
| `AnnotationRenderers/CompressionPolicyAnnotationRenderer.cs` | Renders compression policy annotations to `WithCompressionPolicy(...)` fluent API or `[CompressionPolicy]` attribute; `ShouldRender` guard requires the hypertable renderer to have already consumed its annotation |
| `AnnotationRenderers/PolicyJobRendererHelper.cs` | Shared static helpers for rendering optional policy-job fields (InitialStart, ScheduleInterval, MaxRuntime, etc.) shared across all policy renderers |
| `AnnotationRenderers/AnnotationRendererHelper.cs` | Static helpers: `Find`, `GetString`, `SplitColumns`, `Consume`, `ResolvePropertyName`, `TryResolvePropertyName`, `ResolveColumns` |
| `AnnotationRenderers/NameOfCodeFragment.cs` | Custom `CodeFragment` record: renders as `nameof(Property)` or `$"{nameof(Property)} DESC"` |

### Scaffolding Pipeline

`dotnet ef dbcontext scaffold` runs in two phases:

**Phase 1 — Database extraction** (`TimescaleDatabaseModelFactory.cs` + `Scaffolding/`):
`TimescaleDatabaseModelFactory` overrides NpgsqlDatabaseModelFactory. After the base factory builds the `DatabaseModel` from the database schema, it calls each extractor/applier pair to layer TimescaleDB metadata on top as annotations. All interval fields are normalized to humanized units (e.g. `"1 hour"`) via `IntervalParsingHelper.NormalizeInterval` to avoid phantom migrations from PostgreSQL's `HH:MM:SS` rendering:
- `HypertableScaffoldingExtractor` + `HypertableAnnotationApplier` — hypertable config, dimensions, chunk time interval
- `ReorderPolicyScaffoldingExtractor` + `ReorderPolicyAnnotationApplier`
- `RetentionPolicyScaffoldingExtractor` + `RetentionPolicyAnnotationApplier`
- `ContinuousAggregateScaffoldingExtractor` + `ContinuousAggregateAnnotationApplier`
- `ContinuousAggregatePolicyScaffoldingExtractor` + `ContinuousAggregatePolicyAnnotationApplier`
- `CompressionPolicyScaffoldingExtractor` + `CompressionPolicyAnnotationApplier` — reads jobs from `timescaledb_information.jobs` joined with `_timescaledb_config.bgw_job` for timezone; applier suppresses default schedule intervals
- `CompressionSettingsScaffoldingHelper` — shared helper that reads `timescaledb_information.hypertable_columnstore_settings` (2.18+) with fallback to `compression_settings` (pre-2.18); provides segmentby, orderby, sparse index, and compress_chunk_time_interval; used by both `HypertableScaffoldingExtractor` and `ContinuousAggregateScaffoldingExtractor`

**Phase 2 — Annotation code generation** (`TimescaleDbAnnotationCodeGenerator` + `AnnotationRenderers/`):
EF Core's scaffolding pipeline calls `TimescaleDbAnnotationCodeGenerator` to convert those annotations into C# code. The dispatcher iterates its registered `IFeatureAnnotationRenderer` implementations:
- When `UseDataAnnotations = false` → `GenerateFluentApiCalls` → fluent API method chains in `OnModelCreating`
- When `UseDataAnnotations = true` → `GenerateDataAnnotationAttributes` → `[Attribute]` declarations on entity classes

Registered renderers: `HypertableAnnotationRenderer`, `ContinuousAggregateAnnotationRenderer`, `ContinuousAggregatePolicyAnnotationRenderer`, `RetentionPolicyAnnotationRenderer`, `ReorderPolicyAnnotationRenderer`, `CompressionPolicyAnnotationRenderer`. Registration order matters: child renderers (`ContinuousAggregatePolicyAnnotationRenderer`, `RetentionPolicyAnnotationRenderer`, `ReorderPolicyAnnotationRenderer`, `CompressionPolicyAnnotationRenderer`) must run after their respective parent renderers so the `ShouldRender` guard can verify the parent annotation was consumed.

`TimescaleCSharpModelGenerator` wraps EF Core's standard model generator and post-processes the generated files to inject missing `using` directives for TimescaleDB attribute namespaces. `TimescaleModelCodeGeneratorSelector` ensures this custom generator is selected.

**Additional Design-Time Utilities:**
- `Scaffolding/ViewDefinitionParser.cs` - Parses a continuous aggregate's stored view definition SQL (best-effort, cached) to extract `TimeBucketWidth`, `TimeBucketSourceColumn`, aggregate functions, GROUP BY columns, and WHERE clause; used by `ContinuousAggregateAnnotationRenderer`

**Scaffolding/ Interfaces:**
- `ITimescaleFeatureExtractor.cs` - `Extract(DbConnection connection)` returns feature metadata
- `IAnnotationApplier.cs` - `ApplyAnnotations(DatabaseTable table, object featureInfo)`

### build/CmdScale.EntityFrameworkCore.TimescaleDB.Design.targets

- MSBuild integration that injects DesignTimeServicesReference attribute
- Generates `GeneratedTimescaleDesignTimeServices.g.cs` during compile
- Enables `dotnet ef` CLI tools to discover design-time services

## Migration Operation Priority Ordering

Custom operations are sorted by `TimescaleMigrationsModelDiffer.GetOperationPriority()`. Drop operations get negative priorities (run before standard EF table drops, in reverse dependency order); add/alter operations get positive priorities (run after standard EF table creation, in dependency order).

| Priority | Operation Type |
|----------|---------------|
| -60 | `DropRetentionPolicyOperation` |
| -50 | `RemoveContinuousAggregatePolicyOperation` |
| -40 | `DropContinuousAggregateOperation` |
| -25 | `DropCompressionPolicyOperation` |
| -20 | `DropReorderPolicyOperation` |
| 0 | Standard EF operations (CreateTable, AddColumn, DropTable, …) |
| 10 | `CreateHypertableOperation` |
| 15 | `AlterHypertableOperation` |
| 20 | `AddReorderPolicyOperation` / `AlterReorderPolicyOperation` |
| 25 | `AddCompressionPolicyOperation` / `AlterCompressionPolicyOperation` |
| 30 | `CreateContinuousAggregateOperation` |
| 40 | `AlterContinuousAggregateOperation` |
| 50 | `AddContinuousAggregatePolicyOperation` |
| 60 | `AddRetentionPolicyOperation` / `AlterRetentionPolicyOperation` |

## Continuous Aggregates Implementation Details

Continuous aggregates are materialized views that automatically refresh:

- **MaterializedViewName:** Name of the generated materialized view
- **ParentName:** Entity name of source hypertable (resolved to table name via EF metadata)
- **TimeBucketWidth:** Time interval for bucketing (e.g., "1 day", "1 hour")
- **TimeBucketSourceColumn:** Time column to bucket on (resolved to database column name)
- **AggregateFunctions:** `ContinuousAggregateFunction` values in the typed API; stored as colon-delimited strings on the operation (see patterns.md)
- **GroupByColumns:** Column names for GROUP BY
- **WhereClause:** Raw SQL for filtering, emitted verbatim into the materialized view's `WHERE`. Identifiers are passed through unchanged, so quoted column references must match the resolved database column names.

**SQL Generation Special Cases:**
- `first()`/`last()` functions require time ordering column: `first(price, timestamp ORDER BY timestamp)`
- `time_bucket()` function wraps time column in SELECT and GROUP BY
- Aggregate column aliases must match property names for EF mapping

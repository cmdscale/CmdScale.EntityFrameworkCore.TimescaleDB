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
| `TimescaleDbContextOptionsBuilderExtensions.cs` | Service registration via `UseTimescaleDb()` |
| `TimescaleDbMigrationsSqlGenerator.cs` | Runtime SQL generator for `dotnet ef database update` |

### Configuration/ - Feature Subsystems

> When adding new features, follow the same directory structure pattern.

#### Hypertable/ (4 files)
- `HypertableAttribute.cs` - Data annotation: `[Hypertable("TimeColumn", ChunkTimeInterval = "1 day")]`
- `HypertableConvention.cs` - IEntityTypeAddedConvention implementation
- `HypertableAnnotations.cs` - Annotation constants
- `HypertableTypeBuilder.cs` - Fluent API: `IsHypertable()`, `WithChunkTimeInterval()`, etc.

#### ReorderPolicy/ (3 files)
- `ReorderPolicyAttribute.cs` - Data annotation: `[ReorderPolicy("index_name")]`
- `ReorderPolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `ReorderPolicyAnnotations.cs` - Annotation constants
- `ReorderPolicyTypeBuilder.cs` - Fluent API: `WithReorderPolicy()`

#### RetentionPolicy/ (4 files)
- `RetentionPolicyAttribute.cs` - Data annotation: `[RetentionPolicy(DropAfter = "30 days")]`
- `RetentionPolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `RetentionPolicyAnnotations.cs` - Annotation constants
- `RetentionPolicyTypeBuilder.cs` - Fluent API: `WithRetentionPolicy()`

#### ContinuousAggregate/ (8 files)
- `ContinuousAggregateAttribute.cs` - Entity-level attribute defining materialized view
- `TimeBucketAttribute.cs` - Property-level attribute for time bucketing
- `AggregateAttribute.cs` - Property-level attribute with `EAggregateFunction` enum
- `ContinuousAggregateConvention.cs` - Processes all three attributes above
- `ContinuousAggregateAnnotations.cs` - 13 annotation constants
- `ContinuousAggregateBuilder<TEntity, TSourceEntity>.cs` - Type-safe generic builder
- `ContinuousAggregateTypeBuilder.cs` - Fluent API extensions

#### ContinuousAggregatePolicy/ (5 files)
- `ContinuousAggregatePolicyAttribute.cs` - Data annotation: `[ContinuousAggregatePolicy]`
- `ContinuousAggregatePolicyConvention.cs` - IEntityTypeAddedConvention implementation
- `ContinuousAggregatePolicyAnnotations.cs` - Annotation constants
- `ContinuousAggregatePolicyBuilder.cs` - Fluent API builder
- `ContinuousAggregateBuilderPolicyExtensions.cs` - Extension methods for builder

#### Cross-cutting
- `TimeColumnStoreTypeValidationConvention.cs` - IModelFinalizedConvention validating that hypertable and continuous-aggregate time columns resolve to a PostgreSQL time-dimension store type (timestamp/timestamptz/date/integer); backed by `Internals/TimeColumnStoreTypeValidator.cs`

### Abstractions/ - Domain Objects

| File | Purpose |
|------|---------|
| `Dimension.cs` | Represents range/hash partitioning with factory methods |
| `EDimensionType.cs` | Enum: `Range`, `Hash` |
| `EAggregateFunction.cs` | Enum: `Avg`, `Sum`, `Min`, `Max`, `Count`, `First`, `Last` |
| `ContinuousAggregateFunction.cs` | Strongly-typed `(Alias, Function, SourceColumn)` for continuous-aggregate columns; `ToAnnotationValue()` serializes to the `alias:Function:sourceColumn` wire format |

### Operations/ - Migration Operations

All inherit `MigrationOperation` and contain feature-specific properties:

- `CreateHypertableOperation.cs` / `AlterHypertableOperation.cs`
- `AddReorderPolicyOperation.cs` / `AlterReorderPolicyOperation.cs` / `DropReorderPolicyOperation.cs`
- `AddRetentionPolicyOperation.cs` / `AlterRetentionPolicyOperation.cs` / `DropRetentionPolicyOperation.cs`
- `CreateContinuousAggregateOperation.cs` / `AlterContinuousAggregateOperation.cs` / `DropContinuousAggregateOperation.cs`
- `AddContinuousAggregatePolicyOperation.cs` / `RemoveContinuousAggregatePolicyOperation.cs`

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

### Internals/ - Core Diffing Logic

- `TimescaleMigrationsModelDiffer.cs` - Extends EF Core's MigrationsModelDiffer; orchestrates the feature differs, builds the `FeatureDiffContext`, implements `GetOperationPriority()`
- `Features/IFeatureDiffer.cs` - Interface: `GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)`
- `Features/FeatureDiffContext.cs` - Cross-cutting diff state passed to every feature differ

**Feature-specific:**
- `Features/Hypertables/` - `HypertableDiffer.cs`, `HypertableModelExtractor.cs`
- `Features/ReorderPolicies/` - `ReorderPolicyDiffer.cs`, `ReorderPolicyModelExtractor.cs`
- `Features/RetentionPolicies/` - `RetentionPolicyDiffer.cs`, `RetentionPolicyModelExtractor.cs`
- `Features/ContinuousAggregates/` - `ContinuousAggregateDiffer.cs`, `ContinuousAggregateModelExtractor.cs`
- `Features/ContinuousAggregatePolicies/` - `ContinuousAggregatePolicyDiffer.cs`, `ContinuousAggregatePolicyModelExtractor.cs`

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

### TimescaleCSharpMigrationOperationGenerator.cs

- Generates C# code for `dotnet ef migrations add`
- Switches on the operation type and delegates to the matching `*CSharpGenerator` (constructed with `Dependencies.CSharpHelper`)
- Emits typed `migrationBuilder.CreateHypertable(...)` / `AddRetentionPolicy(...)` / etc. calls in migration Up/Down methods

### Generators/ - Design-Time C# Generation

Each `*CSharpGenerator.Generate(XxxOperation, IndentedStringBuilder)` emits one typed `migrationBuilder` call, with one named argument per line.

| File | Purpose |
|------|---------|
| `HypertableCSharpGenerator.cs` | Emits `CreateHypertable(...)` / `AlterHypertable(...)` |
| `ReorderPolicyCSharpGenerator.cs` | Emits `AddReorderPolicy(...)` / `AlterReorderPolicy(...)` / `DropReorderPolicy(...)` |
| `RetentionPolicyCSharpGenerator.cs` | Emits `AddRetentionPolicy(...)` / `AlterRetentionPolicy(...)` / `DropRetentionPolicy(...)` |
| `ContinuousAggregateCSharpGenerator.cs` | Emits `CreateContinuousAggregate(...)` / `AlterContinuousAggregate(...)` / `DropContinuousAggregate(...)` |
| `ContinuousAggregatePolicyCSharpGenerator.cs` | Emits `AddContinuousAggregatePolicy(...)` / `RemoveContinuousAggregatePolicy(...)` |
| `MigrationCallWriter.cs` | `IDisposable` helper that writes a `.Method(` call and named `arg: value` lines |
| `CSharpGeneratorHelper.cs` | `LiteralStringList()` for `["a", "b"]` collection expressions and `StaticCall()` for `Type.Method(args)` literals |

### TimescaleDatabaseModelFactory.cs

Orchestrates db-first scaffolding with extractor/applier pairs:
- `HypertableScaffoldingExtractor` + `HypertableAnnotationApplier`
- `ReorderPolicyScaffoldingExtractor` + `ReorderPolicyAnnotationApplier`
- `ContinuousAggregateScaffoldingExtractor` + `ContinuousAggregateAnnotationApplier`

### Scaffolding/

**Interfaces:**
- `ITimescaleFeatureExtractor.cs` - `Extract(DbConnection connection)` returns feature metadata
- `IAnnotationApplier.cs` - `ApplyAnnotations(DatabaseTable table, object featureInfo)`

**Feature Extractors** query TimescaleDB system tables:
- `HypertableScaffoldingExtractor.cs` - Queries `timescaledb_information.hypertables`, dimensions, chunk stats
- `ReorderPolicyScaffoldingExtractor.cs` - Queries `timescaledb_information.jobs`
- `ContinuousAggregateScaffoldingExtractor.cs` - Queries continuous aggregate metadata

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
| -20 | `DropReorderPolicyOperation` |
| 0 | Standard EF operations (CreateTable, AddColumn, DropTable, …) |
| 10 | `CreateHypertableOperation` |
| 15 | `AlterHypertableOperation` |
| 20 | `AddReorderPolicyOperation` / `AlterReorderPolicyOperation` |
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

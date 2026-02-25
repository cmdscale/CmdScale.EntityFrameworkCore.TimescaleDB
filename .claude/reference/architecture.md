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

### Abstractions/ - Domain Objects

| File | Purpose |
|------|---------|
| `Dimension.cs` | Represents range/hash partitioning with factory methods |
| `EDimensionType.cs` | Enum: `Range`, `Hash` |
| `EAggregateFunction.cs` | Enum: `Avg`, `Sum`, `Min`, `Max`, `Count`, `First`, `Last` |W

### Operations/ - Migration Operations

All inherit `MigrationOperation` and contain feature-specific properties:

- `CreateHypertableOperation.cs` / `AlterHypertableOperation.cs`
- `AddReorderPolicyOperation.cs` / `AlterReorderPolicyOperation.cs` / `DropReorderPolicyOperation.cs`
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

### Generators/ - SQL and C# Code Generation

| File | Purpose |
|------|---------|
| `HypertableOperationGenerator.cs` | Generates `create_hypertable()`, `set_chunk_time_interval()`, etc. |
| `ReorderPolicyOperationGenerator.cs` | Generates `add_reorder_policy()`, `remove_reorder_policy()`, etc. |
| `ContinuousAggregateOperationGenerator.cs` | Generates materialized view SQL |
| `SqlBuilderHelper.cs` | Quote handling utilities (`isDesignTime` parameter critical) |

### Internals/ - Core Diffing Logic

- `TimescaleMigrationsModelDiffer.cs` - Extends EF Core's MigrationsModelDiffer, implements `GetOperationPriority()`
- `Features/IFeatureDiffer.cs` - Interface: `GetDifferences(IRelationalModel? source, IRelationalModel? target)`

**Feature-specific:**
- `Features/Hypertables/` - `HypertableDiffer.cs`, `HypertableModelExtractor.cs`
- `Features/ReorderPolicies/` - `ReorderPolicyDiffer.cs`, `ReorderPolicyModelExtractor.cs`
- `Features/ContinuousAggregates/` - `ContinuousAggregateDiffer.cs`, `ContinuousAggregateModelExtractor.cs`
- `Features/ContinuousAggregatePolicies/` - `ContinuousAggregatePolicyDiffer.cs`, `ContinuousAggregatePolicyModelExtractor.cs`

### DefaultValues.cs - Centralized Constants

```csharp
DefaultSchema = "public"
ChunkTimeInterval = "7 days" // ChunkTimeIntervalLong = 604_800_000_000L
ReorderPolicyScheduleInterval = "1 day"
ReorderPolicyMaxRetries = -1 // indefinite
ReorderPolicyMaxRuntime = "00:00:00" // no limit
ReorderPolicyRetryPeriod = "00:05:00"
```

## Design Library Structure

### TimescaleDBDesignTimeServices.cs

- Configured with `[assembly: DesignTimeProviderServices(...)]` attribute
- Registers:
  - `ICSharpMigrationOperationGenerator` → `TimescaleCSharpMigrationOperationGenerator`
  - `IDatabaseModelFactory` → `TimescaleDatabaseModelFactory`

### TimescaleCSharpMigrationOperationGenerator.cs

- Generates C# code for `dotnet ef migrations add`
- Calls operation generators with `isDesignTime: true`
- Outputs `.Sql(@"...")` calls in migration Up/Down methods

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

Custom operations are prioritized by `TimescaleMigrationsModelDiffer.GetOperationPriority()`:

| Priority | Operation Type | Reason |
|----------|---------------|--------|
| 0 | Standard EF operations | CreateTable, AddColumn, DropColumn, etc. |
| 10 | `CreateHypertableOperation` | Tables must exist first |
| 20 | Reorder policy operations | Hypertables must exist |
| 30 | `CreateContinuousAggregateOperation` | Source hypertables must exist |
| 40 | Alter/Drop continuous aggregate | Last to ensure dependencies exist |

## Continuous Aggregates Implementation Details

Continuous aggregates are materialized views that automatically refresh:

- **MaterializedViewName:** Name of the generated materialized view
- **ParentName:** Entity name of source hypertable (resolved to table name via EF metadata)
- **TimeBucketWidth:** Time interval for bucketing (e.g., "1 day", "1 hour")
- **TimeBucketSourceColumn:** Time column to bucket on (resolved to database column name)
- **AggregateFunctions:** Colon-delimited strings (see patterns.md)
- **GroupByColumns:** Column names for GROUP BY
- **WhereClause:** Raw SQL for filtering (partially implemented)

**SQL Generation Special Cases:**
- `first()`/`last()` functions require time ordering column: `first(price, timestamp ORDER BY timestamp)`
- `time_bucket()` function wraps time column in SELECT and GROUP BY
- Aggregate column aliases must match property names for EF mapping

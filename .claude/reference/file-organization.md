# File Organization Reference

Quick reference for locating key files in the CmdScale.EntityFrameworkCore.TimescaleDB library.

> This listing may lag behind the actual source. Check `src/Eftdb/Configuration/` and `src/Eftdb/Internals/Features/` for the authoritative list.

## Core Library Key Files

### Entry Points

| File | Purpose |
|------|---------|
| `TimescaleDbServiceCollectionExtensions.cs` | DI registration |
| `TimescaleDbContextOptionsBuilderExtensions.cs` | Service registration via UseTimescaleDb() |
| `TimescaleDbMigrationsSqlGenerator.cs` | Runtime SQL generation |

### Hypertable

| File | Purpose |
|------|---------|
| `Configuration/Hypertable/HypertableTypeBuilder.cs` | Fluent API |
| `Configuration/Hypertable/HypertableAnnotations.cs` | Annotation constants |
| `Configuration/Hypertable/HypertableAttribute.cs` | Data annotation |
| `Configuration/Hypertable/HypertableConvention.cs` | Convention processing |
| `Internals/Features/Hypertables/HypertableDiffer.cs` | Diffing logic |
| `Internals/Features/Hypertables/HypertableModelExtractor.cs` | Model extraction |
| `Generators/HypertableOperationGenerator.cs` | SQL/C# generation |
| `Operations/CreateHypertableOperation.cs` | Migration operation |
| `Operations/AlterHypertableOperation.cs` | Migration operation |

### Reorder Policy

| File | Purpose |
|------|---------|
| `Configuration/ReorderPolicy/ReorderPolicyTypeBuilder.cs` | Fluent API |
| `Configuration/ReorderPolicy/ReorderPolicyAnnotations.cs` | Annotation constants |
| `Configuration/ReorderPolicy/ReorderPolicyAttribute.cs` | Data annotation |
| `Configuration/ReorderPolicy/ReorderPolicyConvention.cs` | Convention processing |
| `Internals/Features/ReorderPolicies/ReorderPolicyDiffer.cs` | Diffing logic |
| `Internals/Features/ReorderPolicies/ReorderPolicyModelExtractor.cs` | Model extraction |
| `Generators/ReorderPolicyOperationGenerator.cs` | SQL/C# generation |
| `Operations/AddReorderPolicyOperation.cs` | Migration operation |
| `Operations/AlterReorderPolicyOperation.cs` | Migration operation |
| `Operations/DropReorderPolicyOperation.cs` | Migration operation |

### Continuous Aggregate

| File | Purpose |
|------|---------|
| `Configuration/ContinuousAggregate/ContinuousAggregateBuilder.cs` | Type-safe builder |
| `Configuration/ContinuousAggregate/ContinuousAggregateTypeBuilder.cs` | Fluent API extensions |
| `Configuration/ContinuousAggregate/ContinuousAggregateAnnotations.cs` | Annotation constants |
| `Configuration/ContinuousAggregate/ContinuousAggregateAttribute.cs` | Entity-level attribute |
| `Configuration/ContinuousAggregate/TimeBucketAttribute.cs` | Property-level attribute |
| `Configuration/ContinuousAggregate/AggregateAttribute.cs` | Property-level attribute |
| `Configuration/ContinuousAggregate/ContinuousAggregateConvention.cs` | Convention processing |
| `Internals/Features/ContinuousAggregates/ContinuousAggregateDiffer.cs` | Diffing logic |
| `Internals/Features/ContinuousAggregates/ContinuousAggregateModelExtractor.cs` | Model extraction |
| `Generators/ContinuousAggregateOperationGenerator.cs` | SQL/C# generation |
| `Operations/CreateContinuousAggregateOperation.cs` | Migration operation |
| `Operations/AlterContinuousAggregateOperation.cs` | Migration operation |
| `Operations/DropContinuousAggregateOperation.cs` | Migration operation |

### Continuous Aggregate Policy

| File | Purpose |
|------|---------|
| `Configuration/ContinuousAggregatePolicy/ContinuousAggregatePolicyAnnotations.cs` | Annotation constants |
| `Configuration/ContinuousAggregatePolicy/ContinuousAggregatePolicyAttribute.cs` | Data annotation |
| `Configuration/ContinuousAggregatePolicy/ContinuousAggregatePolicyConvention.cs` | Convention processing |
| `Configuration/ContinuousAggregatePolicy/ContinuousAggregatePolicyBuilder.cs` | Fluent API builder |
| `Configuration/ContinuousAggregatePolicy/ContinuousAggregateBuilderPolicyExtensions.cs` | Builder extensions |
| `Internals/Features/ContinuousAggregatePolicies/ContinuousAggregatePolicyDiffer.cs` | Diffing logic |
| `Internals/Features/ContinuousAggregatePolicies/ContinuousAggregatePolicyModelExtractor.cs` | Model extraction |
| `Operations/AddContinuousAggregatePolicyOperation.cs` | Migration operation |
| `Operations/RemoveContinuousAggregatePolicyOperation.cs` | Migration operation |

### Query Functions

| File | Purpose |
|------|---------|
| `Query/TimescaleDbFunctionsExtensions.cs` | EF.Functions extension entry point (partial class stub) |
| `Query/TimescaleDbFunctionsExtensions.TimeBucket.cs` | `EF.Functions.TimeBucket()` overloads |
| `Query/Internal/TimescaleDbMethodCallTranslatorPlugin.cs` | Registers method call translators with EF Core |
| `Query/Internal/TimescaleDbTimeBucketTranslator.cs` | Translates `TimeBucket` calls to `time_bucket` SQL |

### Coordination & Utilities

| File | Purpose |
|------|---------|
| `Internals/TimescaleMigrationsModelDiffer.cs` | Operation prioritization |
| `Internals/Features/IFeatureDiffer.cs` | Differ interface |
| `Generators/SqlBuilderHelper.cs` | Quote handling, regclass |
| `DefaultValues.cs` | Centralized defaults |
| `Abstractions/Dimension.cs` | Range/hash partitioning |
| `Abstractions/EAggregateFunction.cs` | Aggregate function enum |

## Design Library Key Files

| File | Purpose |
|------|---------|
| `TimescaleDBDesignTimeServices.cs` | Register design-time services |
| `TimescaleCSharpMigrationOperationGenerator.cs` | C# code generation for migrations |
| `TimescaleDatabaseModelFactory.cs` | Db-first scaffolding orchestration |
| `Scaffolding/ITimescaleFeatureExtractor.cs` | Extractor interface |
| `Scaffolding/IAnnotationApplier.cs` | Applier interface |
| `Scaffolding/HypertableScaffoldingExtractor.cs` | Query hypertables from database |
| `Scaffolding/HypertableAnnotationApplier.cs` | Apply hypertable annotations |
| `Scaffolding/ReorderPolicyScaffoldingExtractor.cs` | Query reorder policies from database |
| `Scaffolding/ReorderPolicyAnnotationApplier.cs` | Apply reorder policy annotations |
| `Scaffolding/ContinuousAggregateScaffoldingExtractor.cs` | Query continuous aggregates |
| `Scaffolding/ContinuousAggregateAnnotationApplier.cs` | Apply continuous aggregate annotations |
| `build/CmdScale.EntityFrameworkCore.TimescaleDB.Design.targets` | MSBuild integration |

## Test Files

| Directory | Purpose |
|-----------|---------|
| `tests/Eftdb.Tests/` | Unit tests (xUnit, Moq) |
| `tests/Eftdb.FunctionalTests/` | Integration tests (Testcontainers) |

## Sample Files

| Directory | Purpose |
|-----------|---------|
| `samples/Eftdb.Samples.Shared/` | Shared models and configurations |
| `samples/Eftdb.Samples.CodeFirst/` | Code-first migration examples |
| `samples/Eftdb.Samples.DatabaseFirst/` | Database-first scaffolding examples |

## Directory Structure Overview

```
src/
├── Eftdb/                  # Core runtime library (CmdScale.EntityFrameworkCore.TimescaleDB)
│   ├── Abstractions/       # Domain objects (Dimension, enums)
│   ├── Configuration/      # Fluent API, attributes, conventions
│   │   ├── ContinuousAggregate/
│   │   ├── ContinuousAggregatePolicy/
│   │   ├── Hypertable/
│   │   └── ReorderPolicy/
│   ├── Generators/         # SQL and C# code generation
│   ├── Internals/          # Core diffing logic
│   │   └── Features/
│   │       ├── ContinuousAggregates/
│   │       ├── ContinuousAggregatePolicies/
│   │       ├── Hypertables/
│   │       └── ReorderPolicies/
│   ├── Operations/         # Migration operations
│   ├── Query/              # EF.Functions extensions and LINQ translators
│   │   └── Internal/       # EF Core query pipeline integration
│   └── *.cs                # Entry points, extensions
│
└── Eftdb.Design/           # Design-time library (CmdScale.EntityFrameworkCore.TimescaleDB.Design)
    ├── Scaffolding/        # Extractors and appliers
    ├── build/              # MSBuild targets
    └── *.cs                # Design-time services

tests/
├── Eftdb.Tests/            # Unit tests
└── Eftdb.FunctionalTests/  # Integration tests

samples/
├── Eftdb.Samples.Shared/   # Shared models
├── Eftdb.Samples.CodeFirst/ # Code-first examples
└── Eftdb.Samples.DatabaseFirst/ # Database-first examples

benchmarks/
└── Eftdb.Benchmarks/       # Performance benchmarks
```

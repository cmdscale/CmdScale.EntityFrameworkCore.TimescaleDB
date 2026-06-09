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
| `Generators/HypertableSqlGenerator.cs` | Runtime SQL generation |
| `MigrationExtensions/HypertableMigrationExtensions.cs` | Typed migrationBuilder methods |
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
| `Generators/ReorderPolicySqlGenerator.cs` | Runtime SQL generation |
| `MigrationExtensions/ReorderPolicyMigrationExtensions.cs` | Typed migrationBuilder methods |
| `Operations/AddReorderPolicyOperation.cs` | Migration operation |
| `Operations/AlterReorderPolicyOperation.cs` | Migration operation |
| `Operations/DropReorderPolicyOperation.cs` | Migration operation |

### Retention Policy

| File | Purpose |
|------|---------|
| `Configuration/RetentionPolicy/RetentionPolicyTypeBuilder.cs` | Fluent API |
| `Configuration/RetentionPolicy/RetentionPolicyAnnotations.cs` | Annotation constants |
| `Configuration/RetentionPolicy/RetentionPolicyAttribute.cs` | Data annotation |
| `Configuration/RetentionPolicy/RetentionPolicyConvention.cs` | Convention processing |
| `Internals/Features/RetentionPolicies/RetentionPolicyDiffer.cs` | Diffing logic |
| `Internals/Features/RetentionPolicies/RetentionPolicyModelExtractor.cs` | Model extraction |
| `Generators/RetentionPolicySqlGenerator.cs` | Runtime SQL generation |
| `MigrationExtensions/RetentionPolicyMigrationExtensions.cs` | Typed migrationBuilder methods |
| `Operations/AddRetentionPolicyOperation.cs` | Migration operation |
| `Operations/AlterRetentionPolicyOperation.cs` | Migration operation |
| `Operations/DropRetentionPolicyOperation.cs` | Migration operation |

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
| `Generators/ContinuousAggregateSqlGenerator.cs` | Runtime SQL generation |
| `MigrationExtensions/ContinuousAggregateMigrationExtensions.cs` | Typed migrationBuilder methods |
| `Abstractions/ContinuousAggregateFunction.cs` | Typed aggregate-function value |
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
| `Generators/ContinuousAggregatePolicySqlGenerator.cs` | Runtime SQL generation |
| `MigrationExtensions/ContinuousAggregatePolicyMigrationExtensions.cs` | Typed migrationBuilder methods |
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
| `Internals/TimescaleMigrationsModelDiffer.cs` | Differ orchestration, context building, operation prioritization |
| `Internals/Features/IFeatureDiffer.cs` | Differ interface |
| `Internals/Features/FeatureDiffContext.cs` | Cross-cutting diff state (renames, recreated aggregates) |
| `Generators/SqlBuilderHelper.cs` | Identifier quoting, regclass, command grouping, SELECT→PERFORM |
| `Generators/PolicyJobSqlBuilder.cs` | Shared `alter_job` clause builder for policies |
| `DefaultValues.cs` | Centralized defaults |
| `Abstractions/Dimension.cs` | Range/hash partitioning |
| `Abstractions/EAggregateFunction.cs` | Aggregate function enum |
| `Abstractions/ContinuousAggregateFunction.cs` | Typed aggregate-function value |

## Design Library Key Files

| File | Purpose |
|------|---------|
| `TimescaleDBDesignTimeServices.cs` | Register design-time services |
| `TimescaleCSharpMigrationOperationGenerator.cs` | Dispatches operations to the `*CSharpGenerator` classes |
| `Generators/HypertableCSharpGenerator.cs` | Emits `CreateHypertable`/`AlterHypertable` calls |
| `Generators/ReorderPolicyCSharpGenerator.cs` | Emits reorder-policy calls |
| `Generators/RetentionPolicyCSharpGenerator.cs` | Emits retention-policy calls |
| `Generators/ContinuousAggregateCSharpGenerator.cs` | Emits continuous-aggregate calls |
| `Generators/ContinuousAggregatePolicyCSharpGenerator.cs` | Emits CA-policy calls |
| `Generators/MigrationCallWriter.cs` | Writes a `.Method(arg: value, …)` call |
| `Generators/CSharpGeneratorHelper.cs` | Collection-expression and static-call literal helpers |
| `TimescaleDatabaseModelFactory.cs` | Db-first scaffolding orchestration |
| `Scaffolding/ITimescaleFeatureExtractor.cs` | Extractor interface |
| `Scaffolding/IAnnotationApplier.cs` | Applier interface |
| `Scaffolding/HypertableScaffoldingExtractor.cs` | Query hypertables from database |
| `Scaffolding/HypertableAnnotationApplier.cs` | Apply hypertable annotations |
| `Scaffolding/ReorderPolicyScaffoldingExtractor.cs` | Query reorder policies from database |
| `Scaffolding/ReorderPolicyAnnotationApplier.cs` | Apply reorder policy annotations |
| `Scaffolding/RetentionPolicyScaffoldingExtractor.cs` | Query retention policies from database |
| `Scaffolding/RetentionPolicyAnnotationApplier.cs` | Apply retention policy annotations |
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
│   │   ├── ReorderPolicy/
│   │   └── RetentionPolicy/
│   ├── Generators/         # Runtime SQL generation
│   ├── MigrationExtensions/ # Typed migrationBuilder.* methods
│   ├── Internals/          # Core diffing logic
│   │   └── Features/
│   │       ├── ContinuousAggregates/
│   │       ├── ContinuousAggregatePolicies/
│   │       ├── Hypertables/
│   │       ├── ReorderPolicies/
│   │       └── RetentionPolicies/
│   ├── Operations/         # Migration operations
│   ├── Query/              # EF.Functions extensions and LINQ translators
│   │   └── Internal/       # EF Core query pipeline integration
│   └── *.cs                # Entry points, extensions
│
└── Eftdb.Design/           # Design-time library (CmdScale.EntityFrameworkCore.TimescaleDB.Design)
    ├── Generators/         # Design-time C# (typed migration call) generation
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

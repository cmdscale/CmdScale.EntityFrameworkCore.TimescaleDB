# Key Patterns and Conventions

This document describes the architectural patterns used throughout the CmdScale.EntityFrameworkCore.TimescaleDB library.

## 1. Service Registration

`UseTimescaleDb()` is the single entry point for configuring TimescaleDB support:

```csharp
options.UseNpgsql(connectionString).UseTimescaleDb();
```

Internally, it registers an `IDbContextOptionsExtension` that provides:
- `IConventionSetPlugin` → `TimescaleDbConventionSetPlugin` (processes data attributes)
- `IMigrationsModelDiffer` → `TimescaleMigrationsModelDiffer` (feature-aware diffing)
- `IMigrationsSqlGenerator` → `TimescaleDbMigrationsSqlGenerator` (TimescaleDB SQL)

**Location:** `TimescaleDbContextOptionsBuilderExtensions.cs`

## 2. Convention System

Each feature has an `IEntityTypeAddedConvention` implementation that processes its data attributes during model building. Conventions convert data attributes to entity type annotations stored in EF Core metadata.

All conventions follow the same pattern: read attributes from the entity type, validate configuration, and store results as annotations. They are registered in `TimescaleDbConventionSetPlugin.ModifyConventions()`.

**Location:** `Configuration/{Feature}/{Feature}Convention.cs` — check the source for the current list of conventions.

## 3. Dual Configuration Model

Both data annotations and Fluent API result in identical annotations:

```csharp
// Data Annotations
[Hypertable("Timestamp", ChunkTimeInterval = "1 day")]
public class Trade { }

// Fluent API
builder.Entity<Trade>()
    .IsHypertable(x => x.Timestamp)
    .WithChunkTimeInterval("1 day");
```

Both approaches store identical annotation values in entity type metadata.

## 4. IFeatureDiffer Pattern

Each TimescaleDB feature has a dedicated differ implementing `IFeatureDiffer`. The differ uses a corresponding `*ModelExtractor` static class to read annotations from the source and target models, then compares them to generate appropriate migration operations (Create, Alter, Drop). A `FeatureDiffContext` carries rename maps and recreated-aggregate state the differ cannot derive on its own (see architecture.md).

Example (`HypertableDiffer`):
```csharp
public class HypertableDiffer : IFeatureDiffer
{
    public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
    {
        context ??= FeatureDiffContext.Empty;
        HypertableInfo? sourceInfo = HypertableModelExtractor.Extract(source);
        HypertableInfo? targetInfo = HypertableModelExtractor.Extract(target);
        return CompareDifferences(sourceInfo, targetInfo, context);
    }
}
```

`TimescaleMigrationsModelDiffer.GetDifferences()` runs EF Core's base differ, builds the `FeatureDiffContext`, then invokes each feature differ with it.

**Location:** `Internals/Features/{Feature}/` — check the source for the full list of feature differs.

## 5. Runtime vs Design-Time Duality

The same custom `MigrationOperation` types feed two independent code paths:

| Context | Entry point | Generators | Output |
|---------|-------------|------------|--------|
| Runtime (`dotnet ef database update`) | `TimescaleDbMigrationsSqlGenerator` | `Generators/*SqlGenerator` | TimescaleDB SQL statements |
| Design-time (`dotnet ef migrations add`) | `TimescaleCSharpMigrationOperationGenerator` | `Design/Generators/*CSharpGenerator` | Typed `migrationBuilder.*` calls |

The design-time path emits typed calls (e.g. `migrationBuilder.CreateHypertable(...)`) from `MigrationExtensions/`; those operations are turned into SQL by the runtime path at `database update` time.

## 6. Annotation-Based Metadata Storage

All TimescaleDB configuration is stored in entity type annotations. Each feature defines its annotation constants in a dedicated class.

**Pattern:** `Configuration/{Feature}/{Feature}Annotations.cs` — each class contains `const string` fields for annotation keys.

**Usage Pattern (example: Hypertable):**
```csharp
// Write
entityType.SetAnnotation(HypertableAnnotations.IsHypertable, true);
entityType.SetAnnotation(HypertableAnnotations.ChunkTimeInterval, "1 day");

// Read
bool isHypertable = entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value as bool? ?? false;
string? interval = entityType.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value as string;
```

**Complex Types:** Lists and complex objects (e.g., `Dimension[]`) are JSON-serialized before storage.

Check `Configuration/{Feature}/{Feature}Annotations.cs` for the complete list of annotations per feature.

## 7. Column Name Convention Support

**Critical:** Always use `StoreObjectIdentifier` and `GetColumnName()` to resolve property names to database column names:

```csharp
// Get the table identifier
StoreObjectIdentifier storeIdentifier = StoreObjectIdentifier.Table(tableName, schema);

// Resolve property to column name (respects naming conventions)
string columnName = property.GetColumnName(storeIdentifier);
```

This automatically handles snake_case, camelCase, PascalCase, and custom naming conventions.

**Where to use:**
- Model extractors when reading column names from annotations
- Operation generators when building SQL
- Differs when comparing column references

**Location:** `Internals/Features/{Feature}/{Feature}ModelExtractor.cs`

## 8. SQL Building Helpers

`*SqlGenerator` classes build identifiers and table references through `SqlBuilderHelper`:

```csharp
SqlBuilderHelper.Regclass("my_table", "custom_schema");           // 'custom_schema."my_table"'
SqlBuilderHelper.QualifiedIdentifier("my_table", "custom_schema"); // "custom_schema"."my_table"
SqlBuilderHelper.QuoteIdentifier("my_column");                    // "my_column"
```

`SqlBuilderHelper.BuildQueryString(statements, builder, suppressTransaction, usePerform)` groups the generated statements into commands and appends them to the `MigrationCommandListBuilder`. When `usePerform` is set (idempotent scripts), leading `SELECT` keywords are rewritten to `PERFORM` so the SQL is valid inside a PL/pgSQL block.

**Location:** `Generators/SqlBuilderHelper.cs`, `Generators/PolicyJobSqlBuilder.cs`

## 9. Continuous Aggregate Function Encoding

The typed API uses `Abstractions/ContinuousAggregateFunction` — `(Alias, Function, SourceColumn)` — for each aggregate column:

```csharp
new ContinuousAggregateFunction("average_price", EAggregateFunction.Avg, "price")
```

`ToAnnotationValue()` serializes it to the colon-delimited wire format stored on `CreateContinuousAggregateOperation.AggregateFunctions`:

**Format:** `"alias:Function:sourceColumn"` (always three parts — First/Last take no time column in the wire format; the SQL generator supplies the time-bucket column as their second argument: `last("price", "timestamp")`).

**Examples:** `"average_price:Avg:price"`, `"last_price:Last:price"`

**Parsing:** Split by `:` and validate array length (exactly 3 elements; malformed entries are skipped).

**Location:** `Abstractions/ContinuousAggregateFunction.cs`, `ContinuousAggregateModelExtractor.cs`, `Generators/ContinuousAggregateSqlGenerator.cs`

## 10. Expression-Based Configuration

All Fluent API uses lambda expressions for refactoring-safe property resolution:

```csharp
// Hypertable time column
builder.IsHypertable(x => x.Timestamp)

// Aggregate function mapping
builder.AddAggregateFunction(
    aggregateProperty: x => x.AvgPrice,
    sourceProperty: x => x.Price,
    function: EAggregateFunction.Avg
)

// First/Last — the time argument is always the continuous aggregate's
// time-bucket column; there is no timeColumn parameter
builder.AddAggregateFunction(
    aggregateProperty: x => x.LastPrice,
    sourceProperty: x => x.Price,
    function: EAggregateFunction.Last
)

// Group by columns
builder.AddGroupByColumn(x => x.Exchange)
```

Lambda expressions are parsed to extract property names (via `LambdaExpression.Body` as `MemberExpression`), then resolved to database column names using EF Core's metadata system.

**Location:** `ContinuousAggregateBuilder<TEntity, TSourceEntity>.cs`

## 11. DRY Principle Implementation

- Extract common logic into helper methods (`SqlBuilderHelper`, `PolicyJobSqlBuilder`)
- Centralize constants in `DefaultValues.cs` and annotation name classes
- Use `StoreObjectIdentifier` pattern consistently across extractors
- Avoid duplicating SQL generation logic - route it through the `*SqlGenerator` classes

```csharp
// Correct - Centralized helper
string qualifiedName = SqlBuilderHelper.QualifiedIdentifier(table, schema);

// Incorrect - Duplicated logic
string qualifiedName = string.IsNullOrEmpty(schema)
    ? $"\"{table}\""
    : $"\"{schema}\".\"{table}\"";
```

## 12. Separation of Concerns

Keep each class focused on a single responsibility:

| Layer | Purpose | Classes |
|-------|---------|---------|
| Configuration | User-facing APIs | Attributes, Fluent API, Conventions |
| Model Extraction | Read from EF metadata | `*ModelExtractor` classes |
| Diffing | Compare models, generate operations | `*Differ` classes |
| Runtime SQL | Convert operations to SQL | `Generators/*SqlGenerator` classes |
| Design-time C# | Convert operations to typed migration calls | `Design/Generators/*CSharpGenerator` classes |
| Migration API | Construct operations from migration files | `MigrationExtensions/*MigrationExtensions` classes |
| Scaffolding extraction | Reverse engineer from database | `Scaffolding/*ScaffoldingExtractor` + `*AnnotationApplier` classes |
| Scaffolding code generation | Annotations → C# fluent API or data annotation attributes | `Design/Generators/AnnotationRenderers/*AnnotationRenderer` classes |

**Never mix concerns:** Extractors should not generate SQL, differs should not read databases.

## 13. Scaffolding Annotation Code Generation

`dotnet ef dbcontext scaffold` runs two distinct phases:

**Phase 1 — Database extraction** (`Scaffolding/`): `TimescaleDatabaseModelFactory` calls each `*ScaffoldingExtractor` to query TimescaleDB system tables, then calls the matching `*AnnotationApplier` to store the metadata as annotations on the EF Core `DatabaseModel`. The result is the same annotation format the runtime library uses.

**Phase 2 — Code generation** (`Generators/AnnotationRenderers/`): EF Core's scaffolding pipeline asks `TimescaleDbAnnotationCodeGenerator` to convert those annotations into C# code. It dispatches to registered `IFeatureAnnotationRenderer` implementations.

**`IFeatureAnnotationRenderer` contract:**

```csharp
interface IFeatureAnnotationRenderer
{
    // Called when UseDataAnnotations = false — emit fluent API calls
    void GenerateFluentApiCalls(
        IEntityType entityType,
        Dictionary<string, IAnnotation> annotations,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters);

    // Called when UseDataAnnotations = true — return attribute fragments
    IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
        IEntityType entityType,
        Dictionary<string, IAnnotation> annotations);
}
```

**Key rules:**
- Call `AnnotationRendererHelper.Consume(annotations, keys...)` for every annotation key you handle. Unconsumed annotations cause EF Core to emit a raw `.HasAnnotation("key", value)` fallback in the scaffolded code.
- Use `AnnotationRendererHelper.ResolvePropertyName(entityType, columnName)` to map database column names back to C# property names.
- Use `NameOfCodeFragment` to emit `nameof(Entity.Property)` instead of hard-coded string literals so the scaffolded code is refactoring-safe. `TimescaleCSharpHelper.UnknownLiteral` handles rendering these.
- Register each new renderer in `TimescaleDbAnnotationCodeGenerator`.
- If a renderer emits attributes from a new namespace, add that namespace to `TimescaleCSharpModelGenerator.CollectAttributeNamespaces()` so the `using` directive is injected automatically.

**`TimescaleCSharpModelGenerator`** sits at the top of the scaffolding code generation chain. It wraps EF Core's standard `CSharpModelGenerator` and, when `UseDataAnnotations = true`, inspects the generated entity files to add any missing TimescaleDB `using` directives. `TimescaleModelCodeGeneratorSelector` ensures this generator is selected in preference to EF Core's default `CSharpModelGenerator`.

**Location:** `Design/Generators/AnnotationRenderers/`, `Design/Generators/TimescaleDbAnnotationCodeGenerator.cs`, `Design/Generators/TimescaleCSharpModelGenerator.cs`, `Design/Generators/TimescaleModelCodeGeneratorSelector.cs`

```csharp
// Correct - Separation of concerns
public class HypertableDiffer : IFeatureDiffer
{
    public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
    {
        // Only diffing logic - delegates extraction to HypertableModelExtractor
        HypertableInfo? sourceInfo = HypertableModelExtractor.Extract(source);
        HypertableInfo? targetInfo = HypertableModelExtractor.Extract(target);
        return CompareDifferences(sourceInfo, targetInfo, context ?? FeatureDiffContext.Empty);
    }
}
```

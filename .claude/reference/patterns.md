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

Each TimescaleDB feature has a dedicated differ implementing `IFeatureDiffer`. The differ uses a corresponding `*ModelExtractor` static class to read annotations from the source and target models, then compares them to generate appropriate migration operations (Create, Alter, Drop).

Example (`HypertableDiffer`):
```csharp
public class HypertableDiffer : IFeatureDiffer
{
    public IEnumerable<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
    {
        HypertableInfo? sourceInfo = HypertableModelExtractor.Extract(source);
        HypertableInfo? targetInfo = HypertableModelExtractor.Extract(target);
        return CompareDifferences(sourceInfo, targetInfo);
    }
}
```

All differs are registered in `TimescaleMigrationsModelDiffer`'s `_featureDiffers` list.

**Location:** `Internals/Features/{Feature}/` — check the source for the full list of feature differs.

## 5. Runtime vs Design-Time Duality

| Context | Generator | Quote String | isDesignTime |
|---------|-----------|--------------|--------------|
| Runtime (`dotnet ef database update`) | `TimescaleDbMigrationsSqlGenerator` | `"` | `false` |
| Design-time (`dotnet ef migrations add`) | `TimescaleCSharpMigrationOperationGenerator` | `""` | `true` |

Both use the same operation generators with different `isDesignTime` parameter values.

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

## 8. SQL Generation with Quote Escaping

**SqlBuilderHelper** provides utilities for proper quoting:

```csharp
// Runtime SQL (isDesignTime = false)
string sql = SqlBuilderHelper.BuildQueryString("SELECT * FROM \"my_table\"", builder, isDesignTime: false);
// Output: SELECT * FROM "my_table"

// Design-time C# (isDesignTime = true)
string csharp = SqlBuilderHelper.BuildQueryString("SELECT * FROM \"my_table\"", builder, isDesignTime: true);
// Output: SELECT * FROM ""my_table"" (quotes doubled for C# string escaping)
```

**Critical:** Always pass `isDesignTime` parameter correctly to operation generators.

**Location:** `Generators/SqlBuilderHelper.cs`

## 9. Continuous Aggregate String Encoding

Aggregate functions are stored as colon-delimited strings in annotations:

**Format:**
- Basic: `"alias:function:sourceColumn"`
- First/Last: `"alias:function:sourceColumn:timeColumn"`

**Examples:**
```csharp
// Avg aggregate
"avg_price:Avg:price"

// Last aggregate with time column
"last_price:Last:price:timestamp"
```

**Parsing:** Split by `:` and validate array length (3 or 4 elements).

**Location:** `ContinuousAggregateModelExtractor.cs`, `ContinuousAggregateOperationGenerator.cs`

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

// First/Last with time column
builder.AddAggregateFunction(
    aggregateProperty: x => x.LastPrice,
    sourceProperty: x => x.Price,
    function: EAggregateFunction.Last,
    timeColumn: x => x.Timestamp
)

// Group by columns
builder.AddGroupByColumn(x => x.Exchange)
```

Lambda expressions are parsed to extract property names (via `LambdaExpression.Body` as `MemberExpression`), then resolved to database column names using EF Core's metadata system.

**Location:** `ContinuousAggregateBuilder<TEntity, TSourceEntity>.cs`

## 11. DRY Principle Implementation

- Extract common logic into helper methods (`SqlBuilderHelper`)
- Centralize constants in `DefaultValues.cs` and annotation name classes
- Use `StoreObjectIdentifier` pattern consistently across extractors
- Avoid duplicating SQL generation logic - use operation generators consistently

```csharp
// Correct - Centralized helper
string tableName = SqlBuilderHelper.GetQualifiedTableName(schema, table, _quoteString);

// Incorrect - Duplicated logic
string tableName = string.IsNullOrEmpty(schema)
    ? $"{_quoteString}{table}{_quoteString}"
    : $"{_quoteString}{schema}{_quoteString}.{_quoteString}{table}{_quoteString}";
```

## 12. Separation of Concerns

Keep each class focused on a single responsibility:

| Layer | Purpose | Classes |
|-------|---------|---------|
| Configuration | User-facing APIs | Attributes, Fluent API, Conventions |
| Model Extraction | Read from EF metadata | `*ModelExtractor` classes |
| Diffing | Compare models, generate operations | `*Differ` classes |
| Generation | Convert operations to SQL/C# | `*OperationGenerator` classes |
| Design-time | Reverse engineer from database | Scaffolding extractors/appliers |

**Never mix concerns:** Extractors should not generate SQL, differs should not read databases.

```csharp
// Correct - Separation of concerns
public class HypertableDiffer : IFeatureDiffer
{
    public IEnumerable<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
    {
        // Only diffing logic - delegates extraction to HypertableModelExtractor
        HypertableInfo? sourceInfo = HypertableModelExtractor.Extract(source);
        HypertableInfo? targetInfo = HypertableModelExtractor.Extract(target);
        return CompareDifferences(sourceInfo, targetInfo);
    }
}
```

# Key Patterns and Conventions

Architectural patterns used throughout the library. Structure and file locations: see `architecture.md`.

## 1. Service Registration

`UseTimescaleDb()` is the single entry point:

```csharp
options.UseNpgsql(connectionString).UseTimescaleDb();
```

It registers an `IDbContextOptionsExtension` providing `IConventionSetPlugin` (attribute processing), `IMigrationsModelDiffer` (feature-aware diffing), and `IMigrationsSqlGenerator` (TimescaleDB SQL).

## 2. Dual Configuration Model

Data annotations and fluent API write **identical annotations** to entity type metadata:

```csharp
[Hypertable("Timestamp", ChunkTimeInterval = "1 day")]      // via {Feature}Convention
public class Trade { }

builder.Entity<Trade>().IsHypertable(x => x.Timestamp)      // via {Feature}TypeBuilder
    .WithChunkTimeInterval("1 day");
```

Conventions implement `IEntityTypeAddedConvention`, are registered in `TimescaleDbConventionSetPlugin.ModifyConventions()`, and only convert attributes → annotations. Annotation keys are `const string`s in `{Feature}Annotations` — never hard-code them. Complex values (e.g. `Dimension[]`) are JSON-serialized.

## 3. IFeatureDiffer

```csharp
public IReadOnlyList<MigrationOperation> GetDifferences(
    IRelationalModel? source, IRelationalModel? target, FeatureDiffContext? context = null)
{
    context ??= FeatureDiffContext.Empty;
    HypertableInfo? sourceInfo = HypertableModelExtractor.Extract(source);
    HypertableInfo? targetInfo = HypertableModelExtractor.Extract(target);
    return CompareDifferences(sourceInfo, targetInfo, context);
}
```

Extraction is the extractor's job; comparison is the differ's; the context resolves renames (`ResolveTable`/`ResolveColumn`/`ResolveIndex`) so a rename is not treated as drop-and-create. Operation ordering is centralized in `TimescaleMigrationsModelDiffer.GetOperationPriority()` — differs never set priorities.

## 4. Runtime vs Design-Time Duality

The same `MigrationOperation` types feed two independent paths:

| Context | Entry point | Output |
|---------|-------------|--------|
| `dotnet ef database update` | `TimescaleDbMigrationsSqlGenerator` → `{Feature}SqlGenerator` | TimescaleDB SQL |
| `dotnet ef migrations add` | `TimescaleCSharpMigrationOperationGenerator` → `{Feature}CSharpGenerator` | typed `migrationBuilder.*` calls |

Every new operation type must be registered in **both** switches, plus a `MigrationExtensions` method so generated migrations compile. Generators carry no `isDesignTime` flag and do no quote-doubling.

## 5. Column Name Resolution

**Critical:** never assume a naming convention. Resolve property names to column names via:

```csharp
StoreObjectIdentifier storeIdentifier = StoreObjectIdentifier.Table(tableName, schema);
string columnName = property.GetColumnName(storeIdentifier);
```

For lookups that may involve CLR property names, complex-type paths, or already-resolved column names, go through `Internals/ColumnNameResolver` — the single resolution authority. Applies to model extractors, SQL generators, and differs.

## 6. SQL Building

Always build identifiers through `SqlBuilderHelper` — never hand-roll quoting:

```csharp
SqlBuilderHelper.Regclass("my_table", "custom_schema");            // 'custom_schema."my_table"'
SqlBuilderHelper.QualifiedIdentifier("my_table", "custom_schema"); // "custom_schema"."my_table"
SqlBuilderHelper.QuoteIdentifier("my_column");                     // "my_column"
```

`BuildQueryString(statements, builder, suppressTransaction, usePerform)` groups statements into commands; `usePerform` rewrites leading `SELECT` → `PERFORM` for idempotent PL/pgSQL scripts. Policy `alter_job` clauses go through `PolicyJobSqlBuilder`.

## 7. Continuous Aggregate Function Encoding

Typed API: `ContinuousAggregateFunction(Alias, Function, SourceColumn)`. Wire format on the operation: `"alias:Function:sourceColumn"` — always exactly three parts; malformed entries are skipped on parse. `First`/`Last` take no time column in the wire format; the SQL generator supplies the time-bucket column as second argument (`last("price", "timestamp")`).

## 8. Expression-Based Configuration

All fluent API uses lambdas for refactoring-safe property references:

```csharp
builder.IsHypertable(x => x.Timestamp);
builder.AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg);
builder.AddGroupByColumn(x => x.Exchange);
```

`ExpressionHelper.GetPropertyName` extracts names (chained access → dot-path), then `ColumnNameResolver` resolves to columns.

## 9. Scaffolding Annotation Rendering

Phase 2 of scaffolding (see architecture.md) converts `DatabaseModel` annotations to C# via `IFeatureAnnotationRenderer`:

```csharp
interface IFeatureAnnotationRenderer
{
    // UseDataAnnotations = false — emit fluent API calls
    void GenerateFluentApiCalls(IEntityType entityType,
        Dictionary<string, IAnnotation> annotations,
        CSharpRuntimeAnnotationCodeGeneratorParameters parameters);

    // UseDataAnnotations = true — return attribute fragments
    IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
        IEntityType entityType, Dictionary<string, IAnnotation> annotations);
}
```

Rules:
- `AnnotationRendererHelper.Consume(annotations, keys...)` **every** key you handle — unconsumed annotations become raw `.HasAnnotation(...)` fallbacks in scaffolded code.
- Emit real, renderable C# — not `.HasAnnotation`. If the runtime API can't be rendered (complex args), add a renderable runtime overload (e.g. the string-based `{Feature}StringBuilder`s) rather than falling back.
- Use `NameOfCodeFragment` / `ColumnListCodeFragment` for rename-safe `nameof(...)` references; `TimescaleCSharpHelper.UnknownLiteral` renders them. Fall back to raw strings only for unmapped columns.
- Map columns back to properties with `AnnotationRendererHelper.ResolvePropertyName` / `TryResolvePropertyName`.
- Register the renderer in `TimescaleDbAnnotationCodeGenerator` — policy renderers after their parent renderer (their `ShouldRender` checks parent consumption).
- New attribute namespaces must be added to `TimescaleCSharpModelGenerator.CollectAttributeNamespaces()` for `using` injection.
- Policy scaffolding reuses `PolicyJobBuilderCore` (runtime) + `PolicyJobRendererHelper` (design) — do not duplicate policy-job field handling.

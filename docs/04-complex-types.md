# Complex Type Support

This library resolves EF Core [complex type](https://learn.microsoft.com/en-us/ef/core/modeling/complex-types) member references in every column-referencing configuration API. A fluent selector may traverse complex-type properties (`x => x.Param1.Value`), and string-based configuration (data annotations, raw column lists) may use the equivalent dot-separated path (`"Param1.Value"`) or the mapped database column name directly.

Resolution honours all registered naming conventions: a complex property `Value` on complex member `Param1` maps to `Param1_Value` by default and to `param1_value` under EFCore.NamingConventions snake_case, for example.

---

## Supported APIs

Complex-type member chains resolve in all of the following:

| Feature | API |
| --- | --- |
| Hypertable time column | `IsHypertable(x => x.Meta.Timestamp)`, `[Hypertable("Meta.Timestamp")]` |
| Additional dimensions | `HasRangeDimension(x => x.Meta.Region, ...)`, `HasHashDimension(...)` |
| Chunk-skip columns | `WithChunkSkipping(x => x.Meta.DeviceId)` |
| Compression segment-by | `WithCompressionSegmentBy(x => x.Meta.TenantId)` |
| Compression order-by | `s => [s.ByDescending(x => x.Meta.Timestamp)]` |
| Sparse indexes | `s => s.Bloom(x => x.Meta.DeviceId)`, `s => s.MinMax(...)` |
| Continuous aggregate time bucket | `IsContinuousAggregate<TAgg, TSource>(..., x => x.Meta.Timestamp, ...)` |
| Aggregate functions | `AddAggregateFunction(a => a.Avg, d => d.Param1.Value, EAggregateFunction.Avg)` |
| Group-by columns | `AddGroupByColumn(x => x.Param1.Name)` |

Nested complex types (`x => x.Outer.Inner.Value`) resolve recursively.

```csharp
[ComplexType]
public class SensorChannel
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class Reading
{
    public Guid Id { get; set; }
    public DateTime RecordedAt { get; set; }
    public SensorChannel Primary { get; set; } = new();
    public SensorChannel Secondary { get; set; } = new();
}
```

```csharp
builder.IsContinuousAggregate<HourlyAggregate, Reading>(x => x.RecordedAt, "1 hour")
       .AddAggregateFunction(a => a.AvgPrimary, d => d.Primary.Value, EAggregateFunction.Avg)
       .AddAggregateFunction(a => a.AvgSecondary, d => d.Secondary.Value, EAggregateFunction.Avg)
       .AddGroupByColumn(d => d.Primary.Name);
```

The time column of a hypertable or continuous aggregate may live inside a complex type; the store-type validation at model finalization traverses the path the same way and throws for invalid store types exactly as for top-level properties.

---

## Limitations

- **JSON-mapped complex types** (`ComplexProperty(...).ToJson()`): properties inside a JSON-mapped complex type do not have individual table columns. References to them do not resolve and the configuration entry is skipped.
- **Complex type collections** (EF Core 10): collections have no per-element columns; paths through a collection complex property do not resolve.
- **Owned entity types** are not traversed. Complex-type support covers `[ComplexType]` / `ComplexProperty(...)` mappings only; a path through an owned navigation does not resolve.
- **Scaffolding** produces flat entities: `dotnet ef dbcontext scaffold` never generates `[ComplexType]` declarations, so a scaffolded model represents complex-type columns as ordinary flat properties. Round-tripping a complex-type model through scaffolding yields an equivalent flat model with no phantom migration diffs, because annotation values store resolved database column names that the resolver recognises in column-name form.

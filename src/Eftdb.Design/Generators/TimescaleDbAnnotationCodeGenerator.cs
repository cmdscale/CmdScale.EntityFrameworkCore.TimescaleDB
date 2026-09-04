using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

#pragma warning disable EF1001 // NpgsqlAnnotationCodeGenerator lives in *.Internal but is the documented base for provider annotation code generation.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Converts the <c>TimescaleDB:*</c> annotations produced by scaffolding into the library's Fluent API
    /// calls (default) or DataAnnotation attributes (<c>--data-annotations</c>).
    /// </summary>
    /// <remarks>
    /// Subclasses Npgsql's generator so its own annotation handling (arrays, identity, comments) is preserved.
    /// Feature-specific rendering is delegated to one <see cref="IFeatureAnnotationRenderer"/> per feature,
    /// mirroring the per-feature extractor/applier pairs in <c>TimescaleDatabaseModelFactory</c>. Annotations
    /// no renderer consumes fall back to <c>.HasAnnotation(...)</c>, keeping the generated model complete.
    /// </remarks>
    public sealed class TimescaleDbAnnotationCodeGenerator(
        AnnotationCodeGeneratorDependencies dependencies,
        IOperationReporter reporter)
        : NpgsqlAnnotationCodeGenerator(dependencies)
    {
        private readonly IReadOnlyList<IFeatureAnnotationRenderer> _renderers =
        [
            new HypertableAnnotationRenderer(),
            new ContinuousAggregateAnnotationRenderer(reporter),
            new ContinuousAggregatePolicyAnnotationRenderer(),
            new RetentionPolicyAnnotationRenderer(),
            new ReorderPolicyAnnotationRenderer(),
            new CompressionPolicyAnnotationRenderer(),
        ];

        private readonly Dictionary<IEntityType, IReadOnlyList<AttributeCodeFragment>> _entityAttributeCache = [];
        private readonly Dictionary<IProperty, IReadOnlyList<AttributeCodeFragment>> _propertyAttributeCache = [];

        internal bool ScaffoldMode { get; set; }
        internal bool ScaffoldDataAnnotationsMode { get; set; }

        internal void ResetScaffoldState()
        {
            ScaffoldMode = false;
            ScaffoldDataAnnotationsMode = false;
            _entityAttributeCache.Clear();
            _propertyAttributeCache.Clear();
        }

        public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            List<MethodCallCodeFragment> calls = [.. base.GenerateFluentApiCalls(entityType, annotations)];

            if (!ScaffoldMode)
            {
                return calls;
            }

            if (ScaffoldDataAnnotationsMode)
            {
                foreach (IFeatureAnnotationRenderer renderer in _renderers)
                {
                    renderer.ConsumeFeatureAnnotations(entityType, annotations);
                }
            }
            else
            {
                foreach (IFeatureAnnotationRenderer renderer in _renderers)
                {
                    calls.AddRange(renderer.GenerateFluentApiCalls(entityType, annotations));
                }
            }

            return calls;
        }

        public override IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            List<AttributeCodeFragment> attributes = [.. base.GenerateDataAnnotationAttributes(entityType, annotations)];

            if (!ScaffoldMode)
            {
                return attributes;
            }

            if (_entityAttributeCache.TryGetValue(entityType, out IReadOnlyList<AttributeCodeFragment>? cached))
            {
                attributes.AddRange(cached);
                return attributes;
            }

            List<AttributeCodeFragment> featureAttributes = [];
            foreach (IFeatureAnnotationRenderer renderer in _renderers)
            {
                featureAttributes.AddRange(renderer.GenerateDataAnnotationAttributes(entityType, annotations));
            }

            _entityAttributeCache[entityType] = featureAttributes;
            attributes.AddRange(featureAttributes);
            return attributes;
        }

        public override IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IProperty property, IDictionary<string, IAnnotation> annotations)
        {
            List<AttributeCodeFragment> attributes = [.. base.GenerateDataAnnotationAttributes(property, annotations)];

            if (!ScaffoldMode)
            {
                return attributes;
            }

            if (_propertyAttributeCache.TryGetValue(property, out IReadOnlyList<AttributeCodeFragment>? cached))
            {
                attributes.AddRange(cached);
                return attributes;
            }

            IReadOnlyList<AttributeCodeFragment> featureAttributes = GenerateContinuousAggregatePropertyAttributes(property);
            _propertyAttributeCache[property] = featureAttributes;
            attributes.AddRange(featureAttributes);
            return attributes;
        }

        /// <summary>
        /// Renders the property-level <c>[Aggregate]</c> and <c>[GroupByColumn]</c> attributes of a
        /// continuous aggregate entity.
        /// </summary>
        private static IReadOnlyList<AttributeCodeFragment> GenerateContinuousAggregatePropertyAttributes(IProperty property)
        {
            if (property.DeclaringType is not IEntityType entityType)
            {
                return [];
            }

            if (entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value is not string)
            {
                return [];
            }

            string? parentName = entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value as string;
            IEntityType? parentEntityType = ParentEntityTypeResolver.Resolve(entityType.Model, parentName);

            // Code-first: AggregateFunctions annotation is populated by the convention/builder.
            // Format: "{CLR property name}:{EAggregateFunction}:{source CLR property name or *}"
            if (entityType.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions)?.Value is List<string> aggregateFunctions)
            {
                string? entry = aggregateFunctions.FirstOrDefault(e => e.StartsWith(property.Name + ":"));
                if (entry is null) return [];

                string[] parts = entry.Split(':', 3);
                if (parts.Length < 3 || !Enum.TryParse<EAggregateFunction>(parts[1], out EAggregateFunction function))
                    return [];

                return [new AttributeCodeFragment(typeof(AggregateAttribute), function,
                    ResolveSourceArgByClrName(parts[2], parentEntityType))];
            }

            // Db-First: AggregateFunctions is not set; the applier only stores the raw ViewDefinition SQL.
            // Match this property's DB column name against the parsed aggregates and GROUP BY columns.
            if (entityType.FindAnnotation(ContinuousAggregateAnnotations.ViewDefinition)?.Value is not string viewDefinition) return [];

            ViewDefinitionParser.ParsedViewDefinition parsed = ViewDefinitionParser.Parse(viewDefinition);

            string viewName = entityType.GetViewName() ?? entityType.GetTableName() ?? entityType.Name;
            string? viewSchema = entityType.GetViewSchema() ?? entityType.GetSchema();
            StoreObjectIdentifier caStoreId = StoreObjectIdentifier.View(viewName, viewSchema);
            string columnName = property.GetColumnName(caStoreId) ?? property.Name;

            if (parsed.TimeBucketWidth is not null
                && parsed.TimeBucketSourceColumn is not null
                && parsed.TimeBucketAlias is not null
                && parsed.TimeBucketAlias != DefaultValues.ContinuousAggregateTimeBucketColumnName
                && parsed.TimeBucketAlias == columnName)
            {
                return [new AttributeCodeFragment(typeof(TimeBucketAttribute),
                    IntervalParsingHelper.NormalizeInterval(parsed.TimeBucketWidth),
                    ResolveSourceArgByColumnName(parsed.TimeBucketSourceColumn, parentEntityType))];
            }

            ViewDefinitionParser.ParsedAggregate? agg = parsed.Aggregates.FirstOrDefault(a => a.Alias == columnName);
            if (agg is not null)
            {
                return [new AttributeCodeFragment(typeof(AggregateAttribute), agg.Function,
                    ResolveSourceArgByColumnName(agg.SourceColumn, parentEntityType))];
            }

            if (parsed.GroupByColumns.Contains(columnName))
            {
                return [GenerateGroupByColumnAttribute(property, columnName, parentEntityType)];
            }

            return [];
        }

        /// <summary>
        /// Emits <c>[GroupByColumn]</c> without arguments when the parent's CLR property name matches the
        /// aggregate property's own name (the attribute's default); otherwise the source is referenced
        /// explicitly, as <c>nameof(...)</c> when it resolves on the parent entity.
        /// </summary>
        private static AttributeCodeFragment GenerateGroupByColumnAttribute(
            IProperty property, string columnName, IEntityType? parentEntityType)
        {
            object sourceArg = ResolveSourceArgByColumnName(columnName, parentEntityType);

            return sourceArg is NameOfCodeFragment nameOf && nameOf.PropertyName.EndsWith("." + property.Name, StringComparison.Ordinal)
                ? new AttributeCodeFragment(typeof(GroupByColumnAttribute))
                : new AttributeCodeFragment(typeof(GroupByColumnAttribute), sourceArg);
        }

        private static object ResolveSourceArgByClrName(string clrName, IEntityType? parentEntityType)
        {
            if (clrName == "*") return "*";
            if (parentEntityType is null) return clrName;
            IProperty? parentProp = parentEntityType.FindProperty(clrName);
            return parentProp is not null
                ? new NameOfCodeFragment($"{parentEntityType.ShortName()}.{parentProp.Name}")
                : (object)clrName;
        }

        private static object ResolveSourceArgByColumnName(string columnName, IEntityType? parentEntityType)
        {
            if (columnName == "*") return "*";
            if (parentEntityType is null) return columnName;
            return AnnotationRendererHelper.TryResolvePropertyName(parentEntityType, columnName, out string propertyName)
                ? new NameOfCodeFragment($"{parentEntityType.ShortName()}.{propertyName}")
                : (object)columnName;
        }
    }
}
#pragma warning restore EF1001

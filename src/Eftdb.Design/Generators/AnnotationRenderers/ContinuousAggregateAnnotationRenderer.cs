using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.AnnotationRendererHelper;

#pragma warning disable EF1001 // Suppress warning about internal APIs usage, common for providers/extensions
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers
{
    /// <summary>
    /// Renders ContinuousAggregate annotations as <c>IsContinuousAggregate(...)</c> Fluent API chains or
    /// <c>[ContinuousAggregate]</c> attributes. The <c>ViewDefinition</c> annotation is parsed to reconstruct
    /// the structured configuration; on success it is consumed so no raw SQL leaks into the generated file.
    /// When the view definition cannot be parsed, a warning is reported and the annotations are left in
    /// place so the <c>.HasAnnotation(...)</c> fallback preserves the configuration.
    /// </summary>
    internal sealed class ContinuousAggregateAnnotationRenderer(IOperationReporter reporter) : IFeatureAnnotationRenderer
    {
        private static readonly MethodInfo IsContinuousAggregateMethod =
            typeof(ContinuousAggregateTypeBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)
                         && m.GetParameters().Length == 5);

        private static MethodInfo BuilderMethod(string name) =>
            typeof(ContinuousAggregateStringBuilder<>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == name);

        private static readonly MethodInfo AddAggregateFunctionMethod = BuilderMethod("AddAggregateFunction");
        private static readonly MethodInfo AddGroupByColumnMethod = BuilderMethod("AddGroupByColumn");
        private static readonly MethodInfo WhereMethod = BuilderMethod("Where");
        private static readonly MethodInfo MaterializedOnlyMethod = BuilderMethod("MaterializedOnly");
        private static readonly MethodInfo WithNoDataMethod = BuilderMethod("WithNoData");
        private static readonly MethodInfo CreateGroupIndexesMethod = BuilderMethod("CreateGroupIndexes");
        private static readonly MethodInfo WithChunkIntervalMethod = BuilderMethod("WithChunkInterval");

        public IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            string? materializedViewName = GetString(annotations, ContinuousAggregateAnnotations.MaterializedViewName);
            if (materializedViewName is null)
            {
                return [];
            }

            string? viewDefinition = GetString(annotations, ContinuousAggregateAnnotations.ViewDefinition);
            string? parentName = GetString(annotations, ContinuousAggregateAnnotations.ParentName);
            string? chunkInterval = GetString(annotations, ContinuousAggregateAnnotations.ChunkInterval);
            bool materializedOnly = Find(annotations, ContinuousAggregateAnnotations.MaterializedOnly)?.Value is true;
            bool withNoData = Find(annotations, ContinuousAggregateAnnotations.WithNoData)?.Value is true;
            bool createGroupIndexes = Find(annotations, ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value as bool? ?? true;

            ViewDefinitionParser.ParsedViewDefinition? parsed = viewDefinition is not null
                ? ViewDefinitionParser.Parse(viewDefinition)
                : null;

            if (parsed?.TimeBucketWidth is null || parsed.TimeBucketSourceColumn is null)
            {
                ReportUnparseableViewDefinition(materializedViewName);
                return [];
            }

            string humanizedWidth = IntervalParsingHelper.NormalizeInterval(parsed.TimeBucketWidth);

            IEntityType? parentEntityType = ParentEntityTypeResolver.Resolve(entityType.Model, parentName);
            string parentClrName = parentEntityType?.ShortName() ?? parentName ?? materializedViewName;

            object parentNameArg = parentEntityType is not null
                ? new NameOfCodeFragment(parentClrName)
                : (object)(parentName ?? string.Empty);

            object timeBucketArg = ResolveParentColumnArg(parentEntityType, parentClrName, parsed.TimeBucketSourceColumn);

            MethodCallCodeFragment call = new(IsContinuousAggregateMethod, materializedViewName, parentNameArg, humanizedWidth, timeBucketArg);

            if (materializedOnly)
            {
                call = call.Chain(MaterializedOnlyMethod, true);
            }

            if (withNoData)
            {
                call = call.Chain(WithNoDataMethod, true);
            }

            if (!createGroupIndexes)
            {
                call = call.Chain(CreateGroupIndexesMethod, false);
            }

            string caEntityClrName = entityType.ShortName();

            foreach (ViewDefinitionParser.ParsedAggregate agg in parsed.Aggregates)
            {
                object aliasArg = TryResolvePropertyName(entityType, agg.Alias, out string aliasProperty)
                    ? new NameOfCodeFragment($"{caEntityClrName}.{aliasProperty}")
                    : (object)agg.Alias;

                object sourceArg = agg.SourceColumn == "*"
                    ? (object)"*"
                    : ResolveParentColumnArg(parentEntityType, parentClrName, agg.SourceColumn);

                call = call.Chain(AddAggregateFunctionMethod, [aliasArg, sourceArg, agg.Function]);
            }

            foreach (string col in parsed.GroupByColumns)
            {
                call = call.Chain(AddGroupByColumnMethod, ResolveParentColumnArg(parentEntityType, parentClrName, col));
            }

            if (!string.IsNullOrWhiteSpace(parsed.WhereClause))
            {
                call = call.Chain(WhereMethod, parsed.WhereClause);
            }

            if (!string.IsNullOrWhiteSpace(chunkInterval) && !IsDerivedDefaultChunkInterval(chunkInterval, parentEntityType))
            {
                call = call.Chain(WithChunkIntervalMethod, IntervalParsingHelper.NormalizeInterval(chunkInterval));
            }

            ConsumeAllCaAnnotations(annotations);
            return [call];
        }

        public IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            string? materializedViewName = GetString(annotations, ContinuousAggregateAnnotations.MaterializedViewName);
            if (materializedViewName is null)
            {
                return [];
            }

            string? viewDefinition = GetString(annotations, ContinuousAggregateAnnotations.ViewDefinition);
            string? parentName = GetString(annotations, ContinuousAggregateAnnotations.ParentName);
            string? chunkInterval = GetString(annotations, ContinuousAggregateAnnotations.ChunkInterval);
            bool materializedOnly = Find(annotations, ContinuousAggregateAnnotations.MaterializedOnly)?.Value is true;
            bool withNoData = Find(annotations, ContinuousAggregateAnnotations.WithNoData)?.Value is true;
            bool createGroupIndexes = Find(annotations, ContinuousAggregateAnnotations.CreateGroupIndexes)?.Value as bool? ?? true;

            ViewDefinitionParser.ParsedViewDefinition? parsed = viewDefinition is not null
                ? ViewDefinitionParser.Parse(viewDefinition)
                : null;

            if (parsed?.TimeBucketWidth is null || parsed.TimeBucketSourceColumn is null)
            {
                return [];
            }

            ConsumeAllCaAnnotations(annotations);

            string humanizedWidth = IntervalParsingHelper.NormalizeInterval(parsed.TimeBucketWidth);

            IEntityType? parentEntityType = ParentEntityTypeResolver.Resolve(entityType.Model, parentName);
            string parentClrName = parentEntityType?.ShortName() ?? parentName ?? materializedViewName;

            object parentNameArg = parentEntityType is not null
                ? new NameOfCodeFragment(parentClrName)
                : (object)(parentName ?? string.Empty);

            object timeBucketArg = ResolveParentColumnArg(parentEntityType, parentClrName, parsed.TimeBucketSourceColumn);

            ReportUnrepresentableGroupByEntries(entityType, materializedViewName, parsed.GroupByColumns);

            Dictionary<string, object?> caNamedArgs = new()
            {
                [nameof(ContinuousAggregateAttribute.MaterializedViewName)] = materializedViewName,
                [nameof(ContinuousAggregateAttribute.ParentName)] = parentNameArg,
            };

            if (!string.IsNullOrWhiteSpace(chunkInterval) && !IsDerivedDefaultChunkInterval(chunkInterval, parentEntityType))
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.ChunkInterval)] = IntervalParsingHelper.NormalizeInterval(chunkInterval);
            }

            if (materializedOnly)
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.MaterializedOnly)] = true;
            }

            if (withNoData)
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.WithNoData)] = true;
            }

            if (!createGroupIndexes)
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.CreateGroupIndexes)] = false;
            }

            if (!string.IsNullOrWhiteSpace(parsed.WhereClause))
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.Where)] = parsed.WhereClause;
            }

            return [
                new AttributeCodeFragment(typeof(ContinuousAggregateAttribute), [], caNamedArgs),
                new AttributeCodeFragment(typeof(TimeBucketAttribute), humanizedWidth, timeBucketArg),
            ];
        }

        public void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            string? materializedViewName = GetString(annotations, ContinuousAggregateAnnotations.MaterializedViewName);
            if (materializedViewName is null)
            {
                return;
            }

            string? viewDefinition = GetString(annotations, ContinuousAggregateAnnotations.ViewDefinition);
            ViewDefinitionParser.ParsedViewDefinition? parsed = viewDefinition is not null
                ? ViewDefinitionParser.Parse(viewDefinition)
                : null;

            if (parsed?.TimeBucketWidth is null || parsed.TimeBucketSourceColumn is null)
            {
                ReportUnparseableViewDefinition(materializedViewName);
                return;
            }

            ConsumeAllCaAnnotations(annotations);
        }

        /// <summary>
        /// A continuous aggregate's chunk interval defaults to 10x the parent hypertable's chunk
        /// interval; a value equal to that derived default is elided, symmetric with the hypertable's
        /// 7-days elision. Calendar-unit intervals fail the microsecond conversion and are kept.
        /// </summary>
        private static bool IsDerivedDefaultChunkInterval(string chunkInterval, IEntityType? parentEntityType)
        {
            string parentChunkInterval = parentEntityType?.FindAnnotation(HypertableAnnotations.ChunkTimeInterval)?.Value as string
                ?? DefaultValues.ChunkTimeInterval;

            return IntervalParsingHelper.TryGetTotalMicroseconds(chunkInterval, out long caMicroseconds)
                && IntervalParsingHelper.TryGetTotalMicroseconds(parentChunkInterval, out long parentMicroseconds)
                && caMicroseconds == 10 * parentMicroseconds;
        }

        private void ReportUnparseableViewDefinition(string materializedViewName)
            => reporter.WriteWarning(
                $"The view definition of continuous aggregate '{materializedViewName}' could not be parsed. " +
                "Its configuration is preserved as .HasAnnotation(...) calls; migrations will recreate the " +
                "view from the raw SQL definition.");

        /// <summary>
        /// GROUP BY entries that match no property on the aggregate entity (raw SQL expressions or
        /// unmapped columns) have no data-annotation representation and would be silently lost.
        /// </summary>
        private void ReportUnrepresentableGroupByEntries(
            IEntityType entityType, string materializedViewName, IReadOnlyList<string> groupByColumns)
        {
            foreach (string col in groupByColumns)
            {
                if (!TryResolvePropertyName(entityType, col, out _))
                {
                    reporter.WriteWarning(
                        $"The GROUP BY expression '{col}' of continuous aggregate '{materializedViewName}' " +
                        "cannot be represented as a data annotation. Scaffold without --data-annotations or " +
                        "configure it manually via AddGroupByColumn(...).");
                }
            }
        }

        private static object ResolveParentColumnArg(IEntityType? parentEntityType, string parentClrName, string columnName)
            => parentEntityType is not null && TryResolvePropertyName(parentEntityType, columnName, out string propName)
                ? new NameOfCodeFragment($"{parentClrName}.{propName}")
                : (object)columnName;

        private static void ConsumeAllCaAnnotations(IDictionary<string, IAnnotation> annotations)
        {
            Consume(annotations,
                ContinuousAggregateAnnotations.MaterializedViewName,
                ContinuousAggregateAnnotations.ParentName,
                ContinuousAggregateAnnotations.MaterializedOnly,
                ContinuousAggregateAnnotations.ChunkInterval,
                ContinuousAggregateAnnotations.ViewDefinition,
                ContinuousAggregateAnnotations.TimeBucketWidth,
                ContinuousAggregateAnnotations.TimeBucketSourceColumn,
                ContinuousAggregateAnnotations.TimeBucketGroupBy,
                ContinuousAggregateAnnotations.AggregateFunctions,
                ContinuousAggregateAnnotations.GroupByColumns,
                ContinuousAggregateAnnotations.WhereClause,
                ContinuousAggregateAnnotations.WithNoData,
                ContinuousAggregateAnnotations.CreateGroupIndexes);
        }
    }
}
#pragma warning restore EF1001

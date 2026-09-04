using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRendererHelper;

#pragma warning disable EF1001 // Suppress warning about internal APIs usage, common for providers/extensions
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregate
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
        private static readonly MethodInfo WithCompressionMethod = BuilderMethod("WithCompression");
        private static readonly MethodInfo WithCompressionSegmentByMethod = BuilderMethod("WithCompressionSegmentBy");
        private static readonly MethodInfo WithCompressionOrderByMethod = BuilderMethod("WithCompressionOrderBy");
        private static readonly MethodInfo WithTimeBucketPropertyMethod = BuilderMethod("WithTimeBucketProperty");

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

            string caEntityClrName = entityType.ShortName();
            if (TryResolveTimeBucketProperty(entityType, parsed.TimeBucketAlias, out string bucketProperty))
            {
                call = call.Chain(WithTimeBucketPropertyMethod, new NameOfCodeFragment($"{caEntityClrName}.{bucketProperty}"));
            }

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

            bool compressionConfigured = false;

            string segmentBy = GetString(annotations, HypertableAnnotations.CompressionSegmentBy) ?? "";
            if (!string.IsNullOrWhiteSpace(segmentBy))
            {
                call = call.Chain(WithCompressionSegmentByMethod, CompressionColumnsArg(entityType, caEntityClrName, segmentBy, isOrderBy: false));
                compressionConfigured = true;
            }

            string orderBy = GetString(annotations, HypertableAnnotations.CompressionOrderBy) ?? "";
            if (!string.IsNullOrWhiteSpace(orderBy))
            {
                call = call.Chain(WithCompressionOrderByMethod, CompressionColumnsArg(entityType, caEntityClrName, orderBy, isOrderBy: true));
                compressionConfigured = true;
            }

            if (!compressionConfigured && Find(annotations, HypertableAnnotations.EnableCompression)?.Value is true)
            {
                call = call.Chain(WithCompressionMethod);
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

            bool enableCompression = Find(annotations, HypertableAnnotations.EnableCompression)?.Value is true;
            string? compressionSegmentBy = GetString(annotations, HypertableAnnotations.CompressionSegmentBy);
            string? compressionOrderBy = GetString(annotations, HypertableAnnotations.CompressionOrderBy);

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

            bool hasSegmentBy = !string.IsNullOrWhiteSpace(compressionSegmentBy);
            bool hasOrderBy = !string.IsNullOrWhiteSpace(compressionOrderBy);

            if (enableCompression && !hasSegmentBy && !hasOrderBy)
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.EnableCompression)] = true;
            }

            if (hasSegmentBy)
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.CompressionSegmentBy)] =
                    ToArgumentArray([.. SplitColumns(compressionSegmentBy).Select(column => ColumnReference(entityType, column))]);
            }

            if (hasOrderBy)
            {
                caNamedArgs[nameof(ContinuousAggregateAttribute.CompressionOrderBy)] =
                    ToArgumentArray([.. SplitColumns(compressionOrderBy).Select(entry => OrderByReference(entityType, entry))]);
            }

            if (TryResolveTimeBucketProperty(entityType, parsed.TimeBucketAlias, out _))
            {
                return [new AttributeCodeFragment(typeof(ContinuousAggregateAttribute), [], caNamedArgs)];
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
                ?? parentEntityType?.FindAnnotation(ContinuousAggregateAnnotations.ChunkInterval)?.Value as string
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

        /// <summary>
        /// Resolves the view's bucket alias to the CLR property whose mapped column matches it, when the
        /// alias differs from the default <c>time_bucket</c>. The default alias needs no designation, so it
        /// yields <c>false</c> and undesignated aggregates keep rendering without a
        /// <c>WithTimeBucketProperty</c> call.
        /// </summary>
        private static bool TryResolveTimeBucketProperty(IEntityType entityType, string? bucketAlias, out string propertyName)
        {
            propertyName = string.Empty;
            if (string.IsNullOrWhiteSpace(bucketAlias)
                || string.Equals(bucketAlias, DefaultValues.ContinuousAggregateTimeBucketColumnName, StringComparison.Ordinal))
            {
                return false;
            }

            return TryResolvePropertyName(entityType, bucketAlias, out propertyName);
        }

        private static object ResolveParentColumnArg(IEntityType? parentEntityType, string parentClrName, string columnName)
            => parentEntityType is not null && TryResolvePropertyName(parentEntityType, columnName, out string propName)
                ? new NameOfCodeFragment($"{parentClrName}.{propName}")
                : (object)columnName;

        /// <summary>
        /// Builds the single-string compression argument for the fluent chain.
        /// </summary>
        private static object CompressionColumnsArg(IEntityType entityType, string caEntityClrName, string raw, bool isOrderBy)
        {
            List<object> entries = [];
            bool anyResolved = false;

            foreach (string entry in SplitColumns(raw))
            {
                object reference = isOrderBy ? OrderByReference(entityType, entry) : ColumnReference(entityType, entry);
                if (reference is NameOfCodeFragment nameOf)
                {
                    anyResolved = true;
                    entries.Add(new NameOfCodeFragment($"{caEntityClrName}.{nameOf.PropertyName}", nameOf.Suffix));
                }
                else
                {
                    entries.Add(reference);
                }
            }

            return anyResolved ? new ColumnListCodeFragment(entries) : raw;
        }

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
                ContinuousAggregateAnnotations.TimeBucketTargetProperty,
                ContinuousAggregateAnnotations.AggregateFunctions,
                ContinuousAggregateAnnotations.GroupByColumns,
                ContinuousAggregateAnnotations.WhereClause,
                ContinuousAggregateAnnotations.WithNoData,
                ContinuousAggregateAnnotations.CreateGroupIndexes,
                HypertableAnnotations.EnableCompression,
                HypertableAnnotations.CompressionSegmentBy,
                HypertableAnnotations.CompressionOrderBy);
        }
    }
}
#pragma warning restore EF1001

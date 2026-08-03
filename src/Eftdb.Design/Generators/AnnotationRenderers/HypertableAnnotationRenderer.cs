using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using System.Text.Json;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.AnnotationRendererHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers
{
    /// <summary>
    /// Renders the Hypertable feature's annotations as <c>IsHypertable(...)</c> Fluent API chains or
    /// <c>[Hypertable]</c>/<c>[Dimension]</c> attributes.
    /// </summary>
    internal sealed class HypertableAnnotationRenderer : IFeatureAnnotationRenderer
    {
        private static readonly Type BuilderType = typeof(HypertableTypeBuilder);

        // Rendering only uses the method name and declaring namespace, so any overload's MethodInfo is fine.
        private static MethodInfo HypertableMethod(string name) =>
            BuilderType.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == name);

        private static readonly MethodInfo IsHypertableMethod = HypertableMethod(nameof(HypertableTypeBuilder.IsHypertable));
        private static readonly MethodInfo WithChunkTimeIntervalMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithChunkTimeInterval));
        private static readonly MethodInfo EnableCompressionMethod = HypertableMethod(nameof(HypertableTypeBuilder.EnableCompression));
        private static readonly MethodInfo WithCompressionSegmentByMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithCompressionSegmentBy));
        private static readonly MethodInfo WithCompressionOrderByMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithCompressionOrderBy));
        private static readonly MethodInfo WithChunkSkippingMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithChunkSkipping));
        private static readonly MethodInfo WithMigrateDataMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithMigrateData));
        private static readonly MethodInfo HasRangeDimensionMethod = HypertableMethod(nameof(HypertableTypeBuilder.HasRangeDimension));
        private static readonly MethodInfo HasHashDimensionMethod = HypertableMethod(nameof(HypertableTypeBuilder.HasHashDimension));
        private static readonly MethodInfo WithSparseIndexMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithSparseIndex));
        private static readonly MethodInfo WithoutAutoSparseIndexesMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithoutAutoSparseIndexes));
        private static readonly MethodInfo WithCompressChunkTimeIntervalMethod = HypertableMethod(nameof(HypertableTypeBuilder.WithCompressChunkTimeInterval));

        public IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (Find(annotations, HypertableAnnotations.IsHypertable)?.Value is not true)
            {
                return [];
            }

            if (GetString(annotations, HypertableAnnotations.HypertableTimeColumn) is not string timeColumn || string.IsNullOrWhiteSpace(timeColumn))
            {
                return [];
            }

            string timeProperty = ResolvePropertyName(entityType, timeColumn);
            MethodCallCodeFragment call = new(IsHypertableMethod, PropertyAccessor(timeProperty));

            if (GetString(annotations, HypertableAnnotations.ChunkTimeInterval) is string interval && !string.IsNullOrWhiteSpace(interval))
            {
                call = call.Chain(WithChunkTimeIntervalMethod, interval);
            }

            // WithCompressionSegmentBy, WithCompressionOrderBy, WithSparseIndex, and
            // WithCompressChunkTimeInterval all implicitly enable compression. A separate
            // EnableCompression call is only emitted when none of them is rendered.
            bool compressionConfigured = false;

            string[] segmentBy = ResolveColumns(entityType, GetString(annotations, HypertableAnnotations.CompressionSegmentBy));
            if (segmentBy.Length > 0)
            {
                call = call.Chain(WithCompressionSegmentByMethod, [.. segmentBy.Select(PropertyAccessor)]);
                compressionConfigured = true;
            }

            MethodCallCodeFragment? orderBy = GenerateCompressionOrderByFluent(entityType, GetString(annotations, HypertableAnnotations.CompressionOrderBy));
            if (orderBy != null)
            {
                call = call.Chain(orderBy);
                compressionConfigured = true;
                Consume(annotations, HypertableAnnotations.CompressionOrderBy);
            }

            IAnnotation? sparseIndexAnnotation = Find(annotations, HypertableAnnotations.CompressionSparseIndex);
            if (sparseIndexAnnotation?.Value is string sparseIndex)
            {
                call = sparseIndex.Length == 0
                    ? call.Chain(WithoutAutoSparseIndexesMethod)
                    : call.Chain(WithSparseIndexMethod, sparseIndex);
                compressionConfigured = true;
            }

            if (GetString(annotations, HypertableAnnotations.CompressChunkTimeInterval) is string compressInterval
                && !string.IsNullOrWhiteSpace(compressInterval))
            {
                call = call.Chain(WithCompressChunkTimeIntervalMethod, compressInterval);
                compressionConfigured = true;
            }

            if (!compressionConfigured && Find(annotations, HypertableAnnotations.EnableCompression)?.Value is true)
            {
                call = call.Chain(EnableCompressionMethod);
            }

            string[] chunkSkip = ResolveColumns(entityType, GetString(annotations, HypertableAnnotations.ChunkSkipColumns));
            if (chunkSkip.Length > 0)
            {
                call = call.Chain(WithChunkSkippingMethod, [.. chunkSkip.Select(PropertyAccessor)]);
            }

            if (Find(annotations, HypertableAnnotations.MigrateData)?.Value is true)
            {
                call = call.Chain(WithMigrateDataMethod);
            }

            List<Dimension>? dimensions = TryReadDimensions(GetString(annotations, HypertableAnnotations.AdditionalDimensions));
            if (dimensions is { Count: > 0 })
            {
                foreach (Dimension dimension in dimensions)
                {
                    PropertyAccessorCodeFragment column = PropertyAccessor(ResolvePropertyName(entityType, dimension.ColumnName));
                    call = dimension.Type == EDimensionType.Hash
                        ? call.Chain(HasHashDimensionMethod, [column, dimension.NumberOfPartitions ?? 0])
                        : call.Chain(HasRangeDimensionMethod, [column, dimension.Interval ?? string.Empty]);
                }

                Consume(annotations, HypertableAnnotations.AdditionalDimensions);
            }

            Consume(annotations,
                HypertableAnnotations.IsHypertable,
                HypertableAnnotations.HypertableTimeColumn,
                HypertableAnnotations.ChunkTimeInterval,
                HypertableAnnotations.EnableCompression,
                HypertableAnnotations.CompressionSegmentBy,
                HypertableAnnotations.ChunkSkipColumns,
                HypertableAnnotations.MigrateData,
                HypertableAnnotations.CompressionSparseIndex,
                HypertableAnnotations.CompressChunkTimeInterval);

            return [call];
        }

        public IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            AttributeCodeFragment? hypertable = GenerateHypertableAttribute(entityType, annotations);
            return hypertable == null
                ? []
                : [hypertable, .. GenerateSparseIndexAttributes(annotations), .. GenerateDimensionAttributes(entityType, annotations)];
        }

        public void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (Find(annotations, HypertableAnnotations.IsHypertable)?.Value is not true)
            {
                return;
            }

            Consume(annotations,
                HypertableAnnotations.IsHypertable,
                HypertableAnnotations.HypertableTimeColumn,
                HypertableAnnotations.ChunkTimeInterval,
                HypertableAnnotations.EnableCompression,
                HypertableAnnotations.CompressionSegmentBy,
                HypertableAnnotations.CompressionOrderBy,
                HypertableAnnotations.ChunkSkipColumns,
                HypertableAnnotations.MigrateData,
                HypertableAnnotations.AdditionalDimensions,
                HypertableAnnotations.CompressionSparseIndex,
                HypertableAnnotations.CompressChunkTimeInterval);
        }

        private static AttributeCodeFragment? GenerateHypertableAttribute(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (Find(annotations, HypertableAnnotations.IsHypertable)?.Value is not true)
            {
                return null;
            }

            if (GetString(annotations, HypertableAnnotations.HypertableTimeColumn) is not string timeColumn || string.IsNullOrWhiteSpace(timeColumn))
            {
                return null;
            }

            Dictionary<string, object?> named = [];

            if (Find(annotations, HypertableAnnotations.EnableCompression)?.Value is true)
            {
                named[nameof(HypertableAttribute.EnableCompression)] = true;
            }

            if (GetString(annotations, HypertableAnnotations.ChunkTimeInterval) is string interval
                && !string.IsNullOrWhiteSpace(interval)
                && interval != DefaultValues.ChunkTimeInterval)
            {
                named[nameof(HypertableAttribute.ChunkTimeInterval)] = interval;
            }

            if (Find(annotations, HypertableAnnotations.MigrateData)?.Value is true)
            {
                named[nameof(HypertableAttribute.MigrateData)] = true;
            }

            object[] segmentBy = [.. SplitColumns(GetString(annotations, HypertableAnnotations.CompressionSegmentBy)).Select(column => ColumnReference(entityType, column))];
            if (segmentBy.Length > 0)
            {
                named[nameof(HypertableAttribute.CompressionSegmentBy)] = ToArgumentArray(segmentBy);
            }

            object[] orderBy = [.. SplitColumns(GetString(annotations, HypertableAnnotations.CompressionOrderBy)).Select(entry => OrderByReference(entityType, entry))];
            if (orderBy.Length > 0)
            {
                named[nameof(HypertableAttribute.CompressionOrderBy)] = ToArgumentArray(orderBy);
            }

            object[] chunkSkip = [.. SplitColumns(GetString(annotations, HypertableAnnotations.ChunkSkipColumns)).Select(column => ColumnReference(entityType, column))];
            if (chunkSkip.Length > 0)
            {
                named[nameof(HypertableAttribute.ChunkSkipColumns)] = ToArgumentArray(chunkSkip);
            }

            IAnnotation? sparseIndexAnnotation = Find(annotations, HypertableAnnotations.CompressionSparseIndex);
            if (sparseIndexAnnotation?.Value is string sparseIndex && sparseIndex.Length == 0)
            {
                named[nameof(HypertableAttribute.DisableAutoSparseIndexes)] = true;
            }

            if (GetString(annotations, HypertableAnnotations.CompressChunkTimeInterval) is string compressInterval
                && !string.IsNullOrWhiteSpace(compressInterval))
            {
                named[nameof(HypertableAttribute.CompressChunkTimeInterval)] = compressInterval;
            }

            Consume(annotations,
                HypertableAnnotations.IsHypertable,
                HypertableAnnotations.HypertableTimeColumn,
                HypertableAnnotations.ChunkTimeInterval,
                HypertableAnnotations.EnableCompression,
                HypertableAnnotations.CompressionSegmentBy,
                HypertableAnnotations.CompressionOrderBy,
                HypertableAnnotations.ChunkSkipColumns,
                HypertableAnnotations.MigrateData,
                HypertableAnnotations.CompressChunkTimeInterval);

            if (sparseIndexAnnotation?.Value is string sparseVal && sparseVal.Length == 0)
            {
                Consume(annotations, HypertableAnnotations.CompressionSparseIndex);
            }

            return new AttributeCodeFragment(typeof(HypertableAttribute), [ColumnReference(entityType, timeColumn)], named);
        }

        /// <summary>
        /// References a column as <c>nameof(Property)</c> when it resolves to a CLR property on the entity;
        /// falls back to the raw string for unmapped columns, where a <c>nameof</c> would not compile.
        /// </summary>
        private static object ColumnReference(IEntityType entityType, string column, string suffix = "")
            => TryResolvePropertyName(entityType, column, out string property)
                ? new NameOfCodeFragment(property, suffix)
                : suffix.Length == 0 ? column : column + suffix;

        // Splits a "column [ASC|DESC] [NULLS ...]" entry into a property reference plus literal suffix.
        private static object OrderByReference(IEntityType entityType, string entry)
        {
            int space = entry.IndexOf(' ');
            return space < 0
                ? ColumnReference(entityType, entry)
                : ColumnReference(entityType, entry[..space], entry[space..]);
        }

        private static object ToArgumentArray(object[] entries)
            => Array.Exists(entries, entry => entry is NameOfCodeFragment)
                ? entries
                : Array.ConvertAll(entries, entry => (string)entry);

        /// <summary>
        /// Renders non-empty <c>timescaledb.sparse_index</c> entries as one <c>[SparseIndex]</c>
        /// attribute per entry. The empty-string case is handled as <c>DisableAutoSparseIndexes = true</c>
        /// on the <c>[Hypertable]</c> attribute and does not produce any fragments here.
        /// </summary>
        private static List<AttributeCodeFragment> GenerateSparseIndexAttributes(IDictionary<string, IAnnotation> annotations)
        {
            IAnnotation? sparseIndexAnnotation = Find(annotations, HypertableAnnotations.CompressionSparseIndex);
            if (sparseIndexAnnotation?.Value is not string raw || raw.Length == 0)
            {
                return [];
            }

            List<AttributeCodeFragment> attributes = [];
            foreach (string entry in CompressionAnnotationExtractor.SplitSparseIndexEntries(raw))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                int parenOpen = trimmed.IndexOf('(');
                int parenClose = trimmed.LastIndexOf(')');
                if (parenOpen < 0 || parenClose < parenOpen)
                {
                    continue;
                }

                string funcName = trimmed[..parenOpen].Trim();
                ESparseIndexType kind = string.Equals(funcName, "minmax", StringComparison.OrdinalIgnoreCase)
                    ? ESparseIndexType.MinMax
                    : ESparseIndexType.Bloom;

                string argsPart = trimmed[(parenOpen + 1)..parenClose];
                string[] columns = [.. argsPart.Split(',', StringSplitOptions.TrimEntries).Where(c => c.Length > 0)];
                if (columns.Length == 0)
                {
                    continue;
                }

                List<object> positional = [kind];
                foreach (string col in columns)
                {
                    positional.Add(col);
                }

                attributes.Add(new AttributeCodeFragment(typeof(SparseIndexAttribute), positional.ToArray(), new Dictionary<string, object?>()));
            }

            Consume(annotations, HypertableAnnotations.CompressionSparseIndex);

            return attributes;
        }

        private static List<AttributeCodeFragment> GenerateDimensionAttributes(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            List<Dimension>? dimensions = TryReadDimensions(GetString(annotations, HypertableAnnotations.AdditionalDimensions));
            if (dimensions is not { Count: > 0 })
            {
                return [];
            }

            List<AttributeCodeFragment> attributes = [];
            foreach (Dimension dimension in dimensions)
            {
                object column = ColumnReference(entityType, dimension.ColumnName);
                attributes.Add(dimension.Type == EDimensionType.Hash
                    ? new AttributeCodeFragment(typeof(DimensionAttribute), column, EDimensionType.Hash, dimension.NumberOfPartitions ?? 0)
                    : new AttributeCodeFragment(typeof(DimensionAttribute), column, EDimensionType.Range, dimension.Interval ?? string.Empty));
            }

            Consume(annotations, HypertableAnnotations.AdditionalDimensions);
            return attributes;
        }

        private static List<Dimension>? TryReadDimensions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<List<Dimension>>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Renders CompressionOrderBy as <c>.WithCompressionOrderBy(s =&gt; s.ByDescending(x =&gt; x.Time), ...)</c>
        /// with one selector closure per column. Returns <c>null</c> for empty input.
        /// </summary>
        private static MethodCallCodeFragment? GenerateCompressionOrderByFluent(IEntityType entityType, string? value)
        {
            string[] entries = SplitColumns(value);
            if (entries.Length == 0)
            {
                return null;
            }

            object?[] closures = [.. entries.Select(entry => OrderBySelectorClosure(entityType, entry))];

            return new MethodCallCodeFragment(WithCompressionOrderByMethod, closures);
        }

        private static NestedClosureCodeFragment OrderBySelectorClosure(IEntityType entityType, string entry)
        {
            (string column, bool? isAscending, bool? nullsFirst) = ParseOrderByEntry(entry);
            string property = ResolvePropertyName(entityType, column);

            string selectorMethod = isAscending switch
            {
                true => nameof(OrderBySelector<>.ByAscending),
                false => nameof(OrderBySelector<>.ByDescending),
                null => nameof(OrderBySelector<>.By),
            };

            object?[] arguments = nullsFirst.HasValue
                ? [PropertyAccessor(property), nullsFirst.Value]
                : [PropertyAccessor(property)];

            return new NestedClosureCodeFragment("s", new MethodCallCodeFragment(selectorMethod, arguments));
        }

        private static (string Column, bool? IsAscending, bool? NullsFirst) ParseOrderByEntry(string entry)
        {
            string[] tokens = entry.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string column = tokens[0];
            string rest = string.Join(' ', tokens[1..]).ToUpperInvariant();

            bool? isAscending = rest.Contains("ASC") ? true : rest.Contains("DESC") ? false : null;
            bool? nullsFirst = rest.Contains("NULLS FIRST") ? true : rest.Contains("NULLS LAST") ? false : null;

            return (column, isAscending, nullsFirst);
        }
    }
}

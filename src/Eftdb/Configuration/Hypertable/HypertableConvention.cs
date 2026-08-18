using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// A convention that configures an entity as a hypertable based on the presence of
    /// the [Hypertable] attribute.
    /// </summary>
    internal class HypertableConvention : IEntityTypeAddedConvention
    {
        /// <summary>
        /// Called when an entity type is added to the model.
        /// </summary>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="context">Additional information available during convention execution.</param>
        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            HypertableAttribute? attribute = entityType.ClrType?.GetCustomAttribute<HypertableAttribute>();

            if (attribute != null)
            {
                // Apply the annotations that the Fluent API would have applied.
                entityTypeBuilder.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                entityTypeBuilder.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, attribute.TimeColumnName);

                if (!string.IsNullOrEmpty(attribute.ChunkTimeInterval))
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.ChunkTimeInterval, attribute.ChunkTimeInterval);
                }

                if (attribute.EnableCompression == true)
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                }

                if (attribute.MigrateData == true)
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.MigrateData, true);
                }

                if (attribute.ChunkSkipColumns != null && attribute.ChunkSkipColumns.Length > 0)
                {
                    // Chunk skipping requires compression to be enabled
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.ChunkSkipColumns, string.Join(",", attribute.ChunkSkipColumns));
                }

                if (attribute.CompressionSegmentBy != null && attribute.CompressionSegmentBy.Length > 0)
                {
                    // SegmentBy requires compression to be enabled
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionSegmentBy, string.Join(", ", attribute.CompressionSegmentBy));
                }

                if (attribute.CompressionOrderBy != null && attribute.CompressionOrderBy.Length > 0)
                {
                    // OrderBy requires compression to be enabled
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionOrderBy, string.Join(", ", attribute.CompressionOrderBy));
                }

                SparseIndexAttribute[] sparseIndexAttributes = entityType.ClrType?.GetCustomAttributes<SparseIndexAttribute>().ToArray() ?? [];
                bool hasDisable = attribute.DisableAutoSparseIndexes;
                bool hasSparseIndexAttrs = sparseIndexAttributes.Length > 0;

                if (hasDisable && hasSparseIndexAttrs)
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityType.ClrType?.Name}' has both [SparseIndex] attributes and " +
                        $"{nameof(HypertableAttribute.DisableAutoSparseIndexes)} = true on [Hypertable]. " +
                        "These are mutually exclusive — remove one or the other.");
                }

                if (hasDisable)
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionSparseIndex, string.Empty);
                }
                else if (hasSparseIndexAttrs)
                {
                    string annotationValue = string.Join(", ", sparseIndexAttributes.Select(a =>
                    {
                        string func = a.Kind == ESparseIndexType.Bloom ? "bloom" : "minmax";
                        return $"{func}({string.Join(",", a.Columns)})";
                    }));

                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressionSparseIndex, annotationValue);
                }

                if (!string.IsNullOrWhiteSpace(attribute.CompressChunkTimeInterval))
                {
                    // CompressChunkTimeInterval requires compression to be enabled
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.CompressChunkTimeInterval, attribute.CompressChunkTimeInterval);
                }
            }

            DimensionAttribute[] dimensionAttributes = entityType.ClrType?.GetCustomAttributes<DimensionAttribute>().ToArray() ?? [];
            if (dimensionAttributes.Length > 0)
            {
                List<Dimension> dimensions = [.. dimensionAttributes.Select(ToDimension)];
                entityTypeBuilder.HasAnnotation(HypertableAnnotations.AdditionalDimensions, JsonSerializer.Serialize(dimensions));
            }
        }

        private static Dimension ToDimension(DimensionAttribute attribute)
            => attribute.Type == EDimensionType.Hash
                ? Dimension.CreateHash(attribute.ColumnName, attribute.NumberOfPartitions)
                : Dimension.CreateRange(attribute.ColumnName, attribute.Interval!);
    }
}

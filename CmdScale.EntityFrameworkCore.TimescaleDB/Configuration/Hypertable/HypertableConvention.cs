using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// A convention that configures an entity as a hypertable based on the presence of
    /// the [Hypertable] attribute.
    /// </summary>
    public class HypertableConvention : IEntityTypeAddedConvention
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

                if (attribute.ChunkSkipColumns != null && attribute.ChunkSkipColumns.Length > 0)
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.ChunkSkipColumns, string.Join(",", attribute.ChunkSkipColumns));
                }

                bool hasChunkSkipping = attribute.ChunkSkipColumns != null && attribute.ChunkSkipColumns.Length > 0;
                if (hasChunkSkipping)
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.ChunkSkipColumns, string.Join(",", attribute.ChunkSkipColumns ?? []));
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                }

                bool compressionAnnotationValue = attribute.EnableCompression;
                if (hasChunkSkipping)
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, true);
                }
                else
                {
                    entityTypeBuilder.HasAnnotation(HypertableAnnotations.EnableCompression, compressionAnnotationValue);
                }
            }
        }
    }
}

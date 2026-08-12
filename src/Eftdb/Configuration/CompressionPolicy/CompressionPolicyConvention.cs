using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

using static CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ConventionValidationHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy
{
    /// <summary>
    /// A convention that configures the compression policy for a hypertable or continuous aggregate
    /// based on the presence of the <see cref="CompressionPolicyAttribute"/>.
    /// </summary>
    public class CompressionPolicyConvention : IEntityTypeAddedConvention
    {
        /// <summary>
        /// Called when an entity type is added to the model.
        /// </summary>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="context">Additional information available during convention execution.</param>
        public void ProcessEntityTypeAdded(
            IConventionEntityTypeBuilder entityTypeBuilder,
            IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            CompressionPolicyAttribute? attribute = entityType.ClrType?.GetCustomAttribute<CompressionPolicyAttribute>();

            if (attribute == null)
            {
                return;
            }

            bool hasAfter = !string.IsNullOrWhiteSpace(attribute.After);
            bool hasCreatedBefore = !string.IsNullOrWhiteSpace(attribute.CreatedBefore);

            ValidateExclusiveFields(
                entityType.ClrType?.Name,
                "[CompressionPolicy]",
                "After", hasAfter,
                "CreatedBefore", hasCreatedBefore);

            entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);

            if (hasAfter)
                entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.After, attribute.After!);

            if (hasCreatedBefore)
                entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.CreatedBefore, attribute.CreatedBefore!);

            if (!string.IsNullOrWhiteSpace(attribute.ScheduleInterval))
                entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.ScheduleInterval, attribute.ScheduleInterval);

            DateTime? parsedInitialStart = ParseInitialStart(attribute.InitialStart, entityType.ClrType?.Name, "[CompressionPolicy]");
            if (parsedInitialStart.HasValue)
            {
                entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.InitialStart, parsedInitialStart.Value);
            }

            if (!string.IsNullOrWhiteSpace(attribute.Timezone))
                entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.Timezone, attribute.Timezone);

            if (attribute.IfNotExists)
                entityTypeBuilder.HasAnnotation(CompressionPolicyAnnotations.IfNotExists, true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

using static CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ConventionValidationHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy
{
    /// <summary>
    /// A convention that configures the retention policy for a hypertable or continuous aggregate
    /// based on the presence of the [RetentionPolicy] attribute.
    /// </summary>
    internal class RetentionPolicyConvention : IEntityTypeAddedConvention
    {
        /// <summary>
        /// Called when an entity type is added to the model.
        /// </summary>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="context">Additional information available during convention execution.</param>
        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            RetentionPolicyAttribute? attribute = entityType.ClrType?.GetCustomAttribute<RetentionPolicyAttribute>();

            if (attribute != null)
            {
                bool hasDropAfter = !string.IsNullOrWhiteSpace(attribute.DropAfter);
                bool hasDropCreatedBefore = !string.IsNullOrWhiteSpace(attribute.DropCreatedBefore);

                ValidateExclusiveFields(
                    entityType.ClrType?.Name,
                    "[RetentionPolicy]",
                    "DropAfter", hasDropAfter,
                    "DropCreatedBefore", hasDropCreatedBefore);

                entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);

                if (hasDropAfter)
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.DropAfter, attribute.DropAfter!);

                if (hasDropCreatedBefore)
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.DropCreatedBefore, attribute.DropCreatedBefore!);

                DateTime? parsedInitialStart = ParseInitialStart(attribute.InitialStart, entityType.ClrType?.Name, "[RetentionPolicy]");
                if (parsedInitialStart.HasValue)
                {
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.InitialStart, parsedInitialStart.Value);
                }

                if (!string.IsNullOrWhiteSpace(attribute.ScheduleInterval))
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.ScheduleInterval, attribute.ScheduleInterval);

                if (!string.IsNullOrWhiteSpace(attribute.MaxRuntime))
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.MaxRuntime, attribute.MaxRuntime);

                if (attribute.MaxRetries > -1)
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.MaxRetries, attribute.MaxRetries);

                if (!string.IsNullOrWhiteSpace(attribute.RetryPeriod))
                    entityTypeBuilder.HasAnnotation(RetentionPolicyAnnotations.RetryPeriod, attribute.RetryPeriod);
            }
        }
    }
}

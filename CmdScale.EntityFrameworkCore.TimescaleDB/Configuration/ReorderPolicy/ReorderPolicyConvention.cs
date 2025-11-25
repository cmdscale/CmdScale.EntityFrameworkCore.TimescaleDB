using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    /// <summary>
    /// A convention that configures the reorder policy for a hypertable based on the presence of
    /// the [ReorderPolicy] attribute.
    /// </summary>
    public class ReorderPolicyConvention : IEntityTypeAddedConvention
    {
        /// <summary>
        /// Called when an entity type is added to the model.
        /// </summary>
        /// <param name="entityTypeBuilder">The builder for the entity type.</param>
        /// <param name="context">Additional information available during convention execution.</param>
        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            ReorderPolicyAttribute? attribute = entityType.ClrType?.GetCustomAttribute<ReorderPolicyAttribute>();

            if (attribute != null)
            {
                // Apply the annotations that the Fluent API would have applied.
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.IndexName, attribute.IndexName);

                if (!string.IsNullOrWhiteSpace(attribute.InitialStart))
                {
                    if (DateTime.TryParse(attribute.InitialStart, out DateTime parsedDateTimeOffset))
                    {
                        entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.InitialStart, parsedDateTimeOffset);
                    }
                    else
                    {
                        throw new InvalidOperationException($"InitialStart '{attribute.InitialStart}' is not a valid DateTime format. Please use a valid DateTime string.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(attribute.ScheduleInterval))
                    entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.ScheduleInterval, attribute.ScheduleInterval);

                if (!string.IsNullOrWhiteSpace(attribute.MaxRuntime))
                    entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.MaxRuntime, attribute.MaxRuntime);

                if (attribute.MaxRetries > -1)
                    entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.MaxRetries, attribute.MaxRetries);

                if (!string.IsNullOrWhiteSpace(attribute.RetryPeriod))
                    entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.RetryPeriod, attribute.RetryPeriod);
            }
        }
    }
}

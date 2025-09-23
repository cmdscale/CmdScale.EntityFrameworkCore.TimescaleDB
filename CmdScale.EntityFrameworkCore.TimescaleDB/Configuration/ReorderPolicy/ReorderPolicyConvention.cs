using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    public class ReorderPolicyConvention : IEntityTypeAddedConvention
    {
        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            ReorderPolicyAttribute? attribute = entityType.ClrType?.GetCustomAttribute<ReorderPolicyAttribute>();

            if (attribute != null)
            {
                // Apply the annotations that the Fluent API would have applied.
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.IndexName, attribute.IndexName);
            }
        }
    }
}

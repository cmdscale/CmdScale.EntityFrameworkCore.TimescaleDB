using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    public static class ReorderPolicyTypeBuilder
    {
        public static EntityTypeBuilder<TEntity> WithReorderPolicy<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string indexName,
            DateTime? initialStart =null) where TEntity : class
        {
            entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
            entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.IndexName, indexName);

            if (initialStart.HasValue)
                entityTypeBuilder.HasAnnotation(ReorderPolicyAnnotations.InitialStart, initialStart);
            
            return entityTypeBuilder;
        }
    }
}

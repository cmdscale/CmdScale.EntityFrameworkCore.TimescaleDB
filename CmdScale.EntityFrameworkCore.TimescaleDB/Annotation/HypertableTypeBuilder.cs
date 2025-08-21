using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Annotation
{
    public static class HypertableTypeBuilder
    {
        public static EntityTypeBuilder<TEntity> IsHypertable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            Expression<Func<TEntity, object>> timePropertyExpression) where TEntity : class
        {
            string propertyName = GetPropertyName(timePropertyExpression);

            entityTypeBuilder.HasAnnotation(HypertableAnnotations.IsHypertable, true);
            entityTypeBuilder.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, propertyName);

            return entityTypeBuilder;
        }

        // Helper method to extract the property name from the lambda expression
        private static string GetPropertyName<TEntity>(Expression<Func<TEntity, object>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            // This handles cases where the property is a value type (e.g., DateTime)
            if (propertyExpression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression unaryMemberExpression)
            {
                return unaryMemberExpression.Member.Name;
            }

            throw new ArgumentException("Expression is not a valid property expression.", nameof(propertyExpression));
        }
    }
}

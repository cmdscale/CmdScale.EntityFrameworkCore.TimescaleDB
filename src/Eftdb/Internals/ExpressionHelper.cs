using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Shared helper for extracting CLR property names from selector lambda expressions.
    /// </summary>
    internal static class ExpressionHelper
    {
        /// <summary>
        /// Extracts the property name from a simple property access expression,
        /// unwrapping boxing conversions produced by <c>object</c>-typed selectors.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the expression is not a simple property access.</exception>
        internal static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            if (propertyExpression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression unaryMemberExpression)
            {
                return unaryMemberExpression.Member.Name;
            }

            throw new ArgumentException("Expression must be a simple property access expression.", nameof(propertyExpression));
        }
    }
}

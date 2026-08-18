using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Shared helper for extracting CLR property names from selector lambda expressions.
    /// </summary>
    internal static class ExpressionHelper
    {
        /// <summary>
        /// Extracts the property name from a property access expression, unwrapping boxing
        /// conversions produced by <c>object</c>-typed selectors. A chained access through
        /// complex-type members (e.g. <c>x => x.Param1.Value</c>) yields a dot-separated
        /// path (<c>"Param1.Value"</c>) that <see cref="ColumnNameResolver"/> traverses.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the expression is not a property access rooted in the lambda parameter.</exception>
        internal static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            Expression? body = propertyExpression.Body;
            if (body is UnaryExpression unaryExpression)
            {
                body = unaryExpression.Operand;
            }

            List<string> segments = [];
            while (body is MemberExpression memberExpression)
            {
                segments.Add(memberExpression.Member.Name);
                body = memberExpression.Expression;
            }

            if (segments.Count == 0 || body is not ParameterExpression)
            {
                throw new ArgumentException("Expression must be a simple property access expression.", nameof(propertyExpression));
            }

            segments.Reverse();
            return string.Join('.', segments);
        }
    }
}

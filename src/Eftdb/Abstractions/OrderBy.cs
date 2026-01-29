using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    /// <summary>
    /// Represents an ordering specification for a column.
    /// </summary>
    /// <param name="columnName">The name of the column to order by.</param>
    /// <param name="isAscending">
    /// If true, orders Ascending (ASC). 
    /// If false, orders Descending (DESC). 
    /// If null, uses database default (ASC).
    /// </param>
    /// <param name="nullsFirst">
    /// If true, forces NULLS FIRST. 
    /// If false, forces NULLS LAST.
    /// If null, uses database default (NULLS LAST for ASC, NULLS FIRST for DESC).
    /// </param>
    public class OrderBy(string columnName, bool? isAscending = null, bool? nullsFirst = null)
    {
        public string ColumnName { get; } = columnName;
        public bool? IsAscending { get; } = isAscending;
        public bool? NullsFirst { get; } = nullsFirst;

        public string ToSql()
        {
            var sb = new System.Text.StringBuilder(ColumnName);

            // Only append direction if explicitly set
            if (IsAscending.HasValue)
            {
                sb.Append(IsAscending.Value ? " ASC" : " DESC");
            }

            // Only append NULLS clause if explicitly set
            if (NullsFirst.HasValue)
            {
                sb.Append(NullsFirst.Value ? " NULLS FIRST" : " NULLS LAST");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Fluent builder for creating OrderBy instances.
    /// </summary>
    public static class OrderByBuilder
    {
        public static OrderByConfiguration<TEntity> For<TEntity>(Expression<Func<TEntity, object>> expression) => new(expression);
    }

    /// <summary>
    /// Fluent configuration for creating OrderBy instances.
    /// </summary>
    public class OrderByConfiguration<TEntity>(Expression<Func<TEntity, object>> expression)
    {
        private readonly string _propertyName = GetPropertyName(expression);

        public OrderBy Default(bool? nullsFirst = null) => new(_propertyName, null, nullsFirst);
        public OrderBy Ascending(bool? nullsFirst = null) => new(_propertyName, true, nullsFirst);
        public OrderBy Descending(bool? nullsFirst = null) => new(_propertyName, false, nullsFirst);

        // Helper to extract the string name from the expression
        private static string GetPropertyName(Expression<Func<TEntity, object>> expression)
        {
            if (expression.Body is MemberExpression member) return member.Member.Name;
            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression m) return m.Member.Name;
            throw new ArgumentException("Invalid expression. Please use a simple property access expression.");
        }
    }

    /// <summary>
    /// Fluent builder for creating OrderBy instances using lambda expressions.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class OrderBySelector<TEntity>
    {
        public OrderBy By(Expression<Func<TEntity, object>> expression, bool? nullsFirst = null)
            => new(GetPropertyName(expression), null, nullsFirst);

        public OrderBy ByAscending(Expression<Func<TEntity, object>> expression, bool? nullsFirst = null)
            => new(GetPropertyName(expression), true, nullsFirst);

        public OrderBy ByDescending(Expression<Func<TEntity, object>> expression, bool? nullsFirst = null)
            => new(GetPropertyName(expression), false, nullsFirst);

        // Internal helper to get property names from expressions
        private static string GetPropertyName(Expression<Func<TEntity, object>> expression)
        {
            if (expression.Body is MemberExpression m) return m.Member.Name;
            if (expression.Body is UnaryExpression u && u.Operand is MemberExpression m2) return m2.Member.Name;
            throw new ArgumentException("Expression must be a property access.");
        }
    }

    /// <summary>
    /// Extension methods for creating OrderBy instances.
    /// </summary>
    public static class OrderByExtensions
    {
        /// <summary>
        /// Creates an ascending OrderBy instance.
        /// </summary>
        /// <param name="columnName">The name of the column to order by.</param>
        /// <param name="nullsFirst">Whether nulls should appear first.</param>
        public static OrderBy Ascending(this string columnName, bool nullsFirst = false)
        {
            return new OrderBy(columnName, true, nullsFirst);
        }

        /// <summary>
        /// Creates a descending OrderBy instance.
        /// </summary>
        /// <param name="columnName">The name of the column to order by.</param>
        /// <param name="nullsFirst">Whether nulls should appear first.</param>
        public static OrderBy Descending(this string columnName, bool nullsFirst = false)
        {
            return new OrderBy(columnName, false, nullsFirst);
        }
    }
}
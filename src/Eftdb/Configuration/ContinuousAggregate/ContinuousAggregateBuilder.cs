using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Provides a fluent API for configuring a TimescaleDB continuous aggregate.
    /// This builder is aware of both the aggregate entity type and the source hypertable entity type.
    /// </summary>
    /// <typeparam name="TEntity">The class representing the continuous aggregate view.</typeparam>
    /// <typeparam name="TSourceEntity">The class representing the source hypertable.</typeparam>
    public class ContinuousAggregateBuilder<TEntity, TSourceEntity>
        where TEntity : class
        where TSourceEntity : class
    {
        public EntityTypeBuilder<TEntity> EntityTypeBuilder { get; }

        internal ContinuousAggregateBuilder(EntityTypeBuilder<TEntity> entityTypeBuilder)
        {
            EntityTypeBuilder = entityTypeBuilder;
        }

        /// <summary>
        /// Configures whether to create the continuous aggregate with no data initially.
        /// </summary>
        /// <param name="withNoData">True to create with no data; false to populate immediately.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> WithNoData(bool withNoData = true)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WithNoData, withNoData);
            return this;
        }

        /// <summary>
        /// Configures whether to automatically create indexes on group by columns.
        /// </summary>
        /// <param name="createGroupIndexes">True to create indexes; false otherwise.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> CreateGroupIndexes(bool createGroupIndexes = true)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes, createGroupIndexes);
            return this;
        }

        /// <summary>
        /// Configures whether the continuous aggregate returns only materialized data.
        /// </summary>
        /// <param name="materializedOnly">True to return only materialized data; false to include real-time data.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> MaterializedOnly(bool materializedOnly = true)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedOnly, materializedOnly);
            return this;
        }

        /// <summary>
        /// Adds an aggregate function mapping between a property on the continuous aggregate and a source column.
        /// </summary>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <param name="propertyExpression">Expression selecting the property on the continuous aggregate.</param>
        /// <param name="sourceColumn">Expression selecting the source column from the hypertable.</param>
        /// <param name="function">The aggregate function to apply.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> AddAggregateFunction<TProperty>(
            Expression<Func<TEntity, TProperty>> propertyExpression,
            Expression<Func<TSourceEntity, TProperty>> sourceColumn,
            EAggregateFunction function)
        {
            string propertyName = GetPropertyName(propertyExpression);
            IAnnotation? annotation = EntityTypeBuilder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions);
            List<string> aggregateFunctions = annotation?.Value as List<string> ?? [];

            if (aggregateFunctions.Any(x => x.StartsWith(propertyName + ":")))
            {
                return this;
            }

            string sourceColumnName = GetPropertyName(sourceColumn);

            aggregateFunctions.Add($"{propertyName}:{function}:{sourceColumnName}");
            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
            return this;
        }

        /// <summary>
        /// Adds a group by column from the source hypertable.
        /// </summary>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <param name="propertyExpression">Expression selecting the property to group by.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> AddGroupByColumn<TProperty>(
            Expression<Func<TSourceEntity, TProperty>> propertyExpression)
        {
            string propertyName = GetPropertyName(propertyExpression);
            IAnnotation? annotation = EntityTypeBuilder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
            List<string> groupByColumns = annotation?.Value as List<string> ?? [];

            if (groupByColumns.Contains(propertyName))
            {
                return this;
            }

            groupByColumns.Add(propertyName);

            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.GroupByColumns, groupByColumns);
            return this;
        }

        /// <summary>
        /// Adds a group by expression using a raw SQL expression string.
        /// </summary>
        /// <param name="groupByExpression">The SQL expression to group by.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> AddGroupByColumn(string groupByExpression)
        {
            IAnnotation? annotation = EntityTypeBuilder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
            List<string> groupByColumns = annotation?.Value as List<string> ?? [];

            if (groupByColumns.Contains(groupByExpression))
            {
                return this;
            }

            groupByColumns.Add(groupByExpression);

            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.GroupByColumns, groupByColumns);
            return this;
        }

        /// <summary>
        /// Adds a WHERE clause to filter data in the continuous aggregate.
        /// </summary>
        /// <param name="whereClause">The SQL WHERE clause expression.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> Where(string whereClause)
        {
            EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WhereClause, whereClause);
            return this;
        }

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

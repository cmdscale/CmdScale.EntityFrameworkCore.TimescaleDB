using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Provides a fluent API for configuring a TimescaleDB continuous aggregate.
    /// This builder is aware of both the aggregate entity type and the source hypertable entity type.
    /// Annotation writing is delegated to <see cref="ContinuousAggregateBuilderCore"/>.
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
            ContinuousAggregateBuilderCore.WithNoData(EntityTypeBuilder, withNoData);
            return this;
        }

        /// <summary>
        /// Configures whether to automatically create indexes on group by columns.
        /// </summary>
        /// <param name="createGroupIndexes">True to create indexes; false otherwise.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> CreateGroupIndexes(bool createGroupIndexes = true)
        {
            ContinuousAggregateBuilderCore.CreateGroupIndexes(EntityTypeBuilder, createGroupIndexes);
            return this;
        }

        /// <summary>
        /// Configures whether the continuous aggregate returns only materialized data.
        /// </summary>
        /// <param name="materializedOnly">True to return only materialized data; false to include real-time data.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> MaterializedOnly(bool materializedOnly = true)
        {
            ContinuousAggregateBuilderCore.MaterializedOnly(EntityTypeBuilder, materializedOnly);
            return this;
        }

        /// <summary>
        /// Adds an aggregate function mapping between a property on the continuous aggregate and a source column.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property on the continuous aggregate.</typeparam>
        /// <typeparam name="TSourceProperty">The type of the source column on the hypertable.</typeparam>
        /// <param name="propertyExpression">Expression selecting the property on the continuous aggregate.</param>
        /// <param name="sourceColumn">Expression selecting the source column from the hypertable.</param>
        /// <param name="function">The aggregate function to apply.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> AddAggregateFunction<TProperty, TSourceProperty>(
            Expression<Func<TEntity, TProperty>> propertyExpression,
            Expression<Func<TSourceEntity, TSourceProperty>> sourceColumn,
            EAggregateFunction function)
        {
            string propertyName = GetPropertyName(propertyExpression);
            string sourceColumnName = GetPropertyName(sourceColumn);
            ContinuousAggregateBuilderCore.AddAggregateFunction(EntityTypeBuilder, propertyName, sourceColumnName, function);
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
            ContinuousAggregateBuilderCore.AddGroupByColumn(EntityTypeBuilder, GetPropertyName(propertyExpression));
            return this;
        }

        /// <summary>
        /// Adds a group by expression using a raw SQL expression string.
        /// </summary>
        /// <param name="groupByExpression">The SQL expression to group by.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> AddGroupByColumn(string groupByExpression)
        {
            ContinuousAggregateBuilderCore.AddGroupByColumn(EntityTypeBuilder, groupByExpression);
            return this;
        }

        /// <summary>
        /// Adds a WHERE clause to filter data in the continuous aggregate.
        /// </summary>
        /// <param name="whereClause">The SQL WHERE clause expression.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> Where(string whereClause)
        {
            ContinuousAggregateBuilderCore.Where(EntityTypeBuilder, whereClause);
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

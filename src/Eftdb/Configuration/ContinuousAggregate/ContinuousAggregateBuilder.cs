using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
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
            string propertyName = ExpressionHelper.GetPropertyName(propertyExpression);
            string sourceColumnName = ExpressionHelper.GetPropertyName(sourceColumn);
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
            ContinuousAggregateBuilderCore.AddGroupByColumn(EntityTypeBuilder, ExpressionHelper.GetPropertyName(propertyExpression));
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

        /// <summary>
        /// Enables or disables columnstore (compression) on the continuous aggregate.
        /// Corresponds to <c>ALTER MATERIALIZED VIEW ... SET (timescaledb.compress = true)</c>.
        /// Enabling compression is a prerequisite for adding a compression policy to a continuous aggregate.
        /// </summary>
        /// <param name="enable">Whether to enable compression. Defaults to <see langword="true"/>.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> WithCompression(bool enable = true)
        {
            ContinuousAggregateBuilderCore.EnableCompression(EntityTypeBuilder, enable);
            return this;
        }

        /// <summary>
        /// Specifies the columns to segment by when compressing the continuous aggregate.
        /// Corresponds to <c>timescaledb.compress_segmentby</c>. Implicitly enables compression.
        /// </summary>
        /// <param name="segmentByColumns">Lambda expressions selecting the properties to segment by.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> WithCompressionSegmentBy(
            params Expression<Func<TEntity, object>>[] segmentByColumns)
        {
            string[] columnNames = [.. segmentByColumns.Select(ExpressionHelper.GetPropertyName<TEntity, object>)];
            ContinuousAggregateBuilderCore.WithCompressionSegmentBy(EntityTypeBuilder, string.Join(", ", columnNames));
            return this;
        }

        /// <summary>
        /// Specifies the columns to order by within each compressed segment using explicit
        /// <see cref="OrderBy"/> definitions. Corresponds to <c>timescaledb.compress_orderby</c>.
        /// Implicitly enables compression.
        /// </summary>
        /// <param name="orderByRules"><see cref="OrderBy"/> instances describing the ordering.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> WithCompressionOrderBy(
            params OrderBy[] orderByRules)
        {
            ContinuousAggregateBuilderCore.WithCompressionOrderBy(EntityTypeBuilder, string.Join(", ", orderByRules.Select(r => r.ToSql())));
            return this;
        }

        /// <summary>
        /// Specifies the columns to order by within each compressed segment using an
        /// <see cref="OrderBySelector{TEntity}"/> factory. Corresponds to <c>timescaledb.compress_orderby</c>.
        /// Implicitly enables compression.
        /// </summary>
        /// <param name="orderSelector">A function that receives an <see cref="OrderBySelector{TEntity}"/> and returns the ordering rules.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> WithCompressionOrderBy(
            Func<OrderBySelector<TEntity>, IEnumerable<OrderBy>> orderSelector)
        {
            OrderBySelector<TEntity> selector = new();
            return WithCompressionOrderBy([.. orderSelector(selector)]);
        }

        /// <summary>
        /// Specifies the columns to order by within each compressed segment, one selector per column.
        /// Corresponds to <c>timescaledb.compress_orderby</c>. Implicitly enables compression.
        /// </summary>
        /// <param name="orderSelectors">Per-column selector functions.</param>
        /// <returns>The builder for method chaining.</returns>
        public ContinuousAggregateBuilder<TEntity, TSourceEntity> WithCompressionOrderBy(
            params Func<OrderBySelector<TEntity>, OrderBy>[] orderSelectors)
        {
            OrderBySelector<TEntity> selector = new();
            return WithCompressionOrderBy([.. orderSelectors.Select(s => s(selector))]);
        }

    }
}

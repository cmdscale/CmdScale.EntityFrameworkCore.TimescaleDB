using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    public static class ContinuousAggregateTypeBuilder
    {
        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> IsContinuousAggregate<TEntity, TSourceEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string materualizedViewName,
            string timeBucketWidth,
            Expression<Func<TSourceEntity, DateTime>> propertyExpression,
            bool timeBucketGroupBy = true,
            string? chukInterval = null)
            where TEntity : class
            where TSourceEntity : class
        {
            // Configure the entity to map to a view instead of a table
            // This prevents EF Core from trying to create a table for the continuous aggregate
            entityTypeBuilder.ToView(materualizedViewName);

            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, materualizedViewName);

            string parentName = typeof(TSourceEntity).Name;
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ParentName, parentName);

            string timeBucketSourceColumn = GetPropertyName(propertyExpression);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, timeBucketSourceColumn);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, timeBucketWidth);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy, timeBucketGroupBy);

            if (!string.IsNullOrEmpty(chukInterval))
            {
                entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ChunkInterval, chukInterval);
            }

            return new ContinuousAggregateBuilder<TEntity, TSourceEntity>(entityTypeBuilder);
        }

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> WithNoData<TEntity, TSourceEntity>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            bool withNoData = true)
            where TEntity : class
            where TSourceEntity : class
        {

            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WithNoData, withNoData);
            return builder;
        }

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> CreateGroupIndexes<TEntity, TSourceEntity>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            bool createGroupIndexes = true)
            where TEntity : class
            where TSourceEntity : class
        {
            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes, createGroupIndexes);
            return builder;
        }

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> MaterializedOnly<TEntity, TSourceEntity>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            bool materializedOnly = true)
            where TEntity : class
            where TSourceEntity : class
        {
            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedOnly, materializedOnly);
            return builder;
        }

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> AddAggregateFunction<TEntity, TSourceEntity, TProperty>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            Expression<Func<TEntity, TProperty>> propertyExpression,
            Expression<Func<TSourceEntity, TProperty>> sourceColumn,
            EAggregateFunction function
            )
            where TEntity : class
            where TSourceEntity : class
        {
            string propertyName = GetPropertyName(propertyExpression);
            IAnnotation? annotation = builder.EntityTypeBuilder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.AggregateFunctions);
            List<string> aggregateFunctions = annotation?.Value as List<string> ?? [];

            if (aggregateFunctions.Any(x => x.StartsWith(propertyName + ":")))
            {
                return builder;
            }

            string sourceColumnName = GetPropertyName(sourceColumn);

            aggregateFunctions.Add($"{propertyName}:{function}:{sourceColumnName}");
            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
            return builder;
        }

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> AddGroupByColumn<TEntity, TSourceEntity, TProperty>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            Expression<Func<TSourceEntity, TProperty>> propertyExpression) 
            where TEntity : class 
            where TSourceEntity : class
        {
            string propertyName = GetPropertyName(propertyExpression);
            IAnnotation? annotation = builder.EntityTypeBuilder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
            List<string> groupByColumns = annotation?.Value as List<string> ?? [];

            if (groupByColumns.Contains(propertyName))
            {
                return builder;
            }

            groupByColumns.Add(propertyName);

            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.GroupByColumns, groupByColumns);
            return builder;
        }

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> AddGroupByColumn<TEntity, TSourceEntity>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
            string groupByExpression)
            where TEntity : class
            where TSourceEntity : class
        {
            IAnnotation? annotation = builder.EntityTypeBuilder.Metadata.FindAnnotation(ContinuousAggregateAnnotations.GroupByColumns);
            List<string> groupByColumns = annotation?.Value as List<string> ?? [];

            if (groupByColumns.Contains(groupByExpression))
            {
                return builder;
            }

            groupByColumns.Add(groupByExpression);

            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.GroupByColumns, groupByColumns);
            return builder;
        }

        // TODO: Remove or implement expression parsing
        //public static ContinuousAggregateBuilder<TEntity, TSourceEntity> Where<TEntity, TSourceEntity>(
        //    this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
        //     Expression<Func<TSourceEntity, bool>> predicate)
        //     where TEntity : class
        //     where TSourceEntity : class
        //{
        //    builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WhereClause, predicate);
        //    return builder;
        //}

        public static ContinuousAggregateBuilder<TEntity, TSourceEntity> Where<TEntity, TSourceEntity>(
            this ContinuousAggregateBuilder<TEntity, TSourceEntity> builder,
             string whereClause)
             where TEntity : class
             where TSourceEntity : class
        {
            builder.EntityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WhereClause, whereClause);
            return builder;
        }

        private static string GetPropertyName<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
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

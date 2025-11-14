using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Reads the [ContinuousAggregate], [TimeBucket], and [Aggregate] attributes
    /// to configure an entity as a TimescaleDB continuous aggregate.
    /// </summary>
    public class ContinuousAggregateConvention : IEntityTypeAddedConvention
    {
        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            IConventionEntityType entityType = entityTypeBuilder.Metadata;
            ContinuousAggregateAttribute? continuousAggregateAttribute = entityType.ClrType?.GetCustomAttribute<ContinuousAggregateAttribute>();

            if (continuousAggregateAttribute == null) return;

            // Configure the entity to map to a view instead of a table
            // This prevents EF Core from trying to create a table for the continuous aggregate
            entityTypeBuilder.ToView(continuousAggregateAttribute.MaterializedViewName);

            // Apply class-level configurations from [ContinuousAggregateAttribute]
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, continuousAggregateAttribute.MaterializedViewName);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ParentName, continuousAggregateAttribute.ParentName);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.ChunkInterval, continuousAggregateAttribute.ChunkInterval);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WithNoData, continuousAggregateAttribute.WithNoData);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.CreateGroupIndexes, continuousAggregateAttribute.CreateGroupIndexes);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.MaterializedOnly, continuousAggregateAttribute.MaterializedOnly);
            entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.WhereClause, continuousAggregateAttribute.Where);

            // Discover class-level TimeBucket configuration from [TimeBucketAttribute]
            TimeBucketAttribute? timeBucketAttr = entityType.ClrType?.GetCustomAttribute<TimeBucketAttribute>();
            if (timeBucketAttr != null)
            {
                entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketWidth, timeBucketAttr.BucketWidth);
                entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketSourceColumn, timeBucketAttr.SourceColumn);
                entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.TimeBucketGroupBy, timeBucketAttr.GroupBy);
            }

            // Discover property-level configurations
            List<string> aggregateFunctions = [];

            foreach (IConventionProperty property in entityType.GetProperties())
            {
                PropertyInfo? propertyInfo = property.PropertyInfo;
                if (propertyInfo == null) continue;

                // Discover aggregate columns from [AggregateAttribute]
                AggregateAttribute? aggregateAttr = propertyInfo.GetCustomAttribute<AggregateAttribute>();
                if (aggregateAttr != null)
                {
                    // Serialize the aggregate info into a string format for the annotation.
                    // Format: "DestinationPropertyName:AggregateFunction:SourceColumnName"
                    // Example: "AvgTemperature:Avg:temperature"
                    string sourceColumn = aggregateAttr.SourceColumn ?? property.Name;
                    aggregateFunctions.Add($"{property.Name}:{aggregateAttr.Function}:{sourceColumn}");
                }
            }

            // Apply the discovered property-level annotations
            if (aggregateFunctions.Count != 0)
            {
                entityTypeBuilder.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, aggregateFunctions);
            }
        }
    }
}

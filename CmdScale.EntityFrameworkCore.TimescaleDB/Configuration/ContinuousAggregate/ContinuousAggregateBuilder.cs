using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    }
}

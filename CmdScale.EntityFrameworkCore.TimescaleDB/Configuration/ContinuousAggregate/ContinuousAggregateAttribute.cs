using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using System.Linq.Expressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Defines a TimescaleDB continuous aggregate on an EF Core entity.
    /// This attribute provides all the necessary metadata to construct the
    /// CREATE MATERIALIZED VIEW statement for a continuous aggregate.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ContinuousAggregateAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the materialized view that will be created in the database.
        /// This corresponds to the <view_name> in the CREATE MATERIALIZED VIEW statement.
        /// </summary>
        public string MaterializedViewName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the source hypertable or another continuous aggregate
        /// on which this continuous aggregate is based.
        /// </summary>
        public string ParentName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the chunk interval for the continuous aggregate's underlying materialized hypertable.
        /// If not set, it defaults to 10 times the chunk_time_interval of the parent hypertable.
        /// Corresponds to the 'timescaledb.chunk_interval' option.
        /// </summary>
        public string? ChunkInterval { get; set; }

        /// <summary>
        /// By default, when you create a view for the first time, it is populated with data. This is so that the aggregates can be computed across the entire hypertable. 
        /// If you don't want this to happen, for example if the table is very large, or if new data is being continuously added, you can control the order in which the data is refreshed. 
        /// You can do this by adding a manual refresh with your continuous aggregate policy using the WITH NO DATA option.
        /// </summary>
        public bool WithNoData { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to automatically create indexes on the GROUP BY columns.
        /// Defaults to true. Corresponds to the 'timescaledb.create_group_indexes' option.
        /// </summary>
        public bool CreateGroupIndexes { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether queries to the view should only return materialized data.
        /// If false (the default), recent data from the source hypertable that has not yet been materialized will be included in query results.
        /// Corresponds to the 'timescaledb.materialized_only' option.
        /// </summary>
        public bool MaterializedOnly { get; set; } = false;

        /// <summary>
        /// Gets or sets the time interval for the time_bucket function (e.g., "1 day", "15 minutes").
        /// This is a required parameter for defining the aggregation window.
        /// </summary>
        public string TimeBucketWidth { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the time column in the source hypertable to be used by the time_bucket function.
        /// </summary>
        public string TimeBucketSourceColumn { get; set; } = string.Empty;


        // TOOD: This will only be available for the FluentAPI because attributes do not support complex types like expressions.
        /// <summary>
        /// Gets or sets an optional SQL WHERE clause to filter rows from the source hypertable before aggregation.
        /// The clause should be a valid SQL string without the "WHERE" keyword itself (e.g., "device_id = 'sensor-1'").
        /// </summary>
        //public Expression<Func<TSourceEntity, bool>> WhereClause { get; set; } = string.Empty;
    }
}

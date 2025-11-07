using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Defines an aggregate function for a property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AggregateAttribute(EAggregateFunction function, string sourceColumn = "*") : Attribute
    {
        /// <summary>
        /// The aggregate function to apply.
        /// </summary>
        public EAggregateFunction Function { get; } = function;

        /// <summary>
        /// The name of the column in the source hypertable to aggregate.
        /// For COUNT(*), this can be null or "*".
        /// </summary>
        public string SourceColumn { get; } = sourceColumn;
    }
}

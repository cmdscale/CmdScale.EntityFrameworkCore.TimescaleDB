namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    /// <summary>
    /// Maps a column on a continuous aggregate to an aggregate function applied to a source
    /// hypertable column — e.g. <c>average_temperature = AVG(temperature)</c>.
    /// Used as the strongly typed vocabulary for the <c>aggregateFunctions</c> argument.
    /// </summary>
    public sealed class ContinuousAggregateFunction(string alias, EAggregateFunction function, string sourceColumn)
    {
        /// <summary>The name of the resulting column on the continuous aggregate.</summary>
        public string Alias { get; } = alias;

        /// <summary>The aggregate function to apply.</summary>
        public EAggregateFunction Function { get; } = function;

        /// <summary>The source hypertable column to aggregate.</summary>
        public string SourceColumn { get; } = sourceColumn;

        /// <summary>
        /// Serializes to the <c>alias:Function:sourceColumn</c> wire format stored on
        /// <c>CreateContinuousAggregateOperation.AggregateFunctions</c>.
        /// </summary>
        public string ToAnnotationValue() => $"{Alias}:{Function}:{SourceColumn}";
    }
}

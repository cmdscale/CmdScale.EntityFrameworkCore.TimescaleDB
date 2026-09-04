namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Define the time bucket column for a continuous aggregate.
    /// </summary>
    /// <remarks>
    /// Placed on the class, this attribute only configures the bucket width, source column, and GROUP BY behavior.
    /// Placed on a property, it additionally designates that property as the bucket column target, so the view's
    /// bucket alias derives from the property's mapped column name instead of the hard-coded <c>time_bucket</c>.
    /// A property-level attribute takes precedence over a class-level one.
    /// </remarks>
    /// <param name="bucketWidth">The time interval for the bucket (e.g., "1 hour", "15 minutes").</param>
    /// <param name="sourceColumn">The name of the time column in the source hypertable.</param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class TimeBucketAttribute(string bucketWidth, string sourceColumn) : Attribute
    {
        /// <summary>
        /// The time interval for the bucket (e.g., "1 hour", "15 minutes").
        /// </summary>
        public string BucketWidth { get; } = bucketWidth;

        /// <summary>
        /// The name of the time column in the source hypertable.
        /// </summary>
        public string SourceColumn { get; } = sourceColumn;

        /// <summary>
        /// Whether the time bucket column should be included in the GROUP BY clause.
        /// </summary>
        public bool GroupBy { get; set; } = true;
    }
}

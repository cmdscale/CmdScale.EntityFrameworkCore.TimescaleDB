namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Define the time bucket column for a continuous aggregate.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TimeBucketAttribute"/> class.
    /// </remarks>
    /// <param name="bucketWidth">The time interval for the bucket (e.g., "1 hour", "15 minutes").</param>
    /// <param name="sourceColumn">The name of the time column in the source hypertable.</param>
    [AttributeUsage(AttributeTargets.Class)]
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
        /// Weither the time bucket column should be included in the GROUP BY clause.
        /// </summary>
        public bool GroupBy { get; set; } = true;
    }
}

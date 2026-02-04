namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HypertableAttribute : Attribute
    {
        /// <summary>
        /// The name of the column that contains the time-series data.
        /// This is typically a DateTime, DateTimeOffset, or similar type.
        /// </summary>
        public string TimeColumnName { get; } = string.Empty;

        /// <summary>
        /// Specifies whether compression is enabled on the hypertable.
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// Specifies the columns to group by when compressing the hypertable.
        /// Maps to <c>timescaledb.compress_segmentby</c>.
        /// </summary>
        /// <example>
        /// <code>[Hypertable("time", CompressionSegmentBy = ["device_id", "tenant_id"])]</code>
        /// </example>
        public string[]? CompressionSegmentBy { get; set; } = null;

        /// <summary>
        /// Specifies the columns to order by within each compressed segment.
        /// Maps to <c>timescaledb.compress_orderby</c>.
        /// Since attributes cannot use Expressions, you must specify the full SQL syntax if direction is needed.
        /// </summary>
        /// <example>
        /// <code>[Hypertable("time", CompressionOrderBy = ["time DESC", "value ASC NULLS LAST"])]</code>
        /// </example>
        public string[]? CompressionOrderBy { get; set; } = null;

        /// <summary>
        /// Specifies whether existing data should be migrated when converting a table to a hypertable.
        /// </summary>
        public bool MigrateData { get; set; } = false;

        /// <summary>
        /// Defines the duration of time covered by each chunk in a hypertable.
        /// </summary>
        public string ChunkTimeInterval { get; set; } = DefaultValues.ChunkTimeInterval;

        /// <summary>
        /// Enable range statistics for a specific column in a compressed hypertable. This tracks a range of values for that column per chunk. 
        /// Used for chunk skipping during query optimization and applies only to the chunks created after chunk skipping is enabled.
        /// </summary>
        public string[]? ChunkSkipColumns { get; set; } = null;

        public HypertableAttribute(string timeColumnName)
        {
            if (string.IsNullOrWhiteSpace(timeColumnName))
            {
                throw new ArgumentException("Time column name must be provided.", nameof(timeColumnName));
            }

            TimeColumnName = timeColumnName;
        }
    }
}

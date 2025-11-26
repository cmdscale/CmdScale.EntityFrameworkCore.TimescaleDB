using System.Text.Json.Serialization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    public class Dimension
    {
        public string ColumnName { get; set; } = string.Empty;
        public EDimensionType Type { get; set; } = EDimensionType.Range;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NumberOfPartitions { get; set; } = null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Interval { get; set; }

        public Dimension()
        {
            ColumnName = string.Empty;
        }

        // Private constructor for factory methods and deserialization
        private Dimension(string columnName, EDimensionType type)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Dimension column name must be provided.", nameof(columnName));
            }

            ColumnName = columnName;
            Type = type;
        }

        /// <summary>
        /// Creates a hash partitioning dimension.
        /// </summary>
        /// <param name="columnName">The name of the column to partition on.</param>
        /// <param name="numberOfPartitions">The number of hash partitions.</param>
        public static Dimension CreateHash(string columnName, int numberOfPartitions)
        {
            if (numberOfPartitions <= 0)
            {
                throw new ArgumentException("Number of partitions must be greater than zero.", nameof(numberOfPartitions));
            }

            return new Dimension(columnName, EDimensionType.Hash)
            {
                NumberOfPartitions = numberOfPartitions
            };
        }

        /// <summary>
        /// Creates a range partitioning dimension.
        /// </summary>
        /// <param name="columnName">The name of the column to partition on.</param>
        /// <param name="interval">The interval for each range partition (e.g., "1 day").</param>
        public static Dimension CreateRange(string columnName, string interval)
        {
            if (string.IsNullOrWhiteSpace(interval))
            {
                throw new ArgumentException("Interval must be provided for a range dimension.", nameof(interval));
            }

            return new Dimension(columnName, EDimensionType.Range)
            {
                Interval = interval
            };
        }
    }
}

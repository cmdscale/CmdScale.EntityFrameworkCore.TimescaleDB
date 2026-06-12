using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// Declares an additional partitioning dimension on a hypertable entity.
    /// Apply once per dimension. Corresponds to TimescaleDB's <c>add_dimension</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// [Hypertable(nameof(EventTimestamp))]
    /// [Dimension(nameof(OrderPlacedTimestamp), EDimensionType.Range, "1 month")]
    /// [Dimension(nameof(WarehouseId), EDimensionType.Hash, 4)]
    /// public class OrderStatusEvent { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class DimensionAttribute : Attribute
    {
        /// <summary>The column to partition on.</summary>
        public string ColumnName { get; }

        /// <summary>The partitioning strategy (range or hash).</summary>
        public EDimensionType Type { get; }

        /// <summary>The partitioning interval. Set for range dimensions; <c>null</c> for hash dimensions.</summary>
        public string? Interval { get; }

        /// <summary>The number of hash partitions. Set for hash dimensions; <c>0</c> for range dimensions.</summary>
        public int NumberOfPartitions { get; }

        /// <summary>
        /// Declares a range partitioning dimension.
        /// </summary>
        /// <param name="columnName">The column to partition on.</param>
        /// <param name="type">Must be <see cref="EDimensionType.Range"/>.</param>
        /// <param name="interval">The partitioning interval (e.g. <c>"1 month"</c> or an integer interval as a string).</param>
        public DimensionAttribute(string columnName, EDimensionType type, string interval)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Dimension column name must be provided.", nameof(columnName));
            }

            if (type != EDimensionType.Range)
            {
                throw new ArgumentException("This constructor declares a range dimension; the type must be Range.", nameof(type));
            }

            if (string.IsNullOrWhiteSpace(interval))
            {
                throw new ArgumentException("Interval must be provided for a range dimension.", nameof(interval));
            }

            ColumnName = columnName;
            Type = EDimensionType.Range;
            Interval = interval;
        }

        /// <summary>
        /// Declares a hash partitioning dimension.
        /// </summary>
        /// <param name="columnName">The column to partition on.</param>
        /// <param name="type">Must be <see cref="EDimensionType.Hash"/>.</param>
        /// <param name="numberOfPartitions">The number of hash partitions.</param>
        public DimensionAttribute(string columnName, EDimensionType type, int numberOfPartitions)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Dimension column name must be provided.", nameof(columnName));
            }

            if (type != EDimensionType.Hash)
            {
                throw new ArgumentException("This constructor declares a hash dimension; the type must be Hash.", nameof(type));
            }

            if (numberOfPartitions <= 0)
            {
                throw new ArgumentException("Number of partitions must be greater than zero.", nameof(numberOfPartitions));
            }

            ColumnName = columnName;
            Type = EDimensionType.Hash;
            NumberOfPartitions = numberOfPartitions;
        }
    }
}

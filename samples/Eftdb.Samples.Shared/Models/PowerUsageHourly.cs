namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// Level 1 of the hierarchical rollup: hourly power statistics materialized
    /// directly from the <see cref="PowerMeterReading"/> hypertable.
    /// </summary>
    public class PowerUsageHourly
    {
        /// <summary>
        /// Start of the hour-wide bucket this row summarizes.
        /// </summary>
        public DateTime HourStart { get; set; }

        /// <summary>
        /// Identifier of the meter these hourly statistics belong to.
        /// </summary>
        public string MeterId { get; set; } = string.Empty;

        /// <summary>
        /// Minimum instantaneous power observed during the hour.
        /// </summary>
        public double MinPowerKw { get; set; }

        /// <summary>
        /// Maximum instantaneous power observed during the hour.
        /// </summary>
        public double MaxPowerKw { get; set; }

        /// <summary>
        /// Sum of the sampled power values during the hour. Rolling this column up with
        /// <c>Sum</c> at the daily level is exact (a sum of sums).
        /// </summary>
        public double TotalPowerKw { get; set; }

        /// <summary>
        /// Number of raw readings that fell into the hour. Rolling this column up with
        /// <c>Sum</c> at the daily level yields the exact daily reading count.
        /// </summary>
        public long ReadingCount { get; set; }
    }
}

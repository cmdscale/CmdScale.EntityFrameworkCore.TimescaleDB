namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// Level 2 of the hierarchical rollup: daily power statistics materialized from the
    /// <see cref="PowerUsageHourly"/> continuous aggregate rather than from the raw
    /// hypertable.
    /// </summary>
    public class PowerUsageDaily
    {
        /// <summary>
        /// Start of the day-wide bucket this row summarizes.
        /// </summary>
        public DateTime DayStart { get; set; }

        /// <summary>
        /// Identifier of the meter these daily statistics belong to.
        /// </summary>
        public string MeterId { get; set; } = string.Empty;

        /// <summary>
        /// Minimum instantaneous power for the day (min of the hourly minima).
        /// </summary>
        public double MinPowerKw { get; set; }

        /// <summary>
        /// Maximum instantaneous power for the day (max of the hourly maxima).
        /// </summary>
        public double MaxPowerKw { get; set; }

        /// <summary>
        /// Total sampled power for the day (sum of the hourly sums).
        /// </summary>
        public double TotalPowerKw { get; set; }

        /// <summary>
        /// Number of raw readings for the day (sum of the hourly reading counts).
        /// </summary>
        public long ReadingCount { get; set; }
    }
}

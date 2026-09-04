namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class HourlySensorAggregate
    {
        /// <summary>
        /// Start of the hour-wide bucket this row summarizes.
        /// </summary>
        public DateTime TimeBucket { get; set; }

        public double AvgPrimaryValue { get; set; }
        public double MinPrimaryValue { get; set; }
        public double MaxPrimaryValue { get; set; }
        public double AvgSecondaryValue { get; set; }
        public long ReadingCount { get; set; }
    }
}

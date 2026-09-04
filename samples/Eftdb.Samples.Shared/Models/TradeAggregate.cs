namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class TradeAggregate
    {
        /// <summary>
        /// Start of the hour-wide bucket this row summarizes.
        /// </summary>
        public DateTime TimeBucket { get; set; }

        public decimal AveragePrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal MinPrice { get; set; }
        public decimal TotalVolume { get; set; }
        public long TradeCount { get; set; }
    }
}

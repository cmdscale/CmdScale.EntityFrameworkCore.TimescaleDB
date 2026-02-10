namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// Represents a single trade event executed by an algorithmic trading system.
    /// </summary>
    public class Trade
    {
        /// <summary>
        /// The precise UTC timestamp when the trade was executed.
        /// This is the primary time-series column.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The stock ticker symbol (e.g., "TSLA", "AAPL").
        /// This is a good candidate for a hash partition (space dimension).
        /// </summary>
        public string Ticker { get; set; } = string.Empty;

        /// <summary>
        /// The price at which the trade was executed.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// The number of shares traded.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// The exchange where the trade occurred (e.g., "NASDAQ", "NYSE").
        /// </summary>
        public string Exchange { get; set; } = string.Empty;
    }
}

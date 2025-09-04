using System.ComponentModel.DataAnnotations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    /// <summary>
    /// Represents a single trade event with a standard primary key.
    /// Used for benchmark comparison against keyless or composite-key entities.
    /// </summary>
    public class TradeWithId
    {
        /// <summary>
        /// The unique identifier for the trade record.
        /// This is the primary key.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// The precise UTC timestamp when the trade was executed.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The stock ticker symbol (e.g., "TSLA", "AAPL").
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
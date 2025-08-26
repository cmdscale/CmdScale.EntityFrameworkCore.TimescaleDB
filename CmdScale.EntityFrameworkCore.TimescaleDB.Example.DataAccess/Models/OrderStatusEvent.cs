namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    /// <summary>
    /// Represents a single event in the lifecycle of an e-commerce order.
    /// </summary>
    public class OrderStatusEvent
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public int WarehouseId { get; set; }
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// The timestamp when the order was originally placed by the customer.
        /// </summary>
        public DateTime OrderPlacedTimestamp { get; set; }

        /// <summary>
        /// The timestamp when this specific status change event occurred.
        /// </summary>
        public DateTime EventTimestamp { get; set; }
    }
}

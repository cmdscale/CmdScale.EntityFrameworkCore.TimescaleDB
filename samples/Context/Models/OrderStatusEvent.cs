namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    public class OrderStatusEvent
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public int WarehouseId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderPlacedTimestamp { get; set; }

        public DateTime EventTimestamp { get; set; }
    }
}

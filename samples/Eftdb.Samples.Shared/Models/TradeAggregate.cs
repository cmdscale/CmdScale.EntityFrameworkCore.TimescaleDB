namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class TradeAggregate
    {
        public decimal AveragePrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal MinPrice { get; set; }
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    public class TradeAggregate
    {
        public decimal AveragePrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal MinPrice { get; set; }
    }
}

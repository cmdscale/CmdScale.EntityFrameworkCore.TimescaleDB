namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class HourlyStationAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgLatitude { get; set; }
        public double AvgTemperature { get; set; }
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class ApiRequestAggregate
    {
        public DateTime TimeBucket { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public double AverageDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
        public double MinDurationMs { get; set; }
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class ApiRequestLog
    {
        public DateTime Time { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public double DurationMs { get; set; }
        public string ServiceName { get; set; } = string.Empty;
    }
}

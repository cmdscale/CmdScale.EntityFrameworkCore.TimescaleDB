namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    public class WeatherData
    {
        public Guid Id { get; set; }
        public DateTime Time { get; set; }
        public DateTime Duration { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }
}

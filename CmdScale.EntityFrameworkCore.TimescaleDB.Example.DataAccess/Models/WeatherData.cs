namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models
{
    public class WeatherData
    {
        public Guid Id { get; set; }
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }
}

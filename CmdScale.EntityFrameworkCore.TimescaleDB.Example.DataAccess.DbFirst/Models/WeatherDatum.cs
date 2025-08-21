using System;
using System.Collections.Generic;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.DbFirst.Models;

public partial class WeatherDatum
{
    public Guid Id { get; set; }

    public DateTime Time { get; set; }

    public double Temperature { get; set; }

    public double Humidity { get; set; }
}

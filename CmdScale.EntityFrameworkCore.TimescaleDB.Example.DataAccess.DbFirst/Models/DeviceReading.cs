using System;
using System.Collections.Generic;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.DbFirst.Models;

public partial class DeviceReading
{
    public Guid Id { get; set; }

    public DateTime Time { get; set; }

    public string DeviceId { get; set; } = null!;

    public double Voltage { get; set; }

    public double Power { get; set; }
}

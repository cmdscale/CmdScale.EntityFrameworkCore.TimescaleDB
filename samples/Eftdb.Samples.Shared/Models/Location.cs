using System.ComponentModel.DataAnnotations.Schema;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    [ComplexType]
    public class Location
    {
        public string Site { get; set; } = string.Empty;
        public Coordinates Coordinates { get; set; } = new();
    }
}

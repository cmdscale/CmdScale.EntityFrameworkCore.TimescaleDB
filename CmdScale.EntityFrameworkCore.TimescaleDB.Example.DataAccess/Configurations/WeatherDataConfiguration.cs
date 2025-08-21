using CmdScale.EntityFrameworkCore.TimescaleDB.Annotation;
using CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example.DataAccess.Configurations
{
    public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
    {
        public void Configure(EntityTypeBuilder<WeatherData> builder)
        {
            builder.HasKey(x => new { x.Id, x.Time });
            builder.IsHypertable(x => x.Time);
        }
    }
}

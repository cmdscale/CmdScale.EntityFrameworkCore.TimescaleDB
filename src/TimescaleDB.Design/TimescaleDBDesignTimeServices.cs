using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

#pragma warning disable EF1001
[assembly: DesignTimeProviderServices("CmdScale.EntityFrameworkCore.TimescaleDB.Design.TimescaleDBDesignTimeServices")]
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
    public class TimescaleDBDesignTimeServices : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection services)
        {
            new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(services);

            services.AddSingleton<ICSharpMigrationOperationGenerator, TimescaleCSharpMigrationOperationGenerator>()
                    .AddSingleton<IDatabaseModelFactory, TimescaleDatabaseModelFactory>();
        }
    }
}
#pragma warning restore EF1001
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example
{
    public class TimescaleDBDesignTimeServices : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection services)
        {
            Console.WriteLine("=== This is another design time in the consumer project to test if it will overwrite the ICSharpMigrationOperationGenerator back to default. ===");

            // NOTE: When using project references, instead of package referneces, you need to uncomment these lines to find inject the correct ICSharpMigrationOperationGenerator
            // The reason for this is, because the CmdScale.EntityFrameworkCore.TimescaleDB.Design project only copies the required assembly-attribute when being packaged.

            services.AddSingleton<ICSharpMigrationOperationGenerator, TimescaleCSharpMigrationOperationGenerator>()
                    .AddSingleton<IDatabaseModelFactory, TimescaleDatabaseModelFactory>();
        }
    }
}

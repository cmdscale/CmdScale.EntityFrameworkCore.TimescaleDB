using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Example
{
    public class TimescaleDBDesignTimeServices : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection services)
        {
            Console.WriteLine("=== This is another design time in the consumer project to test if it will overwrite the ICSharpMigrationOperationGenerator back to defualt. ===");
        }
    }
}

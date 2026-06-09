using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils
{
    internal static class DesignTimeHelper
    {
        public static ICSharpHelper CreateRealCSharpHelper()
        {
            ServiceCollection services = new();
            services.AddEntityFrameworkDesignTimeServices();
            new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);

            ServiceProvider provider = services.BuildServiceProvider();
            return provider.GetRequiredService<ICSharpHelper>();
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
    public static class TimescaleDbServiceCollectionExtensions
    {
        /// <summary>
        /// Registers necessary services for the TimescaleDB provider.
        /// </summary>
        public static IServiceCollection AddEntityFrameworkTimescaleDb(this IServiceCollection services)
        {
            new EntityFrameworkRelationalServicesBuilder(services)
                .TryAdd<IMigrationsModelDiffer, TimescaleMigrationsModelDiffer>()
                .TryAdd<IConventionSetPlugin, TimescaleDbContextOptionsBuilderExtensions.TimescaleDbConventionSetPlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, TimescaleDbMethodCallTranslatorPlugin>();

            return services;
        }
    }
}

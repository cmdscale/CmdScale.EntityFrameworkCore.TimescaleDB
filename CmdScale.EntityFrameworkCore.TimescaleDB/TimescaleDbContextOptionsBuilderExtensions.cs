using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
    /// <summary>
    /// Provides extension methods to configure DbContextOptions for TimescaleDB.
    /// </summary>
    public static class TimescaleDbContextOptionsBuilderExtensions
    {
        /// <summary>
        /// Configures the DbContext to use TimescaleDB-aware migrations and conventions.
        /// </summary>
        /// <typeparam name="TContext">The type of the DbContext.</typeparam>
        /// <param name="optionsBuilder">The options builder for the DbContext.</param>
        public static DbContextOptionsBuilder<TContext> UseTimescaleDb<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder)
            where TContext : DbContext
        {
            ((DbContextOptionsBuilder)optionsBuilder).UseTimescaleDb();
            return optionsBuilder;
        }

        /// <summary>
        /// Configures the DbContext to use TimescaleDB-aware migrations and conventions.
        /// </summary>
        /// <param name="optionsBuilder">The options builder for the DbContext.</param>
        public static DbContextOptionsBuilder UseTimescaleDb(this DbContextOptionsBuilder optionsBuilder)
        {
            TimescaleDbOptionsExtension extension = optionsBuilder.Options.FindExtension<TimescaleDbOptionsExtension>() ?? new TimescaleDbOptionsExtension();
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;
        }

        /// <summary>
        /// The internal options extension that carries the TimescaeDB configuration.
        /// </summary>
        private class TimescaleDbOptionsExtension : IDbContextOptionsExtension
        {
            private DbContextOptionsExtensionInfo? _info;
            public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

            public void ApplyServices(IServiceCollection services)
            {
                services.AddSingleton<IConventionSetPlugin, TimescaleDbConventionSetPlugin>();
                services.AddScoped<IMigrationsModelDiffer, TimescaleMigrationsModelDiffer>();
                services.Replace(ServiceDescriptor.Scoped<IMigrationsSqlGenerator, TimescaleDbMigrationsSqlGenerator>());
            }

            public void Validate(IDbContextOptions options) { }

            /// <summary>
            /// The info class that provides metadata about the extension.
            /// </summary>
            private class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
            {
                public override bool IsDatabaseProvider => false;
                public override string LogFragment => "using TimescaleDB extensions";
                public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;
                public override int GetServiceProviderHashCode() => GetType().GetHashCode();
                public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) => debugInfo["TimescaleDB:Enabled"] = "True";
            }
        }

        public class TimescaleDbConventionSetPlugin : IConventionSetPlugin
        {
            public ConventionSet ModifyConventions(ConventionSet conventionSet)
            {
                conventionSet.EntityTypeAddedConventions.Add(new HypertableConvention());
                conventionSet.EntityTypeAddedConventions.Add(new ReorderPolicyConvention());
                conventionSet.EntityTypeAddedConventions.Add(new ContinuousAggregateConvention());
                return conventionSet;
            }
        }

    }
}

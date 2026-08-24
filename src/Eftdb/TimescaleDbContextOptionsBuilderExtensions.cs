using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
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
        /// <typeparam name="TContext">The type of the DbContext.</typeparam>
        /// <param name="optionsBuilder">The options builder for the DbContext.</param>
        /// <param name="configure">An action to configure TimescaleDB-specific options.</param>
        public static DbContextOptionsBuilder<TContext> UseTimescaleDb<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            Action<TimescaleDbOptions> configure)
            where TContext : DbContext
        {
            ((DbContextOptionsBuilder)optionsBuilder).UseTimescaleDb(configure);
            return optionsBuilder;
        }

        /// <summary>
        /// Configures the DbContext to use TimescaleDB-aware migrations and conventions.
        /// </summary>
        /// <param name="optionsBuilder">The options builder for the DbContext.</param>
        public static DbContextOptionsBuilder UseTimescaleDb(this DbContextOptionsBuilder optionsBuilder)
        {
            return ApplyTimescaleDbExtension(optionsBuilder, configure: null);
        }

        /// <summary>
        /// Configures the DbContext to use TimescaleDB-aware migrations and conventions.
        /// </summary>
        /// <param name="optionsBuilder">The options builder for the DbContext.</param>
        /// <param name="configure">An action to configure TimescaleDB-specific options.</param>
        public static DbContextOptionsBuilder UseTimescaleDb(
            this DbContextOptionsBuilder optionsBuilder,
            Action<TimescaleDbOptions> configure)
        {
            return ApplyTimescaleDbExtension(optionsBuilder, configure);
        }

        private static DbContextOptionsBuilder ApplyTimescaleDbExtension(
            DbContextOptionsBuilder optionsBuilder,
            Action<TimescaleDbOptions>? configure)
        {
            TimescaleDbOptions options = new();
            configure?.Invoke(options);

            TimescaleDbOptionsExtension extension = (configure != null || optionsBuilder.Options.FindExtension<TimescaleDbOptionsExtension>() is null)
                ? new TimescaleDbOptionsExtension(options)
                : optionsBuilder.Options.FindExtension<TimescaleDbOptionsExtension>()!;

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;
        }

        /// <summary>
        /// The internal options extension that carries the TimescaleDB configuration.
        /// </summary>
        private class TimescaleDbOptionsExtension(TimescaleDbOptions timescaleDbOptions) : IDbContextOptionsExtension
        {
            internal TimescaleDbOptions TimescaleDbOptions => timescaleDbOptions;

            private DbContextOptionsExtensionInfo? _info;
            public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

            public void ApplyServices(IServiceCollection services)
            {
                services.AddSingleton(timescaleDbOptions);
                services.AddSingleton<IConventionSetPlugin, TimescaleDbConventionSetPlugin>();
                services.AddScoped<IMigrationsModelDiffer, TimescaleMigrationsModelDiffer>();
                services.Replace(ServiceDescriptor.Scoped<IMigrationsSqlGenerator, TimescaleDbMigrationsSqlGenerator>());
                services.TryAddEnumerable(
                    ServiceDescriptor.Scoped<IMethodCallTranslatorPlugin, TimescaleDbMethodCallTranslatorPlugin>());
            }

            public void Validate(IDbContextOptions options) { }

            /// <summary>
            /// The info class that provides metadata about the extension.
            /// </summary>
            private class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
            {
                private new TimescaleDbOptionsExtension Extension => (TimescaleDbOptionsExtension)base.Extension;

                public override bool IsDatabaseProvider => false;
                public override string LogFragment => "using TimescaleDB extensions";

                public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
                    => other is ExtensionInfo otherInfo
                       && Extension.TimescaleDbOptions.UseLegacyCompressionNames == otherInfo.Extension.TimescaleDbOptions.UseLegacyCompressionNames
                       && Extension.TimescaleDbOptions.IsApacheEdition == otherInfo.Extension.TimescaleDbOptions.IsApacheEdition;

                public override int GetServiceProviderHashCode()
                    => HashCode.Combine(
                        GetType(),
                        Extension.TimescaleDbOptions.UseLegacyCompressionNames,
                        Extension.TimescaleDbOptions.IsApacheEdition);

                public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                {
                    debugInfo["TimescaleDB:Enabled"] = "True";
                    debugInfo["TimescaleDB:UseLegacyCompressionNames"] = Extension.TimescaleDbOptions.UseLegacyCompressionNames.ToString();
                    debugInfo["TimescaleDB:IsApacheEdition"] = Extension.TimescaleDbOptions.IsApacheEdition.ToString();
                }
            }
        }

        internal class TimescaleDbConventionSetPlugin : IConventionSetPlugin
        {
            public ConventionSet ModifyConventions(ConventionSet conventionSet)
            {
                conventionSet.EntityTypeAddedConventions.Add(new HypertableConvention());
                conventionSet.EntityTypeAddedConventions.Add(new ReorderPolicyConvention());
                conventionSet.EntityTypeAddedConventions.Add(new ContinuousAggregateConvention());
                conventionSet.EntityTypeAddedConventions.Add(new ContinuousAggregatePolicyConvention());
                conventionSet.EntityTypeAddedConventions.Add(new RetentionPolicyConvention());
                conventionSet.EntityTypeAddedConventions.Add(new CompressionPolicyConvention());
                conventionSet.ModelFinalizedConventions.Add(new TimeColumnStoreTypeValidationConvention());
                conventionSet.ModelFinalizedConventions.Add(new CompressionPolicyPrerequisiteValidationConvention());
                conventionSet.ModelFinalizedConventions.Add(new SparseIndexValidationConvention());
                return conventionSet;
            }
        }

    }
}

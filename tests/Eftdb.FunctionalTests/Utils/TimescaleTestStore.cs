using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Npgsql;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;

public class TimescaleTestStore : RelationalTestStore
{
    private static readonly ConcurrentDictionary<string, TimescaleTestStore> _sharedStores = new();

    private TimescaleTestStore(string name, bool shared, string connectionString)
        : base(name, shared, new NpgsqlConnection(connectionString))
    {
    }

    public static TimescaleTestStore Create(string name, string connectionString)
        => new(name, shared: false, connectionString);

    public static TimescaleTestStore GetOrCreateShared(string name, string connectionString)
        => _sharedStores.GetOrAdd(name, key =>
        {
            TimescaleTestStore store = new(name, shared: true, connectionString);

            DbContextOptions<MigrationsInfrastructureFixtureBase.MigrationsContext> options =
                new DbContextOptionsBuilder<MigrationsInfrastructureFixtureBase.MigrationsContext>()
                    .UseNpgsql(connectionString).UseTimescaleDb().Options;
            _ = store.InitializeAsync(null,
                () => new MigrationsInfrastructureFixtureBase.MigrationsContext(options), null).Result;
            return store;
        });

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.AddInterceptors(new TimescaleMigrationsTestInterceptor()).UseNpgsql(ConnectionString, options => { })
            .UseTimescaleDb().EnableSensitiveDataLogging();

    public override Task CleanAsync(DbContext context)
    {
        context.Database.EnsureClean();
        return Task.CompletedTask;
    }

    public void ExecuteScript(string script)
    {
        if (ConnectionState != ConnectionState.Open)
            Connection.Open();

        using DbCommand cmd = Connection.CreateCommand();
        cmd.CommandText = script;
        cmd.ExecuteNonQuery();
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Npgsql;
using System.Collections.Concurrent;
using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;

public class TimescaleTestStore : RelationalTestStore
{
    private static readonly ConcurrentDictionary<string, TimescaleTestStore> _sharedStores = new();

    protected override DbConnection Connection { get => base.Connection; set => base.Connection = value; }
    public override string ConnectionString { get => base.ConnectionString; protected set => base.ConnectionString = value; }

    private TimescaleTestStore(string name, bool shared, string connectionString)
        : base(name, shared)
    {
        ConnectionString = connectionString;
        Connection = new NpgsqlConnection(ConnectionString);
    }

    public static TimescaleTestStore Create(string name, string connectionString)
        => new(name, shared: false, connectionString);

    public static TimescaleTestStore GetOrCreateShared(string name, string connectionString)
        => _sharedStores.GetOrAdd(name, _ =>
        {
            TimescaleTestStore store = new(name, shared: true, connectionString);

            DbContextOptions<MigrationsInfrastructureFixtureBase.MigrationsContext> options = new DbContextOptionsBuilder<MigrationsInfrastructureFixtureBase.MigrationsContext>()
                .UseNpgsql(connectionString).UseTimescaleDb().Options;
            store.Initialize(null, () => new MigrationsInfrastructureFixtureBase.MigrationsContext(options), null);
            return store;
        });

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.AddInterceptors(new TimescaleMigrationsTestInterceptor()).UseNpgsql(ConnectionString, options =>
        {
        }).UseTimescaleDb().EnableSensitiveDataLogging();

    public override void Clean(DbContext context)
    {
        context.Database.EnsureClean();
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Smoke test proving the hypertable scaffolding pipeline runs against the Apache (OSS) image
/// and extracts a plain hypertable's annotations without throwing.
/// </summary>
public class HypertableApacheScaffoldingSmokeTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(TimescaleImages.Apache)
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Should_Scaffold_Plain_Hypertable_On_Apache

    private class ApacheScaffoldMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ApacheScaffoldContext(string connectionString) : DbContext
    {
        public DbSet<ApacheScaffoldMetric> Metrics => Set<ApacheScaffoldMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApacheScaffoldMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("apache_scaffold_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Scaffold_Plain_Hypertable_On_Apache()
    {
        await using ApacheScaffoldContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        Assert.True(result.ContainsKey(("public", "apache_scaffold_metrics")));

        HypertableScaffoldingExtractor.HypertableInfo info =
            (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "apache_scaffold_metrics")];
        Assert.Equal("Timestamp", info.TimeColumnName);
        Assert.NotNull(info.ChunkTimeInterval);
        Assert.False(info.CompressionEnabled);
    }

    #endregion
}

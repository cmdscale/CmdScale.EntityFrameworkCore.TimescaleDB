using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Diagnostics.Internal;
using System.Diagnostics;
using Testcontainers.PostgreSql;

#pragma warning disable EF1001 // Internal EF Core API usage required for testing scaffolding infrastructure

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class HypertableMixedCaseOrderbyScaffoldingTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:latest-pg17")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task<string> CreateIsolatedDbAsync()
    {
        string testDbName = $"test_{Guid.NewGuid():N}";

        await using NpgsqlConnection adminConnection = new(_connectionString);
        await adminConnection.OpenAsync();

        await using (NpgsqlCommand createCmd = new($"CREATE DATABASE {testDbName}", adminConnection))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        string connStr = _connectionString!.Replace("test_db", testDbName);
        await using NpgsqlConnection testConnection = new(connStr);
        await testConnection.OpenAsync();
        await using (NpgsqlCommand extCmd = new("CREATE EXTENSION IF NOT EXISTS timescaledb", testConnection))
        {
            await extCmd.ExecuteNonQueryAsync();
        }

        return connStr;
    }

    private static TimescaleDatabaseModelFactory CreateFactory()
    {
        LoggerFactory loggerFactory = new();
        DiagnosticsLogger<DbLoggerCategory.Scaffolding> logger = new(
            loggerFactory,
            new LoggingOptions(),
            new DiagnosticListener("Test"),
            new NpgsqlLoggingDefinitions(),
            new NullDbContextLogger());

        return new TimescaleDatabaseModelFactory(logger);
    }

    private sealed class NullDbContextLogger : IDbContextLogger
    {
        public void Log(EventData eventData) { }
        public bool ShouldLog(EventId eventId, LogLevel logLevel) => false;
    }

    // ── Mixed-case orderby: annotation uses unquoted column name ──

    #region Should_Scaffold_MixedCase_OrderBy_Without_Quote_Characters

    private class MixedCaseOrderByEntity
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class MixedCaseOrderByContext(string conn) : DbContext
    {
        public DbSet<MixedCaseOrderByEntity> Readings => Set<MixedCaseOrderByEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<MixedCaseOrderByEntity>(e =>
            {
                e.ToTable("mc_orderby_scaffold");
                e.HasNoKey();
                e.Property(x => x.Timestamp).HasColumnName("Timestamp");
                e.Property(x => x.DeviceId).HasColumnName("DeviceId");
                e.IsHypertable(x => x.Timestamp)
                    .WithCompressionSegmentBy(x => x.DeviceId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Timestamp)]);
            });
    }

    [Fact]
    public async Task Should_Scaffold_MixedCase_OrderBy_Without_Quote_Characters()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using MixedCaseOrderByContext context = new(connStr);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(connStr);
        DatabaseModelFactoryOptions options = new(tables: ["mc_orderby_scaffold"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Assert
        DatabaseTable? table = model.Tables.FirstOrDefault(t => t.Name == "mc_orderby_scaffold");
        Assert.NotNull(table);

        string? orderByAnnotation = table[HypertableAnnotations.CompressionOrderBy] as string;
        Assert.NotNull(orderByAnnotation);

        Assert.Contains("Timestamp DESC", orderByAnnotation);
        Assert.DoesNotContain("\"Timestamp\"", orderByAnnotation);
    }

    #endregion

    // ── Mixed-case orderby: no phantom alter after scaffolding ──

    #region Should_Not_Generate_Phantom_Alter_After_MixedCase_OrderBy_Scaffolding

    private class PhantomMixedCaseEntity
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
    }

    private class PhantomMixedCaseSourceContext(string conn) : DbContext
    {
        public DbSet<PhantomMixedCaseEntity> Readings => Set<PhantomMixedCaseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<PhantomMixedCaseEntity>(e =>
            {
                e.ToTable("mc_phantom_orderby");
                e.HasNoKey();
                e.Property(x => x.Timestamp).HasColumnName("Timestamp");
                e.Property(x => x.DeviceId).HasColumnName("DeviceId");
                e.IsHypertable(x => x.Timestamp)
                    .WithCompressionSegmentBy(x => x.DeviceId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Timestamp)]);
            });
    }

    [Fact]
    public async Task Should_Not_Generate_Phantom_Alter_After_MixedCase_OrderBy_Scaffolding()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using PhantomMixedCaseSourceContext sourceContext = new(connStr);
        await CreateDatabaseViaMigrationAsync(sourceContext);

        // Act
        IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> operations =
            GenerateMigrationOperations(sourceContext, sourceContext);

        // Assert
        Assert.Empty(operations);
    }

    #endregion
}

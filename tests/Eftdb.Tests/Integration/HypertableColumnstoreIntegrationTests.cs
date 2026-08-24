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
using System.Text.Json;
using Testcontainers.PostgreSql;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

#pragma warning disable EF1001 // Internal EF Core API usage required for testing scaffolding infrastructure

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class HypertableColumnstoreIntegrationTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(TimescaleImages.Community)
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

    private static async Task<string?> GetReloption(string connectionString, string tableName, string optionName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT opt.option_value
            FROM pg_catalog.pg_class AS c
            JOIN LATERAL pg_catalog.pg_options_to_table(c.reloptions) AS opt ON true
            WHERE c.relname = @table
              AND opt.option_name = @option
            LIMIT 1";
        cmd.Parameters.AddWithValue("table", tableName);
        cmd.Parameters.AddWithValue("option", optionName);

        object? result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? null : (string)result;
    }

    private static async Task<string?> GetColumnstoreSetting(string connectionString, string tableName, string columnName)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT hcs.{columnName}::text
            FROM timescaledb_information.hypertable_columnstore_settings AS hcs
            WHERE hcs.hypertable::text = @table
            LIMIT 1";
        cmd.Parameters.AddWithValue("table", tableName);

        object? result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? null : (string)result;
    }

    private static async Task<bool> SparseIndexContainsEntry(
        string connectionString,
        string tableName,
        string indexType,
        string columnName)
    {
        string? indexJson = await GetColumnstoreSetting(connectionString, tableName, "index");
        if (string.IsNullOrEmpty(indexJson))
        {
            return false;
        }

        using JsonDocument doc = JsonDocument.Parse(indexJson);
        foreach (JsonElement entry in doc.RootElement.EnumerateArray())
        {
            string? type = entry.TryGetProperty("type", out JsonElement t) ? t.GetString() : null;
            string? col = entry.TryGetProperty("column", out JsonElement c) ? c.GetString() : null;
            string? source = entry.TryGetProperty("source", out JsonElement s) ? s.GetString() : null;

            if (string.Equals(type, indexType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(col, columnName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(source, "config", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ── Migration roundtrip: sparse_index applied to DB ──

    #region Should_Create_Hypertable_With_SparseIndex_In_Database

    private class SparseIndexMigrationEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class SparseIndexMigrationContext(string conn) : DbContext
    {
        public DbSet<SparseIndexMigrationEntity> Metrics => Set<SparseIndexMigrationEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<SparseIndexMigrationEntity>(e =>
            {
                e.ToTable("cs_sparse_create");
                e.HasNoKey();
                e.Property(x => x.DeviceId).HasColumnName("device_id");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_SparseIndex_In_Database()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        // Act
        await using SparseIndexMigrationContext context = new(connStr);
        await CreateDatabaseViaMigrationAsync(context);

        // Assert
        bool hasBloomDeviceId = await SparseIndexContainsEntry(connStr, "cs_sparse_create", "bloom", "device_id");
        Assert.True(hasBloomDeviceId);
    }

    #endregion

    // ── Migration roundtrip: compress_chunk_time_interval applied to DB ──

    #region Should_Create_Hypertable_With_CompressChunkTimeInterval_In_Database

    private class CctiMigrationEntity { public DateTime Ts { get; set; } }

    private class CctiMigrationContext(string conn) : DbContext
    {
        public DbSet<CctiMigrationEntity> Metrics => Set<CctiMigrationEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<CctiMigrationEntity>(e =>
            {
                e.ToTable("cs_ccti_create");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("24 hours");
            });
    }

    [Fact]
    public async Task Should_Create_Hypertable_With_CompressChunkTimeInterval_In_Database()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        // Act
        await using CctiMigrationContext context = new(connStr);
        await CreateDatabaseViaMigrationAsync(context);

        // Assert
        string? value = await GetColumnstoreSetting(connStr, "cs_ccti_create", "compress_interval_length");
        Assert.NotNull(value);
    }

    #endregion

    // ── Migration alter: sparse_index changed ──

    #region Should_Update_SparseIndex_When_Altered

    private class SparseIndexAlterEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class SparseIndexAlterSourceContext(string conn) : DbContext
    {
        public DbSet<SparseIndexAlterEntity> Metrics => Set<SparseIndexAlterEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<SparseIndexAlterEntity>(e =>
            {
                e.ToTable("cs_sparse_alter");
                e.HasNoKey();
                e.Property(x => x.Ts).HasColumnName("ts");
                e.Property(x => x.DeviceId).HasColumnName("device_id");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    private class SparseIndexAlterTargetContext(string conn) : DbContext
    {
        public DbSet<SparseIndexAlterEntity> Metrics => Set<SparseIndexAlterEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<SparseIndexAlterEntity>(e =>
            {
                e.ToTable("cs_sparse_alter");
                e.HasNoKey();
                e.Property(x => x.Ts).HasColumnName("ts");
                e.Property(x => x.DeviceId).HasColumnName("device_id");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id), minmax(ts)");
            });
    }

    [Fact]
    public async Task Should_Update_SparseIndex_When_Altered()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using SparseIndexAlterSourceContext sourceContext = new(connStr);
        await CreateDatabaseViaMigrationAsync(sourceContext);

        // Act
        await using SparseIndexAlterTargetContext targetContext = new(connStr);
        await AlterDatabaseViaMigrationAsync(sourceContext, targetContext);

        // Assert
        bool hasBloom = await SparseIndexContainsEntry(connStr, "cs_sparse_alter", "bloom", "device_id");
        bool hasMinmax = await SparseIndexContainsEntry(connStr, "cs_sparse_alter", "minmax", "ts");
        Assert.True(hasBloom);
        Assert.True(hasMinmax);
    }

    #endregion

    // ── Migration alter: sparse_index removed → RESET ──

    #region Should_Remove_SparseIndex_When_Set_To_Null

    private class SparseIndexRemoveAlterEntity { public DateTime Ts { get; set; } public double Value { get; set; } }

    private class SparseIndexRemoveSourceContext(string conn) : DbContext
    {
        public DbSet<SparseIndexRemoveAlterEntity> Metrics => Set<SparseIndexRemoveAlterEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<SparseIndexRemoveAlterEntity>(e =>
            {
                e.ToTable("cs_sparse_remove");
                e.HasNoKey();
                e.Property(x => x.Value).HasColumnName("value");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(value)");
            });
    }

    private class SparseIndexRemoveTargetContext(string conn) : DbContext
    {
        public DbSet<SparseIndexRemoveAlterEntity> Metrics => Set<SparseIndexRemoveAlterEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<SparseIndexRemoveAlterEntity>(e =>
            {
                e.ToTable("cs_sparse_remove");
                e.HasNoKey();
                e.Property(x => x.Value).HasColumnName("value");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public async Task Should_Remove_SparseIndex_When_Set_To_Null()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using SparseIndexRemoveSourceContext sourceContext = new(connStr);
        await CreateDatabaseViaMigrationAsync(sourceContext);

        // Act
        await using SparseIndexRemoveTargetContext targetContext = new(connStr);
        await AlterDatabaseViaMigrationAsync(sourceContext, targetContext);

        // Assert
        string? value = await GetReloption(connStr, "cs_sparse_remove", "timescaledb.sparse_index");
        Assert.Null(value);
    }

    #endregion

    // ── Scaffolding: sparse_index read from reloptions ──

    #region Should_Scaffold_SparseIndex_From_Reloptions

    private class ScaffoldSparseIndexEntity { public DateTime Ts { get; set; } public double Value { get; set; } }

    private class ScaffoldSparseIndexContext(string conn) : DbContext
    {
        public DbSet<ScaffoldSparseIndexEntity> Metrics => Set<ScaffoldSparseIndexEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<ScaffoldSparseIndexEntity>(e =>
            {
                e.ToTable("cs_scaffold_sparse");
                e.HasNoKey();
                e.Property(x => x.Value).HasColumnName("value");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(value)");
            });
    }

    [Fact]
    public async Task Should_Scaffold_SparseIndex_From_Reloptions()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using ScaffoldSparseIndexContext context = new(connStr);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(connStr);
        DatabaseModelFactoryOptions options = new(tables: ["cs_scaffold_sparse"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Assert
        DatabaseTable? table = model.Tables.FirstOrDefault(t => t.Name == "cs_scaffold_sparse");
        Assert.NotNull(table);
        Assert.Equal("bloom(value)", table[HypertableAnnotations.CompressionSparseIndex]);
    }

    #endregion

    // ── Scaffolding: compress_chunk_time_interval read from columnstore settings ──

    #region Should_Scaffold_CompressChunkTimeInterval_From_ColumnstoreSettings

    private class ScaffoldCctiEntity { public DateTime Ts { get; set; } }

    private class ScaffoldCctiContext(string conn) : DbContext
    {
        public DbSet<ScaffoldCctiEntity> Metrics => Set<ScaffoldCctiEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<ScaffoldCctiEntity>(e =>
            {
                e.ToTable("cs_scaffold_ccti");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("7 days");
            });
    }

    [Fact]
    public async Task Should_Scaffold_CompressChunkTimeInterval_From_ColumnstoreSettings()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using ScaffoldCctiContext context = new(connStr);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(connStr);
        DatabaseModelFactoryOptions options = new(tables: ["cs_scaffold_ccti"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Assert
        DatabaseTable? table = model.Tables.FirstOrDefault(t => t.Name == "cs_scaffold_ccti");
        Assert.NotNull(table);
        Assert.NotNull(table[HypertableAnnotations.CompressChunkTimeInterval]);
    }

    #endregion

    // ── Scaffolding: hypertable WITHOUT columnstore settings → no annotations set ──

    #region Should_Not_Scaffold_Columnstore_Annotations_When_Not_Configured

    private class ScaffoldNoColumnstoreEntity { public DateTime Ts { get; set; } }

    private class ScaffoldNoColumnstoreContext(string conn) : DbContext
    {
        public DbSet<ScaffoldNoColumnstoreEntity> Metrics => Set<ScaffoldNoColumnstoreEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<ScaffoldNoColumnstoreEntity>(e =>
            {
                e.ToTable("cs_scaffold_none");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    [Fact]
    public async Task Should_Not_Scaffold_Columnstore_Annotations_When_Not_Configured()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using ScaffoldNoColumnstoreContext context = new(connStr);
        await CreateDatabaseViaMigrationAsync(context);

        // Act
        TimescaleDatabaseModelFactory factory = CreateFactory();
        await using NpgsqlConnection connection = new(connStr);
        DatabaseModelFactoryOptions options = new(tables: ["cs_scaffold_none"], schemas: []);
        DatabaseModel model = factory.Create(connection, options);

        // Assert
        DatabaseTable? table = model.Tables.FirstOrDefault(t => t.Name == "cs_scaffold_none");
        Assert.NotNull(table);
        Assert.Null(table[HypertableAnnotations.CompressionSparseIndex]);
        Assert.Null(table[HypertableAnnotations.CompressChunkTimeInterval]);
    }

    #endregion

    // ── Phantom migration: scaffolded model diffs to zero operations ──

    #region Should_Not_Generate_Phantom_Hypertable_Alter_After_Scaffolding_Columnstore

    private class PhantomMigrationEntity { public DateTime Ts { get; set; } public double Value { get; set; } }

    private class PhantomMigrationSourceContext(string conn) : DbContext
    {
        public DbSet<PhantomMigrationEntity> Metrics => Set<PhantomMigrationEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql(conn).UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<PhantomMigrationEntity>(e =>
            {
                e.ToTable("cs_phantom");
                e.HasNoKey();
                e.Property(x => x.Value).HasColumnName("value");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(value)");
            });
    }

    [Fact]
    public async Task Should_Not_Generate_Phantom_Hypertable_Alter_After_Scaffolding_Columnstore()
    {
        // Arrange
        string connStr = await CreateIsolatedDbAsync();

        await using PhantomMigrationSourceContext sourceContext = new(connStr);
        await CreateDatabaseViaMigrationAsync(sourceContext);

        // Act
        IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> operations =
            GenerateMigrationOperations(sourceContext, sourceContext);

        // Assert
        Assert.Empty(operations);
    }

    #endregion
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Testcontainers.PostgreSql;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class HypertableMigrateDataIntegrationTests : MigrationTestBase, IAsyncLifetime
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

    #region Helper Methods

    private static async Task<bool> IsHypertableAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
                SELECT COUNT(*) > 0
                FROM timescaledb_information.hypertables
                WHERE hypertable_name = @tableName;
            ";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool boolResult && boolResult;
    }

    private static async Task<int> GetRowCountAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\";";

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is long longResult ? (int)longResult :
               result is int intResult ? intResult : 0;
    }

    #endregion

    #region Should_Generate_Migration_SQL_With_MigrateData_True_FluentAPI

    private class MigrateDataFluentApiEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataFluentApiContext(string connectionString) : DbContext
    {
        public DbSet<MigrateDataFluentApiEntity> Metrics => Set<MigrateDataFluentApiEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataFluentApiEntity>(entity =>
            {
                entity.ToTable("MigrateDataFluentApi");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithMigrateData(true);
            });
        }
    }

    [Fact]
    public void Should_Generate_Migration_SQL_With_MigrateData_True_FluentAPI()
    {
        // Arrange
        using MigrateDataFluentApiContext context = new(_connectionString!);

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        CreateHypertableOperation? hypertableOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(hypertableOp);
        Assert.Equal("MigrateDataFluentApi", hypertableOp.TableName);
        Assert.True(hypertableOp.MigrateData);

        // Act
        IMigrationsSqlGenerator sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, context.Model);

        // Assert
        string allSql = string.Join("\n", commands.Select(c => c.CommandText));
        Assert.Contains("migrate_data => true", allSql);
    }

    #endregion

    #region Should_Generate_Migration_SQL_With_MigrateData_True_Attribute

    [Hypertable("Timestamp", MigrateData = true)]
    private class MigrateDataAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MigrateDataAttributeContext(string connectionString) : DbContext
    {
        public DbSet<MigrateDataAttributeEntity> Metrics => Set<MigrateDataAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataAttributeEntity>(entity =>
            {
                entity.ToTable("MigrateDataAttribute");
                entity.HasNoKey();
            });
        }
    }

    [Fact]
    public void Should_Generate_Migration_SQL_With_MigrateData_True_Attribute()
    {
        // Arrange
        using MigrateDataAttributeContext context = new(_connectionString!);

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        CreateHypertableOperation? hypertableOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(hypertableOp);
        Assert.Equal("MigrateDataAttribute", hypertableOp.TableName);
        Assert.True(hypertableOp.MigrateData);

        // Act
        IMigrationsSqlGenerator sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, context.Model);

        // Assert
        string allSql = string.Join("\n", commands.Select(c => c.CommandText));
        Assert.Contains("migrate_data => true", allSql);
    }

    #endregion

    #region Should_Generate_Migration_SQL_Without_MigrateData_By_Default

    private class DefaultMigrateDataEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultMigrateDataContext(string connectionString) : DbContext
    {
        public DbSet<DefaultMigrateDataEntity> Metrics => Set<DefaultMigrateDataEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultMigrateDataEntity>(entity =>
            {
                entity.ToTable("DefaultMigrateData");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Generate_Migration_SQL_Without_MigrateData_By_Default()
    {
        // Arrange
        using DefaultMigrateDataContext context = new(_connectionString!);

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        CreateHypertableOperation? hypertableOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(hypertableOp);
        Assert.Equal("DefaultMigrateData", hypertableOp.TableName);
        Assert.False(hypertableOp.MigrateData);

        // Act
        IMigrationsSqlGenerator sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, context.Model);

        // Assert
        string allSql = string.Join("\n", commands.Select(c => c.CommandText));
        Assert.DoesNotContain("migrate_data", allSql);
    }

    #endregion

    #region Should_Migrate_Existing_Data_When_Converting_To_Hypertable

    private class ExistingDataEntity
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Temperature { get; set; }
    }

    private class InitialRegularTableContext(string connectionString) : DbContext
    {
        public DbSet<ExistingDataEntity> SensorData => Set<ExistingDataEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExistingDataEntity>(entity =>
            {
                entity.ToTable("SensorDataMigration");
                entity.HasNoKey();
            });
        }
    }

    private class ConvertedHypertableContext(string connectionString) : DbContext
    {
        public DbSet<ExistingDataEntity> SensorData => Set<ExistingDataEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExistingDataEntity>(entity =>
            {
                entity.ToTable("SensorDataMigration");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithMigrateData(true);
            });
        }
    }

    [Fact]
    public async Task Should_Migrate_Existing_Data_When_Converting_To_Hypertable()
    {
        // Arrange
        await using InitialRegularTableContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await initialContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""SensorDataMigration"" (""Timestamp"", ""DeviceId"", ""Temperature"")
            VALUES
                ({new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc)}, {"device_1"}, {20.5}),
                ({new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc)}, {"device_2"}, {21.0}),
                ({new DateTime(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc)}, {"device_3"}, {19.5})", TestContext.Current.CancellationToken);

        int countBeforeConversion = await GetRowCountAsync(initialContext, "SensorDataMigration");
        Assert.Equal(3, countBeforeConversion);

        // Act
        await using ConvertedHypertableContext convertedContext = new(_connectionString!);
        await AlterDatabaseViaMigrationAsync(initialContext, convertedContext);

        // Assert
        bool isHypertable = await IsHypertableAsync(convertedContext, "SensorDataMigration");
        Assert.True(isHypertable);

        int countAfterConversion = await GetRowCountAsync(convertedContext, "SensorDataMigration");
        Assert.Equal(3, countAfterConversion);

        List<ExistingDataEntity> data = await convertedContext.SensorData.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, data.Count);
        Assert.Contains(data, d => d.DeviceId == "device_1" && d.Temperature == 20.5);
        Assert.Contains(data, d => d.DeviceId == "device_2" && d.Temperature == 21.0);
        Assert.Contains(data, d => d.DeviceId == "device_3" && d.Temperature == 19.5);
    }

    #endregion

    #region Should_Apply_MigrateData_False_When_Converting_To_Hypertable

    private class MigrateDataFalseEntity
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Temperature { get; set; }
    }

    private class InitialRegularTableMigrateDataFalseContext(string connectionString) : DbContext
    {
        public DbSet<MigrateDataFalseEntity> SensorData => Set<MigrateDataFalseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataFalseEntity>(entity =>
            {
                entity.ToTable("SensorDataNoMigration");
                entity.HasNoKey();
            });
        }
    }

    private class ConvertedHypertableMigrateDataFalseContext(string connectionString) : DbContext
    {
        public DbSet<MigrateDataFalseEntity> SensorData => Set<MigrateDataFalseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MigrateDataFalseEntity>(entity =>
            {
                entity.ToTable("SensorDataNoMigration");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithMigrateData(false);
            });
        }
    }

    [Fact]
    public async Task Should_Apply_MigrateData_False_When_Converting_To_Hypertable()
    {
        // Arrange
        await using InitialRegularTableMigrateDataFalseContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await initialContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""SensorDataNoMigration"" (""Timestamp"", ""DeviceId"", ""Temperature"")
            VALUES
                ({new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc)}, {"device_1"}, {20.5})", TestContext.Current.CancellationToken);

        int countBeforeConversion = await GetRowCountAsync(initialContext, "SensorDataNoMigration");
        Assert.Equal(1, countBeforeConversion);

        // Act
        await using ConvertedHypertableMigrateDataFalseContext convertedContext = new(_connectionString!);
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initialContext, convertedContext);

        // Assert
        CreateHypertableOperation? hypertableOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(hypertableOp);
        Assert.False(hypertableOp.MigrateData);

        IMigrationsSqlGenerator sqlGenerator = convertedContext.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, convertedContext.Model);

        string allSql = string.Join("\n", commands.Select(c => c.CommandText));
        Assert.DoesNotContain("migrate_data", allSql);
    }

    #endregion
}

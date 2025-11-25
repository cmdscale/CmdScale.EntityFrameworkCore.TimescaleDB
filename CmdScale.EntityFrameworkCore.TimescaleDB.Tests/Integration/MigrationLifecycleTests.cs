using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class MigrationLifecycleTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("timescale/timescaledb:latest-pg16")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Migration_Should_Generate_CreateHypertable_Operation

    private class GenerateHypertableMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class GenerateHypertableContext(string connectionString) : DbContext
    {
        public DbSet<GenerateHypertableMetric> Metrics => Set<GenerateHypertableMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GenerateHypertableMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Generate_CreateHypertable_Operation()
    {
        await using GenerateHypertableContext context = new(_connectionString!);

        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        CreateHypertableOperation? createHypertable = operations
            .OfType<CreateHypertableOperation>()
            .FirstOrDefault();

        Assert.NotNull(createHypertable);
        Assert.Equal("Metrics", createHypertable.TableName);
        Assert.Equal("Timestamp", createHypertable.TimeColumnName);
        Assert.Equal("1 day", createHypertable.ChunkTimeInterval);
    }

    #endregion

    #region Migration_Should_Create_Hypertable_In_Database

    private class CreateHypertableDbMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateHypertableDbContext(string connectionString) : DbContext
    {
        public DbSet<CreateHypertableDbMetric> Metrics => Set<CreateHypertableDbMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateHypertableDbMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Create_Hypertable_In_Database()
    {
        await using CreateHypertableDbContext context = new(_connectionString!);

        await CreateDatabaseViaMigrationAsync(context);

        bool isHypertable = await IsHypertableAsync(context, "Metrics");
        Assert.True(isHypertable);
    }

    #endregion

    #region Migration_Should_Generate_AlterHypertable_When_ChunkInterval_Changes

    private class AlterChunkIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AlterChunkIntervalInitialContext(string connectionString) : DbContext
    {
        public DbSet<AlterChunkIntervalMetric> Metrics => Set<AlterChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterChunkIntervalMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 day");
            });
        }
    }

    private class AlterChunkIntervalModifiedContext(string connectionString) : DbContext
    {
        public DbSet<AlterChunkIntervalMetric> Metrics => Set<AlterChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterChunkIntervalMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("12 hours"); // <-- Changed from "1 day"
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Generate_AlterHypertable_When_ChunkInterval_Changes()
    {
        await using AlterChunkIntervalInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await using AlterChunkIntervalModifiedContext modifiedContext = new(_connectionString!);
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initialContext, modifiedContext);

        AlterHypertableOperation? alterHypertable = operations
            .OfType<AlterHypertableOperation>()
            .FirstOrDefault();

        Assert.NotNull(alterHypertable);
        Assert.Equal("1 day", alterHypertable.OldChunkTimeInterval);
        Assert.Equal("12 hours", alterHypertable.ChunkTimeInterval);
    }

    #endregion

    #region Migration_Should_Apply_ChunkInterval_Change_To_Database

    private class ApplyChunkIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ApplyChunkIntervalInitialContext(string connectionString) : DbContext
    {
        public DbSet<ApplyChunkIntervalMetric> Metrics => Set<ApplyChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApplyChunkIntervalMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("1 day");
            });
        }
    }

    private class ApplyChunkIntervalModifiedContext(string connectionString) : DbContext
    {
        public DbSet<ApplyChunkIntervalMetric> Metrics => Set<ApplyChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApplyChunkIntervalMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                       .WithChunkTimeInterval("12 hours"); // <-- Changed from "1 day"
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Apply_ChunkInterval_Change_To_Database()
    {
        await using ApplyChunkIntervalInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await using ApplyChunkIntervalModifiedContext modifiedContext = new(_connectionString!);
        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        string chunkInterval = await GetChunkIntervalAsync(modifiedContext, "Metrics");
        Assert.Contains("12:00:00", chunkInterval);
    }

    #endregion

    #region Migration_Should_Generate_CreateContinuousAggregate_Operation

    private class GenerateCAMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class GenerateCAAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class GenerateCAContext(string connectionString) : DbContext
    {
        public DbSet<GenerateCAMetric> Metrics => Set<GenerateCAMetric>();
        public DbSet<GenerateCAAggregate> HourlyMetrics => Set<GenerateCAAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GenerateCAMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<GenerateCAAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<GenerateCAAggregate, GenerateCAMetric>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Generate_CreateContinuousAggregate_Operation()
    {
        await using GenerateCAContext context = new(_connectionString!);

        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        CreateContinuousAggregateOperation? createCA = operations
            .OfType<CreateContinuousAggregateOperation>()
            .FirstOrDefault();

        Assert.NotNull(createCA);
        Assert.Equal("hourly_metrics", createCA.MaterializedViewName);
        Assert.Equal("1 hour", createCA.TimeBucketWidth);
        Assert.Contains("AvgValue:Avg:Value", createCA.AggregateFunctions);
    }

    #endregion

    #region Migration_Should_Create_ContinuousAggregate_In_Database

    private class CreateCADbMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateCADbAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CreateCADbContext(string connectionString) : DbContext
    {
        public DbSet<CreateCADbMetric> Metrics => Set<CreateCADbMetric>();
        public DbSet<CreateCADbAggregate> HourlyMetrics => Set<CreateCADbAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateCADbMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CreateCADbAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CreateCADbAggregate, CreateCADbMetric>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Create_ContinuousAggregate_In_Database()
    {
        await using CreateCADbContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool exists = await ContinuousAggregateExistsAsync(context, "hourly_metrics");
        Assert.True(exists);
    }

    #endregion

    #region Migration_Should_Generate_AlterContinuousAggregate_When_ChunkInterval_Changes

    private class AlterCAMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AlterCAAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AlterCAInitialContext(string connectionString) : DbContext
    {
        public DbSet<AlterCAMetric> Metrics => Set<AlterCAMetric>();
        public DbSet<AlterCAAggregate> HourlyMetrics => Set<AlterCAAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterCAMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AlterCAAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AlterCAAggregate, AlterCAMetric>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class AlterCAModifiedContext(string connectionString) : DbContext
    {
        public DbSet<AlterCAMetric> Metrics => Set<AlterCAMetric>();
        public DbSet<AlterCAAggregate> HourlyMetrics => Set<AlterCAAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterCAMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AlterCAAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AlterCAAggregate, AlterCAMetric>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp,
                        chunkInterval: "30 days") // <-- Changed from default "7 days"
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Generate_AlterContinuousAggregate_When_ChunkInterval_Changes()
    {
        await using AlterCAInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await using AlterCAModifiedContext modifiedContext = new(_connectionString!);
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initialContext, modifiedContext);

        AlterContinuousAggregateOperation? alterCA = operations
            .OfType<AlterContinuousAggregateOperation>()
            .FirstOrDefault();

        Assert.NotNull(alterCA);
        Assert.Equal("30 days", alterCA.ChunkInterval);
    }

    #endregion

    #region Migration_Should_Drop_And_Recreate_ContinuousAggregate_When_Structure_Changes

    private class StructuralChangeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StructuralChangeAggregateInitial
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class StructuralChangeAggregateModified
    {
        public DateTime TimeBucket { get; set; }
        public double MaxValue { get; set; }
    }

    private class StructuralChangeInitialContext(string connectionString) : DbContext
    {
        public DbSet<StructuralChangeMetric> Metrics => Set<StructuralChangeMetric>();
        public DbSet<StructuralChangeAggregateInitial> HourlyMetrics => Set<StructuralChangeAggregateInitial>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StructuralChangeMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<StructuralChangeAggregateInitial>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<StructuralChangeAggregateInitial, StructuralChangeMetric>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class StructuralChangeModifiedContext(string connectionString) : DbContext
    {
        public DbSet<StructuralChangeMetric> Metrics => Set<StructuralChangeMetric>();
        public DbSet<StructuralChangeAggregateModified> HourlyMetrics => Set<StructuralChangeAggregateModified>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StructuralChangeMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<StructuralChangeAggregateModified>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<StructuralChangeAggregateModified, StructuralChangeMetric>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max); // <-- Changed from Avg
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Drop_And_Recreate_ContinuousAggregate_When_Structure_Changes()
    {
        await using StructuralChangeInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await using StructuralChangeModifiedContext modifiedContext = new(_connectionString!);
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initialContext, modifiedContext);

        Assert.Contains(operations, op => op is DropContinuousAggregateOperation);
        Assert.Contains(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion

    #region Migration_Should_Generate_AddReorderPolicy_Operation

    private class GenerateReorderPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class GenerateReorderPolicyContext(string connectionString) : DbContext
    {
        public DbSet<GenerateReorderPolicyMetric> Metrics => Set<GenerateReorderPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GenerateReorderPolicyMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Generate_AddReorderPolicy_Operation()
    {
        await using GenerateReorderPolicyContext context = new(_connectionString!);

        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        AddReorderPolicyOperation? addPolicy = operations
            .OfType<AddReorderPolicyOperation>()
            .FirstOrDefault();

        Assert.NotNull(addPolicy);
        Assert.Equal("Metrics", addPolicy.TableName);
        Assert.Equal("metrics_time_idx", addPolicy.IndexName);
    }

    #endregion

    #region Migration_Should_Create_ReorderPolicy_In_Database

    private class CreateReorderPolicyDbMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateReorderPolicyDbContext(string connectionString) : DbContext
    {
        public DbSet<CreateReorderPolicyDbMetric> Metrics => Set<CreateReorderPolicyDbMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreateReorderPolicyDbMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Create_ReorderPolicy_In_Database()
    {
        await using CreateReorderPolicyDbContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasReorderPolicyAsync(context, "Metrics");
        Assert.True(hasPolicy);
    }

    #endregion

    #region Migration_Should_Generate_AlterReorderPolicy_When_Schedule_Changes

    private class AlterReorderPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AlterReorderPolicyInitialContext(string connectionString) : DbContext
    {
        public DbSet<AlterReorderPolicyMetric> Metrics => Set<AlterReorderPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterReorderPolicyMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class AlterReorderPolicyModifiedContext(string connectionString) : DbContext
    {
        public DbSet<AlterReorderPolicyMetric> Metrics => Set<AlterReorderPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterReorderPolicyMetric>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("metrics_time_idx", scheduleInterval: "12:00:00"); // <-- Changed from "1 day"
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public async Task Migration_Should_Generate_AlterReorderPolicy_When_Schedule_Changes()
    {
        await using AlterReorderPolicyInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        await using AlterReorderPolicyModifiedContext modifiedContext = new(_connectionString!);
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initialContext, modifiedContext);

        AlterReorderPolicyOperation? alterPolicy = operations
            .OfType<AlterReorderPolicyOperation>()
            .FirstOrDefault();

        Assert.NotNull(alterPolicy);
        Assert.Equal("1 day", alterPolicy.OldScheduleInterval);
        Assert.Equal("12:00:00", alterPolicy.ScheduleInterval);
    }

    #endregion

    #region Helper Methods

    private static async Task<bool> IsHypertableAsync(DbContext context, string tableName)
    {
        await using NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) > 0
            FROM timescaledb_information.hypertables
            WHERE hypertable_name = @tableName";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool b && b;
    }

    private static async Task<string> GetChunkIntervalAsync(DbContext context, string tableName)
    {
        await using NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT time_interval::text
            FROM timescaledb_information.dimensions
            WHERE hypertable_name = @tableName
              AND dimension_type = 'Time'
            LIMIT 1";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result?.ToString() ?? string.Empty;
    }

    private static async Task<bool> ContinuousAggregateExistsAsync(DbContext context, string viewName)
    {
        await using NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) > 0
            FROM timescaledb_information.continuous_aggregates
            WHERE view_name = @viewName";
        command.Parameters.AddWithValue("viewName", viewName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool b && b;
    }

    private static async Task<bool> HasReorderPolicyAsync(DbContext context, string tableName)
    {
        await using NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) > 0
            FROM timescaledb_information.jobs j
            JOIN timescaledb_information.hypertables h ON j.hypertable_name = h.hypertable_name
            WHERE h.hypertable_name = @tableName
              AND j.proc_name = 'policy_reorder'";
        command.Parameters.AddWithValue("tableName", tableName);

        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool b && b;
    }

    #endregion
}

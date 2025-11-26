using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class ReorderPolicyIntegrationTests : IAsyncLifetime
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

    #region Helper Methods

    private static async Task<bool> HasReorderPolicyAsync(DbContext context, string tableName)
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
            FROM timescaledb_information.jobs
            WHERE application_name LIKE 'Reorder Policy%'
              AND hypertable_name = @tableName;
        ";
        command.Parameters.AddWithValue("tableName", tableName);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is bool boolResult && boolResult;
    }

    private static async Task<int> GetReorderPolicyJobIdAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id
            FROM timescaledb_information.jobs
            WHERE application_name LIKE 'Reorder Policy%'
              AND hypertable_name = @tableName
            LIMIT 1;
        ";
        command.Parameters.AddWithValue("tableName", tableName);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is int jobId ? jobId : 0;
    }

    private static async Task<TimeSpan> GetScheduleIntervalAsync(DbContext context, int jobId)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT schedule_interval
            FROM timescaledb_information.jobs
            WHERE job_id = @jobId;
        ";
        command.Parameters.AddWithValue("jobId", jobId);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is TimeSpan interval ? interval : TimeSpan.Zero;
    }

    private static async Task<TimeSpan> GetMaxRuntimeAsync(DbContext context, int jobId)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT max_runtime
            FROM timescaledb_information.jobs
            WHERE job_id = @jobId;
        ";
        command.Parameters.AddWithValue("jobId", jobId);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is TimeSpan maxRuntime ? maxRuntime : TimeSpan.Zero;
    }

    private static async Task<int> GetMaxRetriesAsync(DbContext context, int jobId)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT max_retries
            FROM timescaledb_information.jobs
            WHERE job_id = @jobId;
        ";
        command.Parameters.AddWithValue("jobId", jobId);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is int maxRetries ? maxRetries : 0;
    }

    private static async Task<TimeSpan> GetRetryPeriodAsync(DbContext context, int jobId)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT retry_period
            FROM timescaledb_information.jobs
            WHERE job_id = @jobId;
        ";
        command.Parameters.AddWithValue("jobId", jobId);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is TimeSpan retryPeriod ? retryPeriod : TimeSpan.Zero;
    }

    #endregion

    #region Should_Create_ReorderPolicy_WithMinimalConfig

    private class MinimalConfigMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class MinimalConfigContext(string connectionString) : DbContext
    {
        public DbSet<MinimalConfigMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalConfigMetric>(entity =>
            {
                entity.ToTable("metrics_minimal_config");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy("metrics_minimal_config_time_idx");
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_minimal_config_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Create_ReorderPolicy_WithMinimalConfig()
    {
        await using MinimalConfigContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        bool hasPolicy = await HasReorderPolicyAsync(context, "metrics_minimal_config");
        Assert.True(hasPolicy);

        int jobId = await GetReorderPolicyJobIdAsync(context, "metrics_minimal_config");
        Assert.True(jobId > 0);
    }

    #endregion

    #region Should_Create_ReorderPolicy_WithAllOptions

    private class AllOptionsMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
        public string? SensorId { get; set; }
    }

    private class AllOptionsContext(string connectionString) : DbContext
    {
        public DbSet<AllOptionsMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllOptionsMetric>(entity =>
            {
                entity.ToTable("metrics_all_options");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_all_options_time_idx",
                    initialStart: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    scheduleInterval: "6 hours",
                    maxRuntime: "00:30:00",
                    maxRetries: 3,
                    retryPeriod: "00:10:00"
                );
                entity.HasIndex(e => new { e.Time, e.Id, e.SensorId })
                      .HasDatabaseName("metrics_all_options_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Create_ReorderPolicy_WithAllOptions()
    {
        await using AllOptionsContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        bool hasPolicy = await HasReorderPolicyAsync(context, "metrics_all_options");
        Assert.True(hasPolicy);

        int jobId = await GetReorderPolicyJobIdAsync(context, "metrics_all_options");
        Assert.True(jobId > 0);

        TimeSpan scheduleInterval = await GetScheduleIntervalAsync(context, jobId);
        TimeSpan maxRuntime = await GetMaxRuntimeAsync(context, jobId);
        int maxRetries = await GetMaxRetriesAsync(context, jobId);
        TimeSpan retryPeriod = await GetRetryPeriodAsync(context, jobId);

        Assert.Equal(TimeSpan.FromHours(6), scheduleInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), maxRuntime);
        Assert.Equal(3, maxRetries);
        Assert.Equal(TimeSpan.FromMinutes(10), retryPeriod);
    }

    #endregion

    #region Should_Create_ReorderPolicy_WithCustomScheduleInterval

    private class CustomScheduleMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CustomScheduleContext(string connectionString) : DbContext
    {
        public DbSet<CustomScheduleMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomScheduleMetric>(entity =>
            {
                entity.ToTable("metrics_custom_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_custom_schedule_time_idx",
                    scheduleInterval: "1 day"
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_custom_schedule_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Create_ReorderPolicy_WithCustomScheduleInterval()
    {
        await using CustomScheduleContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(context, "metrics_custom_schedule");
        TimeSpan scheduleInterval = await GetScheduleIntervalAsync(context, jobId);

        Assert.Equal(TimeSpan.FromDays(1), scheduleInterval);
    }

    #endregion

    #region Should_Alter_ReorderPolicy_ScheduleInterval

    private class ScheduleIntervalMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialScheduleIntervalContext(string connectionString) : DbContext
    {
        public DbSet<ScheduleIntervalMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalMetric>(entity =>
            {
                entity.ToTable("metrics_schedule_interval");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_schedule_interval_time_idx",
                    scheduleInterval: "1 day"
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_schedule_interval_time_idx");
            });
        }
    }

    private class ModifiedScheduleIntervalContext(string connectionString) : DbContext
    {
        public DbSet<ScheduleIntervalMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalMetric>(entity =>
            {
                entity.ToTable("metrics_schedule_interval");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_schedule_interval_time_idx",
                    scheduleInterval: "12 hours"  // <-- Changed from "1 day"
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_schedule_interval_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_ReorderPolicy_ScheduleInterval()
    {
        await using InitialScheduleIntervalContext initialContext = new(_connectionString!);
        await initialContext.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(initialContext, "metrics_schedule_interval");
        TimeSpan initialSchedule = await GetScheduleIntervalAsync(initialContext, jobId);
        Assert.Equal(TimeSpan.FromDays(1), initialSchedule);

        await using ModifiedScheduleIntervalContext modifiedContext = new(_connectionString!);

        IMigrationsModelDiffer modelDiffer = modifiedContext.GetService<IMigrationsModelDiffer>();
        IMigrationsSqlGenerator sqlGenerator = modifiedContext.GetService<IMigrationsSqlGenerator>();

        IReadOnlyList<MigrationOperation> operations = modelDiffer.GetDifferences(
            initialContext.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            modifiedContext.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, modifiedContext.Model);

        foreach (MigrationCommand command in commands)
        {
            await modifiedContext.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        TimeSpan newSchedule = await GetScheduleIntervalAsync(modifiedContext, jobId);
        Assert.Equal(TimeSpan.FromHours(12), newSchedule);
    }

    #endregion

    #region Should_Alter_ReorderPolicy_MaxRuntime

    private class MaxRuntimeMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialMaxRuntimeContext(string connectionString) : DbContext
    {
        public DbSet<MaxRuntimeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeMetric>(entity =>
            {
                entity.ToTable("metrics_max_runtime");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_max_runtime_time_idx",
                    maxRuntime: "00:30:00"
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_max_runtime_time_idx");
            });
        }
    }

    private class ModifiedMaxRuntimeContext(string connectionString) : DbContext
    {
        public DbSet<MaxRuntimeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeMetric>(entity =>
            {
                entity.ToTable("metrics_max_runtime");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_max_runtime_time_idx",
                    maxRuntime: "00:15:00"  // <-- Changed from "00:30:00"
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_max_runtime_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_ReorderPolicy_MaxRuntime()
    {
        await using InitialMaxRuntimeContext initialContext = new(_connectionString!);
        await initialContext.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(initialContext, "metrics_max_runtime");

        await using ModifiedMaxRuntimeContext modifiedContext = new(_connectionString!);

        IMigrationsModelDiffer modelDiffer = modifiedContext.GetService<IMigrationsModelDiffer>();
        IMigrationsSqlGenerator sqlGenerator = modifiedContext.GetService<IMigrationsSqlGenerator>();

        IReadOnlyList<MigrationOperation> operations = modelDiffer.GetDifferences(
            initialContext.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            modifiedContext.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, modifiedContext.Model);

        foreach (MigrationCommand command in commands)
        {
            await modifiedContext.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        TimeSpan maxRuntime = await GetMaxRuntimeAsync(modifiedContext, jobId);
        Assert.Equal(TimeSpan.FromMinutes(15), maxRuntime);
    }

    #endregion

    #region Should_Alter_ReorderPolicy_MaxRetries

    private class MaxRetriesMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialMaxRetriesContext(string connectionString) : DbContext
    {
        public DbSet<MaxRetriesMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesMetric>(entity =>
            {
                entity.ToTable("metrics_max_retries");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_max_retries_time_idx",
                    maxRetries: 3
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_max_retries_time_idx");
            });
        }
    }

    private class ModifiedMaxRetriesContext(string connectionString) : DbContext
    {
        public DbSet<MaxRetriesMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesMetric>(entity =>
            {
                entity.ToTable("metrics_max_retries");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_max_retries_time_idx",
                    maxRetries: 5  // <-- Changed from 3
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_max_retries_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_ReorderPolicy_MaxRetries()
    {
        await using InitialMaxRetriesContext initialContext = new(_connectionString!);
        await initialContext.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(initialContext, "metrics_max_retries");

        await using ModifiedMaxRetriesContext modifiedContext = new(_connectionString!);

        IMigrationsModelDiffer modelDiffer = modifiedContext.GetService<IMigrationsModelDiffer>();
        IMigrationsSqlGenerator sqlGenerator = modifiedContext.GetService<IMigrationsSqlGenerator>();

        IReadOnlyList<MigrationOperation> operations = modelDiffer.GetDifferences(
            initialContext.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            modifiedContext.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, modifiedContext.Model);

        foreach (MigrationCommand command in commands)
        {
            await modifiedContext.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        int maxRetries = await GetMaxRetriesAsync(modifiedContext, jobId);
        Assert.Equal(5, maxRetries);
    }

    #endregion

    #region Should_Alter_ReorderPolicy_MultipleParameters

    private class MultipleParamsMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialMultipleParamsContext(string connectionString) : DbContext
    {
        public DbSet<MultipleParamsMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleParamsMetric>(entity =>
            {
                entity.ToTable("metrics_multiple_params");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_multiple_params_time_idx",
                    scheduleInterval: "1 day",
                    maxRuntime: "00:30:00",
                    maxRetries: 3
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_multiple_params_time_idx");
            });
        }
    }

    private class ModifiedMultipleParamsContext(string connectionString) : DbContext
    {
        public DbSet<MultipleParamsMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleParamsMetric>(entity =>
            {
                entity.ToTable("metrics_multiple_params");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_multiple_params_time_idx",
                    scheduleInterval: "12 hours",  // <-- Changed from "1 day"
                    maxRuntime: "00:15:00",  // <-- Changed from "00:30:00"
                    maxRetries: 5  // <-- Changed from 3
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_multiple_params_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_ReorderPolicy_MultipleParameters()
    {
        await using InitialMultipleParamsContext initialContext = new(_connectionString!);
        await initialContext.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(initialContext, "metrics_multiple_params");

        await using ModifiedMultipleParamsContext modifiedContext = new(_connectionString!);

        IMigrationsModelDiffer modelDiffer = modifiedContext.GetService<IMigrationsModelDiffer>();
        IMigrationsSqlGenerator sqlGenerator = modifiedContext.GetService<IMigrationsSqlGenerator>();

        IReadOnlyList<MigrationOperation> operations = modelDiffer.GetDifferences(
            initialContext.GetService<IDesignTimeModel>().Model.GetRelationalModel(),
            modifiedContext.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, modifiedContext.Model);

        foreach (MigrationCommand command in commands)
        {
            await modifiedContext.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        TimeSpan scheduleInterval = await GetScheduleIntervalAsync(modifiedContext, jobId);
        TimeSpan maxRuntime = await GetMaxRuntimeAsync(modifiedContext, jobId);
        int maxRetries = await GetMaxRetriesAsync(modifiedContext, jobId);

        Assert.Equal(TimeSpan.FromHours(12), scheduleInterval);
        Assert.Equal(TimeSpan.FromMinutes(15), maxRuntime);
        Assert.Equal(5, maxRetries);
    }

    #endregion

    #region Should_Drop_ReorderPolicy

    private class DropPolicyMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class DropPolicyInitialContext(string connectionString) : DbContext
    {
        public DbSet<DropPolicyMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropPolicyMetric>(entity =>
            {
                entity.ToTable("metrics_drop_policy");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy("metrics_drop_policy_time_idx");
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_drop_policy_time_idx");
            });
        }
    }

    private class DropPolicyRemovedContext(string connectionString) : DbContext
    {
        public DbSet<DropPolicyMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropPolicyMetric>(entity =>
            {
                entity.ToTable("metrics_drop_policy");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                // <-- Reorder policy removed
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_drop_policy_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Drop_ReorderPolicy()
    {
        await using DropPolicyInitialContext initialContext = new(_connectionString!);
        await initialContext.Database.EnsureCreatedAsync();

        IRelationalModel sourceRelationalModel = initialContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        bool hasPolicy = await HasReorderPolicyAsync(initialContext, "metrics_drop_policy");
        Assert.True(hasPolicy);

        await using DropPolicyRemovedContext removedContext = new(_connectionString!);

        IMigrationsModelDiffer modelDiffer = removedContext.GetService<IMigrationsModelDiffer>();
        IMigrationsSqlGenerator sqlGenerator = removedContext.GetService<IMigrationsSqlGenerator>();

        IRelationalModel targetModel = removedContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        IReadOnlyList<MigrationOperation> operations = modelDiffer.GetDifferences(sourceRelationalModel, targetModel);

        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, removedContext.Model);

        foreach (MigrationCommand command in commands)
        {
            await removedContext.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        hasPolicy = await HasReorderPolicyAsync(removedContext, "metrics_drop_policy");
        Assert.False(hasPolicy);
    }

    #endregion

    #region Should_Create_ReorderPolicy_OnExistingHypertable

    private class ExistingHypertableMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ExistingHypertableContext(string connectionString) : DbContext
    {
        public DbSet<ExistingHypertableMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExistingHypertableMetric>(entity =>
            {
                entity.ToTable("metrics_existing_hypertable");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy("metrics_existing_hypertable_time_idx");
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_existing_hypertable_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Create_ReorderPolicy_OnExistingHypertable()
    {
        await using ExistingHypertableContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        bool hasPolicy = await HasReorderPolicyAsync(context, "metrics_existing_hypertable");
        Assert.True(hasPolicy);
    }

    #endregion

    #region Should_Query_TimescaleDB_Jobs_View

    private class JobsViewMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
        public string? SensorId { get; set; }
    }

    private class JobsViewContext(string connectionString) : DbContext
    {
        public DbSet<JobsViewMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobsViewMetric>(entity =>
            {
                entity.ToTable("metrics_jobs_view");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_jobs_view_time_idx",
                    initialStart: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    scheduleInterval: "6 hours",
                    maxRuntime: "00:30:00",
                    maxRetries: 3,
                    retryPeriod: "00:10:00"
                );
                entity.HasIndex(e => new { e.Time, e.Id, e.SensorId })
                      .HasDatabaseName("metrics_jobs_view_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Query_TimescaleDB_Jobs_View()
    {
        await using JobsViewContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        await using NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT application_name, hypertable_name, schedule_interval, max_runtime, max_retries, retry_period
            FROM timescaledb_information.jobs
            WHERE application_name LIKE 'Reorder Policy%'
              AND hypertable_name = 'metrics_jobs_view';
        ";

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        string applicationName = reader.GetString(0);
        string hypertableName = reader.GetString(1);
        TimeSpan scheduleInterval = reader.GetTimeSpan(2);
        TimeSpan maxRuntime = reader.GetTimeSpan(3);
        int maxRetries = reader.GetInt32(4);
        TimeSpan retryPeriod = reader.GetTimeSpan(5);

        Assert.Contains("Reorder Policy", applicationName);
        Assert.Equal("metrics_jobs_view", hypertableName);
        Assert.Equal(TimeSpan.FromHours(6), scheduleInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), maxRuntime);
        Assert.Equal(3, maxRetries);
        Assert.Equal(TimeSpan.FromMinutes(10), retryPeriod);
    }

    #endregion

    #region Should_Handle_UnlimitedRetries

    private class UnlimitedRetriesMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class UnlimitedRetriesContext(string connectionString) : DbContext
    {
        public DbSet<UnlimitedRetriesMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnlimitedRetriesMetric>(entity =>
            {
                entity.ToTable("metrics_unlimited_retries");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_unlimited_retries_time_idx",
                    maxRetries: -1
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_unlimited_retries_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_UnlimitedRetries()
    {
        await using UnlimitedRetriesContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(context, "metrics_unlimited_retries");
        int maxRetries = await GetMaxRetriesAsync(context, jobId);

        Assert.Equal(-1, maxRetries);
    }

    #endregion

    #region Should_Handle_ZeroMaxRuntime

    private class ZeroMaxRuntimeMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ZeroMaxRuntimeContext(string connectionString) : DbContext
    {
        public DbSet<ZeroMaxRuntimeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ZeroMaxRuntimeMetric>(entity =>
            {
                entity.ToTable("metrics_zero_max_runtime");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithReorderPolicy(
                    indexName: "metrics_zero_max_runtime_time_idx",
                    maxRuntime: "00:00:00"
                );
                entity.HasIndex(e => new { e.Time, e.Id })
                      .HasDatabaseName("metrics_zero_max_runtime_time_idx");
            });
        }
    }

    [Fact]
    public async Task Should_Handle_ZeroMaxRuntime()
    {
        await using ZeroMaxRuntimeContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync();

        int jobId = await GetReorderPolicyJobIdAsync(context, "metrics_zero_max_runtime");
        TimeSpan maxRuntime = await GetMaxRuntimeAsync(context, jobId);

        Assert.Equal(TimeSpan.Zero, maxRuntime);
    }

    #endregion
}

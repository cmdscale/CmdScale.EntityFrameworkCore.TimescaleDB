using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class RetentionPolicyIntegrationTests : MigrationTestBase, IAsyncLifetime
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

    #region Helper Methods

    private static async Task<bool> HasRetentionPolicyAsync(DbContext context, string tableName)
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
            WHERE proc_name = 'policy_retention'
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

    private static async Task<int> GetRetentionPolicyJobIdAsync(DbContext context, string tableName)
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
            WHERE proc_name = 'policy_retention'
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

    private static async Task<string?> GetRetentionPolicyConfigAsync(DbContext context, string tableName)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT config::text
            FROM timescaledb_information.jobs
            WHERE proc_name = 'policy_retention'
              AND hypertable_name = @tableName
            LIMIT 1;
        ";
        command.Parameters.AddWithValue("tableName", tableName);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result as string;
    }

    private static async Task<DateTime?> GetJobInitialStartAsync(DbContext context, int jobId)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        bool wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = @"
            SELECT initial_start
            FROM timescaledb_information.jobs
            WHERE job_id = @jobId;
        ";
        command.Parameters.AddWithValue("jobId", jobId);
        object? result = await command.ExecuteScalarAsync();

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }

        return result is DateTime dt ? dt : null;
    }

    private static async Task<int> GetJobMaxRetriesAsync(DbContext context, int jobId)
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

        return result is int maxRetries ? maxRetries : -1;
    }

    #endregion

    #region Should_Create_RetentionPolicy_WithDropAfter

    private class DropAfterMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class DropAfterContext(string connectionString) : DbContext
    {
        public DbSet<DropAfterMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropAfterMetric>(entity =>
            {
                entity.ToTable("retention_drop_after");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Create_RetentionPolicy_WithDropAfter()
    {
        await using DropAfterContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasRetentionPolicyAsync(context, "retention_drop_after");
        Assert.True(hasPolicy);

        int jobId = await GetRetentionPolicyJobIdAsync(context, "retention_drop_after");
        Assert.True(jobId > 0);

        string? config = await GetRetentionPolicyConfigAsync(context, "retention_drop_after");
        Assert.NotNull(config);
        Assert.Contains("drop_after", config);
    }

    #endregion

    #region Should_Create_RetentionPolicy_WithDropCreatedBefore

    private class DropCreatedBeforeMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class DropCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<DropCreatedBeforeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropCreatedBeforeMetric>(entity =>
            {
                entity.ToTable("retention_drop_created_before");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropCreatedBefore: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Create_RetentionPolicy_WithDropCreatedBefore()
    {
        await using DropCreatedBeforeContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasRetentionPolicyAsync(context, "retention_drop_created_before");
        Assert.True(hasPolicy);

        string? config = await GetRetentionPolicyConfigAsync(context, "retention_drop_created_before");
        Assert.NotNull(config);
        Assert.Contains("drop_created_before", config);
    }

    #endregion

    #region Should_Alter_RetentionPolicy_ScheduleInterval

    private class AlterScheduleMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialAlterScheduleContext(string connectionString) : DbContext
    {
        public DbSet<AlterScheduleMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterScheduleMetric>(entity =>
            {
                entity.ToTable("retention_alter_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    scheduleInterval: "1 day"
                );
            });
        }
    }

    private class ModifiedAlterScheduleContext(string connectionString) : DbContext
    {
        public DbSet<AlterScheduleMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterScheduleMetric>(entity =>
            {
                entity.ToTable("retention_alter_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    scheduleInterval: "12 hours"  // <-- Changed from "1 day"
                );
            });
        }
    }

    [Fact]
    public async Task Should_Alter_RetentionPolicy_ScheduleInterval()
    {
        await using InitialAlterScheduleContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        int jobId = await GetRetentionPolicyJobIdAsync(initialContext, "retention_alter_schedule");
        TimeSpan initialSchedule = await GetScheduleIntervalAsync(initialContext, jobId);
        Assert.Equal(TimeSpan.FromDays(1), initialSchedule);

        await using ModifiedAlterScheduleContext modifiedContext = new(_connectionString!);

        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        TimeSpan newSchedule = await GetScheduleIntervalAsync(modifiedContext, jobId);
        Assert.Equal(TimeSpan.FromHours(12), newSchedule);
    }

    #endregion

    #region Should_Alter_RetentionPolicy_DropAfter

    private class AlterDropAfterMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialAlterDropAfterContext(string connectionString) : DbContext
    {
        public DbSet<AlterDropAfterMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterDropAfterMetric>(entity =>
            {
                entity.ToTable("retention_alter_drop_after");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class ModifiedAlterDropAfterContext(string connectionString) : DbContext
    {
        public DbSet<AlterDropAfterMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterDropAfterMetric>(entity =>
            {
                entity.ToTable("retention_alter_drop_after");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropAfter: "30 days");  // <-- Changed from "7 days"
            });
        }
    }

    [Fact]
    public async Task Should_Alter_RetentionPolicy_DropAfter()
    {
        await using InitialAlterDropAfterContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        int initialJobId = await GetRetentionPolicyJobIdAsync(initialContext, "retention_alter_drop_after");
        Assert.True(initialJobId > 0);

        await using ModifiedAlterDropAfterContext modifiedContext = new(_connectionString!);

        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        bool hasPolicy = await HasRetentionPolicyAsync(modifiedContext, "retention_alter_drop_after");
        Assert.True(hasPolicy);

        string? config = await GetRetentionPolicyConfigAsync(modifiedContext, "retention_alter_drop_after");
        Assert.NotNull(config);
        Assert.Contains("drop_after", config);
    }

    #endregion

    #region Should_Drop_RetentionPolicy

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
                entity.ToTable("retention_drop_policy");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropAfter: "7 days");
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
                entity.ToTable("retention_drop_policy");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                // <-- Retention policy removed
            });
        }
    }

    [Fact]
    public async Task Should_Drop_RetentionPolicy()
    {
        await using DropPolicyInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        bool hasPolicy = await HasRetentionPolicyAsync(initialContext, "retention_drop_policy");
        Assert.True(hasPolicy);

        await using DropPolicyRemovedContext removedContext = new(_connectionString!);

        await AlterDatabaseViaMigrationAsync(initialContext, removedContext);

        hasPolicy = await HasRetentionPolicyAsync(removedContext, "retention_drop_policy");
        Assert.False(hasPolicy);
    }

    #endregion

    #region Should_Create_RetentionPolicy_WithCustomScheduleInterval

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
                entity.ToTable("retention_custom_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(
                    dropAfter: "14 days",
                    scheduleInterval: "6 hours"
                );
            });
        }
    }

    [Fact]
    public async Task Should_Create_RetentionPolicy_WithCustomScheduleInterval()
    {
        await using CustomScheduleContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        int jobId = await GetRetentionPolicyJobIdAsync(context, "retention_custom_schedule");
        Assert.True(jobId > 0);

        TimeSpan scheduleInterval = await GetScheduleIntervalAsync(context, jobId);
        Assert.Equal(TimeSpan.FromHours(6), scheduleInterval);
    }

    #endregion

    #region Should_Alter_RetentionPolicy_DropAfter_To_DropCreatedBefore

    private class AlterDropAfterToDropCreatedBeforeMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialAlterDropAfterToDropCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<AlterDropAfterToDropCreatedBeforeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterDropAfterToDropCreatedBeforeMetric>(entity =>
            {
                entity.ToTable("retention_alter_da_to_dcb");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class ModifiedAlterDropAfterToDropCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<AlterDropAfterToDropCreatedBeforeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterDropAfterToDropCreatedBeforeMetric>(entity =>
            {
                entity.ToTable("retention_alter_da_to_dcb");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropCreatedBefore: "30 days");  // <-- Changed from dropAfter: "7 days"
            });
        }
    }

    //[Fact(Skip = "TimescaleDB bug #9446: alter_job fails when config contains drop_created_before instead of drop_after. " +
    //      "The generator's Alter recreation path emits alter_job to reapply job settings, which hits this bug.")]
    [Fact]
    public async Task Should_Alter_RetentionPolicy_DropAfter_To_DropCreatedBefore()
    {
        await using InitialAlterDropAfterToDropCreatedBeforeContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        bool hasPolicy = await HasRetentionPolicyAsync(initialContext, "retention_alter_da_to_dcb");
        Assert.True(hasPolicy);

        string? initialConfig = await GetRetentionPolicyConfigAsync(initialContext, "retention_alter_da_to_dcb");
        Assert.NotNull(initialConfig);
        Assert.Contains("drop_after", initialConfig);

        await using ModifiedAlterDropAfterToDropCreatedBeforeContext modifiedContext = new(_connectionString!);

        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        hasPolicy = await HasRetentionPolicyAsync(modifiedContext, "retention_alter_da_to_dcb");
        Assert.True(hasPolicy);

        string? modifiedConfig = await GetRetentionPolicyConfigAsync(modifiedContext, "retention_alter_da_to_dcb");
        Assert.NotNull(modifiedConfig);
        Assert.Contains("drop_created_before", modifiedConfig);
        Assert.DoesNotContain("drop_after", modifiedConfig);
    }

    #endregion

    #region Should_Create_RetentionPolicy_WithInitialStart

    private class InitialStartMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartContext(string connectionString) : DbContext
    {
        public DbSet<InitialStartMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartMetric>(entity =>
            {
                entity.ToTable("retention_initial_start");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                );
            });
        }
    }

    [Fact]
    public async Task Should_Create_RetentionPolicy_WithInitialStart()
    {
        await using InitialStartContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasRetentionPolicyAsync(context, "retention_initial_start");
        Assert.True(hasPolicy);

        int jobId = await GetRetentionPolicyJobIdAsync(context, "retention_initial_start");
        Assert.True(jobId > 0);

        DateTime? initialStart = await GetJobInitialStartAsync(context, jobId);
        Assert.NotNull(initialStart);
    }

    #endregion

    #region Should_Create_RetentionPolicy_WithFullJobSettings

    private class FullJobSettingsMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class FullJobSettingsContext(string connectionString) : DbContext
    {
        public DbSet<FullJobSettingsMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullJobSettingsMetric>(entity =>
            {
                entity.ToTable("retention_full_job_settings");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(
                    dropAfter: "14 days",
                    scheduleInterval: "6 hours",
                    maxRuntime: "1 hour",
                    maxRetries: 3,
                    retryPeriod: "10 minutes"
                );
            });
        }
    }

    [Fact]
    public async Task Should_Create_RetentionPolicy_WithFullJobSettings()
    {
        await using FullJobSettingsContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasRetentionPolicyAsync(context, "retention_full_job_settings");
        Assert.True(hasPolicy);

        int jobId = await GetRetentionPolicyJobIdAsync(context, "retention_full_job_settings");
        Assert.True(jobId > 0);

        TimeSpan scheduleInterval = await GetScheduleIntervalAsync(context, jobId);
        Assert.Equal(TimeSpan.FromHours(6), scheduleInterval);

        int maxRetries = await GetJobMaxRetriesAsync(context, jobId);
        Assert.Equal(3, maxRetries);
    }

    #endregion

    #region Should_Create_RetentionPolicy_ViaEnsureCreated

    private class EnsureCreatedMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class EnsureCreatedContext(string connectionString) : DbContext
    {
        public DbSet<EnsureCreatedMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EnsureCreatedMetric>(entity =>
            {
                entity.ToTable("retention_ensure_created");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Create_RetentionPolicy_ViaEnsureCreated()
    {
        await using EnsureCreatedContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        bool hasPolicy = await HasRetentionPolicyAsync(context, "retention_ensure_created");
        Assert.True(hasPolicy);

        int jobId = await GetRetentionPolicyJobIdAsync(context, "retention_ensure_created");
        Assert.True(jobId > 0);
    }

    #endregion
}

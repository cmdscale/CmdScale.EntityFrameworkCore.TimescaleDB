using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class CompressionPolicyIntegrationTests : MigrationTestBase, IAsyncLifetime
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

    private static async Task<bool> HasCompressionPolicyAsync(DbContext context, string tableName)
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
            WHERE proc_name IN ('policy_compression', 'policy_columnstore')
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

    private static async Task<int> GetCompressionPolicyJobIdAsync(DbContext context, string tableName)
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
            WHERE proc_name IN ('policy_compression', 'policy_columnstore')
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

    private static async Task<string?> GetCompressionPolicyConfigAsync(DbContext context, string tableName)
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
            WHERE proc_name IN ('policy_compression', 'policy_columnstore')
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

    #endregion

    #region Should_Create_CompressionPolicy_WithAfter

    private class CompressAfterMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CompressAfterContext(string connectionString) : DbContext
    {
        public DbSet<CompressAfterMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressAfterMetric>(entity =>
            {
                entity.ToTable("compression_after");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Create_CompressionPolicy_WithAfter()
    {
        await using CompressAfterContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasCompressionPolicyAsync(context, "compression_after");
        Assert.True(hasPolicy);

        string? config = await GetCompressionPolicyConfigAsync(context, "compression_after");
        Assert.NotNull(config);
        Assert.Contains("compress_after", config);
    }

    #endregion

    #region Should_Create_CompressionPolicy_WithCreatedBefore

    private class CompressCreatedBeforeMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CompressCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<CompressCreatedBeforeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressCreatedBeforeMetric>(entity =>
            {
                entity.ToTable("compression_created_before");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(createdBefore: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Create_CompressionPolicy_WithCreatedBefore()
    {
        await using CompressCreatedBeforeContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasCompressionPolicyAsync(context, "compression_created_before");
        Assert.True(hasPolicy);

        string? config = await GetCompressionPolicyConfigAsync(context, "compression_created_before");
        Assert.NotNull(config);
        Assert.Contains("compress_created_before", config);
    }

    #endregion

    #region Should_Create_CompressionPolicy_WithCustomScheduleInterval

    private class CompressCustomScheduleMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CompressCustomScheduleContext(string connectionString) : DbContext
    {
        public DbSet<CompressCustomScheduleMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressCustomScheduleMetric>(entity =>
            {
                entity.ToTable("compression_custom_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(
                    after: "14 days",
                    scheduleInterval: "6 hours"
                );
            });
        }
    }

    [Fact]
    public async Task Should_Create_CompressionPolicy_WithCustomScheduleInterval()
    {
        await using CompressCustomScheduleContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        int jobId = await GetCompressionPolicyJobIdAsync(context, "compression_custom_schedule");
        Assert.True(jobId > 0);

        TimeSpan scheduleInterval = await GetScheduleIntervalAsync(context, jobId);
        Assert.Equal(TimeSpan.FromHours(6), scheduleInterval);
    }

    #endregion

    #region Should_Create_CompressionPolicy_WithInitialStart

    private class CompressInitialStartMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CompressInitialStartContext(string connectionString) : DbContext
    {
        public DbSet<CompressInitialStartMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressInitialStartMetric>(entity =>
            {
                entity.ToTable("compression_initial_start");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(
                    after: "7 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                );
            });
        }
    }

    [Fact]
    public async Task Should_Create_CompressionPolicy_WithInitialStart()
    {
        await using CompressInitialStartContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);

        bool hasPolicy = await HasCompressionPolicyAsync(context, "compression_initial_start");
        Assert.True(hasPolicy);

        int jobId = await GetCompressionPolicyJobIdAsync(context, "compression_initial_start");
        Assert.True(jobId > 0);

        DateTime? initialStart = await GetJobInitialStartAsync(context, jobId);
        Assert.NotNull(initialStart);
    }

    #endregion

    #region Should_Alter_CompressionPolicy_After

    private class AlterAfterMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialAlterAfterContext(string connectionString) : DbContext
    {
        public DbSet<AlterAfterMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterAfterMetric>(entity =>
            {
                entity.ToTable("compression_alter_after");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class ModifiedAlterAfterContext(string connectionString) : DbContext
    {
        public DbSet<AlterAfterMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterAfterMetric>(entity =>
            {
                entity.ToTable("compression_alter_after");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(after: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_CompressionPolicy_After()
    {
        await using InitialAlterAfterContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        bool hasPolicy = await HasCompressionPolicyAsync(initialContext, "compression_alter_after");
        Assert.True(hasPolicy);

        await using ModifiedAlterAfterContext modifiedContext = new(_connectionString!);
        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        hasPolicy = await HasCompressionPolicyAsync(modifiedContext, "compression_alter_after");
        Assert.True(hasPolicy);

        string? config = await GetCompressionPolicyConfigAsync(modifiedContext, "compression_alter_after");
        Assert.NotNull(config);
        Assert.Contains("compress_after", config);
    }

    #endregion

    #region Should_Alter_CompressionPolicy_After_To_CreatedBefore

    private class AlterAfterToCreatedBeforeMetric
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class InitialAlterAfterToCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<AlterAfterToCreatedBeforeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterAfterToCreatedBeforeMetric>(entity =>
            {
                entity.ToTable("compression_alter_a_to_cb");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class ModifiedAlterAfterToCreatedBeforeContext(string connectionString) : DbContext
    {
        public DbSet<AlterAfterToCreatedBeforeMetric> Metrics { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterAfterToCreatedBeforeMetric>(entity =>
            {
                entity.ToTable("compression_alter_a_to_cb");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(createdBefore: "30 days");
            });
        }
    }

    [Fact]
    public async Task Should_Alter_CompressionPolicy_After_To_CreatedBefore()
    {
        await using InitialAlterAfterToCreatedBeforeContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        string? initialConfig = await GetCompressionPolicyConfigAsync(initialContext, "compression_alter_a_to_cb");
        Assert.NotNull(initialConfig);
        Assert.Contains("compress_after", initialConfig);

        await using ModifiedAlterAfterToCreatedBeforeContext modifiedContext = new(_connectionString!);
        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        bool hasPolicy = await HasCompressionPolicyAsync(modifiedContext, "compression_alter_a_to_cb");
        Assert.True(hasPolicy);

        string? modifiedConfig = await GetCompressionPolicyConfigAsync(modifiedContext, "compression_alter_a_to_cb");
        Assert.NotNull(modifiedConfig);
        Assert.Contains("compress_created_before", modifiedConfig);
        Assert.DoesNotContain("compress_after", modifiedConfig);
    }

    #endregion

    #region Should_Alter_CompressionPolicy_ScheduleInterval

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
                entity.ToTable("compression_alter_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(
                    after: "7 days",
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
                entity.ToTable("compression_alter_schedule");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(
                    after: "7 days",
                    scheduleInterval: "12 hours"
                );
            });
        }
    }

    [Fact]
    public async Task Should_Alter_CompressionPolicy_ScheduleInterval()
    {
        await using InitialAlterScheduleContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        int jobId = await GetCompressionPolicyJobIdAsync(initialContext, "compression_alter_schedule");
        TimeSpan initialSchedule = await GetScheduleIntervalAsync(initialContext, jobId);
        Assert.Equal(TimeSpan.FromDays(1), initialSchedule);

        await using ModifiedAlterScheduleContext modifiedContext = new(_connectionString!);
        await AlterDatabaseViaMigrationAsync(initialContext, modifiedContext);

        bool hasPolicy = await HasCompressionPolicyAsync(modifiedContext, "compression_alter_schedule");
        Assert.True(hasPolicy);
    }

    #endregion

    #region Should_Drop_CompressionPolicy

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
                entity.ToTable("compression_drop_policy");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(after: "7 days");
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
                entity.ToTable("compression_drop_policy");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
            });
        }
    }

    [Fact]
    public async Task Should_Drop_CompressionPolicy()
    {
        await using DropPolicyInitialContext initialContext = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(initialContext);

        bool hasPolicy = await HasCompressionPolicyAsync(initialContext, "compression_drop_policy");
        Assert.True(hasPolicy);

        await using DropPolicyRemovedContext removedContext = new(_connectionString!);
        await AlterDatabaseViaMigrationAsync(initialContext, removedContext);

        hasPolicy = await HasCompressionPolicyAsync(removedContext, "compression_drop_policy");
        Assert.False(hasPolicy);
    }

    #endregion

    #region Should_Create_CompressionPolicy_ViaEnsureCreated

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
                entity.ToTable("compression_ensure_created");
                entity.HasKey(e => new { e.Time, e.Id });
                entity.IsHypertable(e => e.Time)
                      .WithCompressionOrderBy(s => s.By(x => x.Time));
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public async Task Should_Create_CompressionPolicy_ViaEnsureCreated()
    {
        await using EnsureCreatedContext context = new(_connectionString!);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        bool hasPolicy = await HasCompressionPolicyAsync(context, "compression_ensure_created");
        Assert.True(hasPolicy);

        int jobId = await GetCompressionPolicyJobIdAsync(context, "compression_ensure_created");
        Assert.True(jobId > 0);
    }

    #endregion
}

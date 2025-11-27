using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

/// <summary>
/// Tests that verify ReorderPolicyModelExtractor correctly extracts reorder policy configurations
/// from EF Core models and converts them to AddReorderPolicyOperation objects.
/// </summary>
public class ReorderPolicyModelExtractorTests
{
    private static IRelationalModel GetRelationalModel(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.GetRelationalModel();
    }

    #region Should_Extract_Minimal_ReorderPolicy

    private class MinimalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalReorderPolicyContext : DbContext
    {
        public DbSet<MinimalMetric> Metrics => Set<MinimalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Extract_Minimal_ReorderPolicy()
    {
        using MinimalReorderPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        AddReorderPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("metrics_time_idx", operation.IndexName);
        Assert.Null(operation.InitialStart);
        Assert.Equal(DefaultValues.ReorderPolicyScheduleInterval, operation.ScheduleInterval);
        Assert.Equal(DefaultValues.ReorderPolicyMaxRuntime, operation.MaxRuntime);
        Assert.Equal(DefaultValues.ReorderPolicyMaxRetries, operation.MaxRetries);
        Assert.Equal(DefaultValues.ReorderPolicyRetryPeriod, operation.RetryPeriod);
    }

    #endregion

    #region Should_Return_Empty_When_No_ReorderPolicies

    private class NoReorderMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoReorderPolicyContext : DbContext
    {
        public DbSet<NoReorderMetric> Metrics => Set<NoReorderMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoReorderMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_ReorderPolicies()
    {
        using NoReorderPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Return_Empty_When_RelationalModel_Is_Null

    [Fact]
    public void Should_Return_Empty_When_RelationalModel_Is_Null()
    {
        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(null)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Extract_InitialStart

    private class InitialStartMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartContext : DbContext
    {
        public DbSet<InitialStartMetric> Metrics => Set<InitialStartMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                      );
            });
        }
    }

    [Fact]
    public void Should_Extract_InitialStart()
    {
        using InitialStartContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        DateTime? initialStart = operations[0].InitialStart;
        Assert.NotNull(initialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, initialStart.Value);
    }

    #endregion

    #region Should_Have_Null_InitialStart_When_Not_Specified

    private class NullInitialStartMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NullInitialStartContext : DbContext
    {
        public DbSet<NullInitialStartMetric> Metrics => Set<NullInitialStartMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullInitialStartMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Have_Null_InitialStart_When_Not_Specified()
    {
        using NullInitialStartContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Null(operations[0].InitialStart);
    }

    #endregion

    #region Should_Extract_ScheduleInterval

    private class ScheduleIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalContext : DbContext
    {
        public DbSet<ScheduleIntervalMetric> Metrics => Set<ScheduleIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          scheduleInterval: "12:00:00"
                      );
            });
        }
    }

    [Fact]
    public void Should_Extract_ScheduleInterval()
    {
        using ScheduleIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("12:00:00", operations[0].ScheduleInterval);
    }

    #endregion

    #region Should_Use_Default_ScheduleInterval_When_Not_Specified

    private class DefaultScheduleIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultScheduleIntervalContext : DbContext
    {
        public DbSet<DefaultScheduleIntervalMetric> Metrics => Set<DefaultScheduleIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultScheduleIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_ScheduleInterval_When_Not_Specified()
    {
        using DefaultScheduleIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.ReorderPolicyScheduleInterval, operations[0].ScheduleInterval);
    }

    #endregion

    #region Should_Extract_MaxRuntime

    private class MaxRuntimeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRuntimeContext : DbContext
    {
        public DbSet<MaxRuntimeMetric> Metrics => Set<MaxRuntimeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          maxRuntime: "01:00:00"
                      );
            });
        }
    }

    [Fact]
    public void Should_Extract_MaxRuntime()
    {
        using MaxRuntimeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("01:00:00", operations[0].MaxRuntime);
    }

    #endregion

    #region Should_Use_Default_MaxRuntime_When_Not_Specified

    private class DefaultMaxRuntimeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultMaxRuntimeContext : DbContext
    {
        public DbSet<DefaultMaxRuntimeMetric> Metrics => Set<DefaultMaxRuntimeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultMaxRuntimeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_MaxRuntime_When_Not_Specified()
    {
        using DefaultMaxRuntimeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.ReorderPolicyMaxRuntime, operations[0].MaxRuntime);
    }

    #endregion

    #region Should_Extract_MaxRetries

    private class MaxRetriesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRetriesContext : DbContext
    {
        public DbSet<MaxRetriesMetric> Metrics => Set<MaxRetriesMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          maxRetries: 5
                      );
            });
        }
    }

    [Fact]
    public void Should_Extract_MaxRetries()
    {
        using MaxRetriesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(5, operations[0].MaxRetries);
    }

    #endregion

    #region Should_Use_Default_MaxRetries_When_Not_Specified

    private class DefaultMaxRetriesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultMaxRetriesContext : DbContext
    {
        public DbSet<DefaultMaxRetriesMetric> Metrics => Set<DefaultMaxRetriesMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultMaxRetriesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_MaxRetries_When_Not_Specified()
    {
        using DefaultMaxRetriesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.ReorderPolicyMaxRetries, operations[0].MaxRetries);
    }

    #endregion

    #region Should_Extract_RetryPeriod

    private class RetryPeriodMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetryPeriodContext : DbContext
    {
        public DbSet<RetryPeriodMetric> Metrics => Set<RetryPeriodMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetryPeriodMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          retryPeriod: "00:10:00"
                      );
            });
        }
    }

    [Fact]
    public void Should_Extract_RetryPeriod()
    {
        using RetryPeriodContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("00:10:00", operations[0].RetryPeriod);
    }

    #endregion

    #region Should_Use_Default_RetryPeriod_When_Not_Specified

    private class DefaultRetryPeriodMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultRetryPeriodContext : DbContext
    {
        public DbSet<DefaultRetryPeriodMetric> Metrics => Set<DefaultRetryPeriodMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultRetryPeriodMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_RetryPeriod_When_Not_Specified()
    {
        using DefaultRetryPeriodContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.ReorderPolicyRetryPeriod, operations[0].RetryPeriod);
    }

    #endregion

    #region Should_Extract_Multiple_ReorderPolicies

    private class MultipleMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultipleEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultiplePoliciesContext : DbContext
    {
        public DbSet<MultipleMetric> Metrics => Set<MultipleMetric>();
        public DbSet<MultipleEvent> Events => Set<MultipleEvent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });

            modelBuilder.Entity<MultipleEvent>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Events");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("events_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_ReorderPolicies()
    {
        using MultiplePoliciesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Equal(2, operations.Count);
        Assert.Contains(operations, op => op.TableName == "Metrics");
        Assert.Contains(operations, op => op.TableName == "Events");
    }

    #endregion

    #region Should_Extract_Fully_Configured_ReorderPolicy

    private class FullyConfiguredMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullyConfiguredContext : DbContext
    {
        public DbSet<FullyConfiguredMetric> Metrics => Set<FullyConfiguredMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy(
                          indexName: "metrics_time_idx",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                          scheduleInterval: "06:00:00",
                          maxRuntime: "02:00:00",
                          maxRetries: 3,
                          retryPeriod: "00:15:00"
                      );
            });
        }
    }

    [Fact]
    public void Should_Extract_Fully_Configured_ReorderPolicy()
    {
        using FullyConfiguredContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        AddReorderPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("metrics_time_idx", operation.IndexName);
        Assert.NotNull(operation.InitialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, operation.InitialStart.Value);
        Assert.Equal("06:00:00", operation.ScheduleInterval);
        Assert.Equal("02:00:00", operation.MaxRuntime);
        Assert.Equal(3, operation.MaxRetries);
        Assert.Equal("00:15:00", operation.RetryPeriod);
    }

    #endregion

    #region Should_Extract_ReorderPolicy_From_Attribute

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_attr_idx")]
    private class ReorderPolicyAttributeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderPolicyAttributeContext : DbContext
    {
        public DbSet<ReorderPolicyAttributeMetric> Metrics => Set<ReorderPolicyAttributeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderPolicyAttributeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Extract_ReorderPolicy_From_Attribute()
    {
        using ReorderPolicyAttributeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        AddReorderPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("metrics_attr_idx", operation.IndexName);
    }

    #endregion

    #region Should_Extract_Fully_Configured_ReorderPolicy_From_Attribute

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_full_attr_idx",
        InitialStart = "2025-06-01T00:00:00Z",
        ScheduleInterval = "12:00:00",
        MaxRuntime = "01:30:00",
        MaxRetries = 5,
        RetryPeriod = "00:20:00")]
    private class FullyConfiguredReorderPolicyAttributeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullyConfiguredReorderPolicyAttributeContext : DbContext
    {
        public DbSet<FullyConfiguredReorderPolicyAttributeMetric> Metrics => Set<FullyConfiguredReorderPolicyAttributeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredReorderPolicyAttributeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Extract_Fully_Configured_ReorderPolicy_From_Attribute()
    {
        using FullyConfiguredReorderPolicyAttributeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        AddReorderPolicyOperation operation = operations[0];
        Assert.Equal("metrics_full_attr_idx", operation.IndexName);
        Assert.NotNull(operation.InitialStart);
        Assert.Equal("12:00:00", operation.ScheduleInterval);
        Assert.Equal("01:30:00", operation.MaxRuntime);
        Assert.Equal(5, operation.MaxRetries);
        Assert.Equal("00:20:00", operation.RetryPeriod);
    }

    #endregion

    #region Should_Extract_Custom_Schema

    private class CustomSchemaReorderMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomSchemaReorderPolicyContext : DbContext
    {
        public DbSet<CustomSchemaReorderMetric> Metrics => Set<CustomSchemaReorderMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomSchemaReorderMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics", "custom_schema");
                entity.IsHypertable(x => x.Timestamp)
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Extract_Custom_Schema()
    {
        using CustomSchemaReorderPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddReorderPolicyOperation> operations = [.. ReorderPolicyModelExtractor.GetReorderPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("custom_schema", operations[0].Schema);
    }

    #endregion
}

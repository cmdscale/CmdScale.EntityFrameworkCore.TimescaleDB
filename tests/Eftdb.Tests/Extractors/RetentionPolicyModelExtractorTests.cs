using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

/// <summary>
/// Tests that verify RetentionPolicyModelExtractor correctly extracts retention policy configurations
/// from EF Core models and converts them to AddRetentionPolicyOperation objects.
/// </summary>
public class RetentionPolicyModelExtractorTests
{
    private static IRelationalModel GetRelationalModel(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.GetRelationalModel();
    }

    #region Should_Extract_Minimal_RetentionPolicy_With_DropAfter

    private class MinimalDropAfterMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalDropAfterContext : DbContext
    {
        public DbSet<MinimalDropAfterMetric> Metrics => Set<MinimalDropAfterMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalDropAfterMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Minimal_RetentionPolicy_With_DropAfter()
    {
        using MinimalDropAfterContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        AddRetentionPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("7 days", operation.DropAfter);
        Assert.Null(operation.DropCreatedBefore);
        Assert.Null(operation.InitialStart);
        Assert.Equal(DefaultValues.RetentionPolicyScheduleInterval, operation.ScheduleInterval);
        Assert.Equal(DefaultValues.RetentionPolicyMaxRuntime, operation.MaxRuntime);
        Assert.Equal(DefaultValues.RetentionPolicyMaxRetries, operation.MaxRetries);
        Assert.Equal(DefaultValues.RetentionPolicyScheduleInterval, operation.RetryPeriod);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_With_DropCreatedBefore

    private class DropCreatedBeforeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropCreatedBeforeContext : DbContext
    {
        public DbSet<DropCreatedBeforeMetric> Metrics => Set<DropCreatedBeforeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropCreatedBeforeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropCreatedBefore: "30 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_RetentionPolicy_With_DropCreatedBefore()
    {
        using DropCreatedBeforeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        AddRetentionPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Null(operation.DropAfter);
        Assert.Equal("30 days", operation.DropCreatedBefore);
    }

    #endregion

    #region Should_Return_Empty_When_RelationalModel_Is_Null

    [Fact]
    public void Should_Return_Empty_When_RelationalModel_Is_Null()
    {
        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(null)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Return_Empty_When_No_RetentionPolicies

    private class NoRetentionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoRetentionPolicyContext : DbContext
    {
        public DbSet<NoRetentionMetric> Metrics => Set<NoRetentionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoRetentionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_RetentionPolicies()
    {
        using NoRetentionPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(
                          dropAfter: "7 days",
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

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Have_Null_InitialStart_When_Not_Specified()
    {
        using NullInitialStartContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(
                          dropAfter: "7 days",
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

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_ScheduleInterval_When_Not_Specified()
    {
        using DefaultScheduleIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.RetentionPolicyScheduleInterval, operations[0].ScheduleInterval);
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
                      .WithRetentionPolicy(
                          dropAfter: "7 days",
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

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_MaxRuntime_When_Not_Specified()
    {
        using DefaultMaxRuntimeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.RetentionPolicyMaxRuntime, operations[0].MaxRuntime);
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
                      .WithRetentionPolicy(
                          dropAfter: "7 days",
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

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_MaxRetries_When_Not_Specified()
    {
        using DefaultMaxRetriesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.RetentionPolicyMaxRetries, operations[0].MaxRetries);
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
                      .WithRetentionPolicy(
                          dropAfter: "7 days",
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

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

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
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Use_Default_RetryPeriod_When_Not_Specified()
    {
        using DefaultRetryPeriodContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.RetentionPolicyScheduleInterval, operations[0].RetryPeriod);
    }

    #endregion

    #region Should_Extract_Multiple_RetentionPolicies

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
                      .WithRetentionPolicy(dropAfter: "7 days");
            });

            modelBuilder.Entity<MultipleEvent>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Events");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "14 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_RetentionPolicies()
    {
        using MultiplePoliciesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Equal(2, operations.Count);
        Assert.Contains(operations, op => op.TableName == "Metrics");
        Assert.Contains(operations, op => op.TableName == "Events");
    }

    #endregion

    #region Should_Extract_Fully_Configured_RetentionPolicy

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
                      .WithRetentionPolicy(
                          dropAfter: "30 days",
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
    public void Should_Extract_Fully_Configured_RetentionPolicy()
    {
        using FullyConfiguredContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        AddRetentionPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("30 days", operation.DropAfter);
        Assert.Null(operation.DropCreatedBefore);
        Assert.NotNull(operation.InitialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, operation.InitialStart.Value);
        Assert.Equal("06:00:00", operation.ScheduleInterval);
        Assert.Equal("02:00:00", operation.MaxRuntime);
        Assert.Equal(3, operation.MaxRetries);
        Assert.Equal("00:15:00", operation.RetryPeriod);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_From_Attribute

    [Hypertable("Timestamp")]
    [RetentionPolicy("7 days")]
    private class RetentionPolicyAttributeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyAttributeContext : DbContext
    {
        public DbSet<RetentionPolicyAttributeMetric> Metrics => Set<RetentionPolicyAttributeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionPolicyAttributeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Extract_RetentionPolicy_From_Attribute()
    {
        using RetentionPolicyAttributeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        AddRetentionPolicyOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("7 days", operation.DropAfter);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_From_View

    private class ViewRetentionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ViewRetentionPolicyContext : DbContext
    {
        public DbSet<ViewRetentionMetric> Metrics => Set<ViewRetentionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewRetentionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("my_view");
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_RetentionPolicy_From_View()
    {
        using ViewRetentionPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        AddRetentionPolicyOperation operation = operations[0];
        Assert.Equal("my_view", operation.TableName);
        Assert.Equal("7 days", operation.DropAfter);
    }

    #endregion

    #region Should_Extract_RetentionPolicy_With_ViewSchema

    private class ViewSchemaRetentionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ViewSchemaRetentionPolicyContext : DbContext
    {
        public DbSet<ViewSchemaRetentionMetric> Metrics => Set<ViewSchemaRetentionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewSchemaRetentionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("my_view", "analytics");
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_RetentionPolicy_With_ViewSchema()
    {
        using ViewSchemaRetentionPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        AddRetentionPolicyOperation operation = operations[0];
        Assert.Equal("my_view", operation.TableName);
        Assert.Equal("analytics", operation.Schema);
    }

    #endregion

    #region Should_Use_DefaultSchema_When_No_Schema_Specified

    private class ViewDefaultSchemaRetentionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ViewDefaultSchemaRetentionPolicyContext : DbContext
    {
        public DbSet<ViewDefaultSchemaRetentionMetric> Metrics => Set<ViewDefaultSchemaRetentionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewDefaultSchemaRetentionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("my_view");
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Use_DefaultSchema_When_No_Schema_Specified()
    {
        using ViewDefaultSchemaRetentionPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("public", operations[0].Schema);
    }

    #endregion

    #region Should_Extract_Custom_Schema

    private class CustomSchemaRetentionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomSchemaRetentionPolicyContext : DbContext
    {
        public DbSet<CustomSchemaRetentionMetric> Metrics => Set<CustomSchemaRetentionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomSchemaRetentionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics", "custom_schema");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Custom_Schema()
    {
        using CustomSchemaRetentionPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("custom_schema", operations[0].Schema);
    }

    #endregion

    #region Should_Skip_Entity_With_HasRetentionPolicy_But_No_Drop_Values

    private class HasRetentionNoDropMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HasRetentionNoDropContext : DbContext
    {
        public DbSet<HasRetentionNoDropMetric> Metrics => Set<HasRetentionNoDropMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HasRetentionNoDropMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("HasRetentionNoDrop");
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
            });
        }
    }

    [Fact]
    public void Should_Skip_Entity_With_HasRetentionPolicy_But_No_Drop_Values()
    {
        using HasRetentionNoDropContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Skip_Entity_With_No_Table_Or_View_Name

    private class NoTableNameMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoTableNameContext : DbContext
    {
        public DbSet<NoTableNameMetric> Metrics => Set<NoTableNameMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoTableNameMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable((string?)null);
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Skip_Entity_With_No_Table_Or_View_Name()
    {
        using NoTableNameContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<AddRetentionPolicyOperation> operations = [.. RetentionPolicyModelExtractor.GetRetentionPolicies(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion
}

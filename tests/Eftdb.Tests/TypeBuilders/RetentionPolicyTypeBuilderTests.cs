using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests that verify RetentionPolicyTypeBuilder Fluent API methods correctly apply annotations.
/// </summary>
public class RetentionPolicyTypeBuilderTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region WithRetentionPolicy_Should_Set_HasRetentionPolicy_Annotation

    private class HasRetentionPolicyEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HasRetentionPolicyContext : DbContext
    {
        public DbSet<HasRetentionPolicyEntity> Metrics => Set<HasRetentionPolicyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HasRetentionPolicyEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_HasRetentionPolicy_Annotation()
    {
        using HasRetentionPolicyContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(HasRetentionPolicyEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_DropAfter_Annotation

    private class DropAfterEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropAfterContext : DbContext
    {
        public DbSet<DropAfterEntity> Metrics => Set<DropAfterEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropAfterEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_DropAfter_Annotation()
    {
        using DropAfterContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DropAfterEntity))!;

        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_DropCreatedBefore_Annotation

    private class DropCreatedBeforeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropCreatedBeforeContext : DbContext
    {
        public DbSet<DropCreatedBeforeEntity> Metrics => Set<DropCreatedBeforeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DropCreatedBeforeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropCreatedBefore: "30 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_DropCreatedBefore_Annotation()
    {
        using DropCreatedBeforeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DropCreatedBeforeEntity))!;

        Assert.Equal("30 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Throw_When_Both_DropAfter_And_DropCreatedBefore_Specified

    private class BothSpecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BothSpecifiedContext : DbContext
    {
        public DbSet<BothSpecifiedEntity> Metrics => Set<BothSpecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BothSpecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days", dropCreatedBefore: "30 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Throw_When_Both_DropAfter_And_DropCreatedBefore_Specified()
    {
        using BothSpecifiedContext context = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GetModel(context));
        Assert.Contains("mutually exclusive", exception.Message);
    }

    #endregion

    #region WithRetentionPolicy_Should_Throw_When_Neither_DropAfter_Nor_DropCreatedBefore_Specified

    private class NeitherSpecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NeitherSpecifiedContext : DbContext
    {
        public DbSet<NeitherSpecifiedEntity> Metrics => Set<NeitherSpecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NeitherSpecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy();
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Throw_When_Neither_DropAfter_Nor_DropCreatedBefore_Specified()
    {
        using NeitherSpecifiedContext context = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GetModel(context));
        Assert.Contains("Exactly one", exception.Message);
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_InitialStart_When_Provided

    private class InitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartContext : DbContext
    {
        public DbSet<InitialStartEntity> Metrics => Set<InitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                );
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_InitialStart_When_Provided()
    {
        using InitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(InitialStartEntity))!;

        object? initialStartValue = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);

        DateTime initialStart = (DateTime)initialStartValue;
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), initialStart);
    }

    #endregion

    #region WithRetentionPolicy_Should_Not_Set_InitialStart_When_Null

    private class NoInitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoInitialStartContext : DbContext
    {
        public DbSet<NoInitialStartEntity> Metrics => Set<NoInitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoInitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Not_Set_InitialStart_When_Null()
    {
        using NoInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoInitialStartEntity))!;

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart));
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_ScheduleInterval_When_Provided

    private class ScheduleIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalContext : DbContext
    {
        public DbSet<ScheduleIntervalEntity> Metrics => Set<ScheduleIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    scheduleInterval: "1 day"
                );
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_ScheduleInterval_When_Provided()
    {
        using ScheduleIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScheduleIntervalEntity))!;

        Assert.Equal("1 day", entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Not_Set_ScheduleInterval_When_Null

    private class NoScheduleIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoScheduleIntervalContext : DbContext
    {
        public DbSet<NoScheduleIntervalEntity> Metrics => Set<NoScheduleIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoScheduleIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Not_Set_ScheduleInterval_When_Null()
    {
        using NoScheduleIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoScheduleIntervalEntity))!;

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_MaxRuntime_When_Provided

    private class MaxRuntimeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRuntimeContext : DbContext
    {
        public DbSet<MaxRuntimeEntity> Metrics => Set<MaxRuntimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    maxRuntime: "01:00:00"
                );
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_MaxRuntime_When_Provided()
    {
        using MaxRuntimeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRuntimeEntity))!;

        Assert.Equal("01:00:00", entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Not_Set_MaxRuntime_When_Null

    private class NoMaxRuntimeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoMaxRuntimeContext : DbContext
    {
        public DbSet<NoMaxRuntimeEntity> Metrics => Set<NoMaxRuntimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoMaxRuntimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Not_Set_MaxRuntime_When_Null()
    {
        using NoMaxRuntimeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoMaxRuntimeEntity))!;

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime));
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_MaxRetries_When_Provided

    private class MaxRetriesEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRetriesContext : DbContext
    {
        public DbSet<MaxRetriesEntity> Metrics => Set<MaxRetriesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    maxRetries: 5
                );
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_MaxRetries_When_Provided()
    {
        using MaxRetriesContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRetriesEntity))!;

        Assert.Equal(5, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Not_Set_MaxRetries_When_Null

    private class NoMaxRetriesEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoMaxRetriesContext : DbContext
    {
        public DbSet<NoMaxRetriesEntity> Metrics => Set<NoMaxRetriesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoMaxRetriesEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Not_Set_MaxRetries_When_Null()
    {
        using NoMaxRetriesContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoMaxRetriesEntity))!;

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries));
    }

    #endregion

    #region WithRetentionPolicy_Should_Set_RetryPeriod_When_Provided

    private class RetryPeriodEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetryPeriodContext : DbContext
    {
        public DbSet<RetryPeriodEntity> Metrics => Set<RetryPeriodEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetryPeriodEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    retryPeriod: "00:10:00"
                );
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Set_RetryPeriod_When_Provided()
    {
        using RetryPeriodContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RetryPeriodEntity))!;

        Assert.Equal("00:10:00", entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Not_Set_RetryPeriod_When_Null

    private class NoRetryPeriodEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoRetryPeriodContext : DbContext
    {
        public DbSet<NoRetryPeriodEntity> Metrics => Set<NoRetryPeriodEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoRetryPeriodEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Not_Set_RetryPeriod_When_Null()
    {
        using NoRetryPeriodContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoRetryPeriodEntity))!;

        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod));
    }

    #endregion

    #region WithRetentionPolicy_Should_Support_All_Parameters

    private class FullyConfiguredEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullyConfiguredContext : DbContext
    {
        public DbSet<FullyConfiguredEntity> Metrics => Set<FullyConfiguredEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    scheduleInterval: "1 day",
                    maxRuntime: "02:00:00",
                    maxRetries: 3,
                    retryPeriod: "00:15:00"
                );
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Support_All_Parameters()
    {
        using FullyConfiguredContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);

        object? initialStartValue = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        DateTime initialStart = (DateTime)initialStartValue;
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), initialStart);

        Assert.Equal("1 day", entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("02:00:00", entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("00:15:00", entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region WithRetentionPolicy_Should_Return_EntityTypeBuilder_For_Chaining

    private class MethodChainingEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MethodChainingContext : DbContext
    {
        public DbSet<MethodChainingEntity> Metrics => Set<MethodChainingEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MethodChainingEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void WithRetentionPolicy_Should_Return_EntityTypeBuilder_For_Chaining()
    {
        using MethodChainingContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MethodChainingEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
    }

    #endregion

    #region ScaffoldOverload_Should_Set_HasRetentionPolicy_And_DropAfter

    private class ScaffoldDropAfterEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldDropAfterContext : DbContext
    {
        public DbSet<ScaffoldDropAfterEntity> Items => Set<ScaffoldDropAfterEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldDropAfterEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_drop_after");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Set_HasRetentionPolicy_And_DropAfter()
    {
        // Arrange & Act
        using ScaffoldDropAfterContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldDropAfterEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore));
    }

    #endregion

    #region ScaffoldOverload_Should_Set_HasRetentionPolicy_And_DropCreatedBefore

    private class ScaffoldDropCreatedBeforeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldDropCreatedBeforeContext : DbContext
    {
        public DbSet<ScaffoldDropCreatedBeforeEntity> Items => Set<ScaffoldDropCreatedBeforeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldDropCreatedBeforeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_drop_created_before");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: null,
                    dropCreatedBefore: "30 days",
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Set_HasRetentionPolicy_And_DropCreatedBefore()
    {
        // Arrange & Act
        using ScaffoldDropCreatedBeforeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldDropCreatedBeforeEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore)?.Value);
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter));
    }

    #endregion

    #region ScaffoldOverload_Should_Set_Optional_Annotations_When_Provided

    private class ScaffoldAllParamsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldAllParamsContext : DbContext
    {
        public DbSet<ScaffoldAllParamsEntity> Items => Set<ScaffoldAllParamsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldAllParamsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_all_params");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: "1 day",
                    maxRuntime: "01:00:00",
                    maxRetries: 3,
                    retryPeriod: "00:10:00");
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Set_Optional_Annotations_When_Provided()
    {
        // Arrange & Act
        using ScaffoldAllParamsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldAllParamsEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("01:00:00", entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("00:10:00", entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region ScaffoldOverload_Should_Not_Set_Optional_Annotations_When_All_Null

    private class ScaffoldMinimalParamsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldMinimalParamsContext : DbContext
    {
        public DbSet<ScaffoldMinimalParamsEntity> Items => Set<ScaffoldMinimalParamsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldMinimalParamsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_minimal_params");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Not_Set_Optional_Annotations_When_All_Null()
    {
        // Arrange & Act
        using ScaffoldMinimalParamsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldMinimalParamsEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval));
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime));
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries));
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod));
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart));
    }

    #endregion

    #region ScaffoldOverload_Should_Throw_When_Both_DropAfter_And_DropCreatedBefore_Specified

    private class ScaffoldBothEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldBothContext : DbContext
    {
        public DbSet<ScaffoldBothEntity> Items => Set<ScaffoldBothEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldBothEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_both");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: "30 days",
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Throw_When_Both_DropAfter_And_DropCreatedBefore_Specified()
    {
        // Arrange
        using ScaffoldBothContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GetModel(context));
        Assert.Contains("mutually exclusive", exception.Message);
    }

    #endregion

    #region ScaffoldOverload_Should_Throw_When_Neither_DropAfter_Nor_DropCreatedBefore_Specified

    private class ScaffoldNeitherEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldNeitherContext : DbContext
    {
        public DbSet<ScaffoldNeitherEntity> Items => Set<ScaffoldNeitherEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldNeitherEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_neither");
                entity.WithRetentionPolicy(
                    dropAfter: null,
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Throw_When_Neither_DropAfter_Nor_DropCreatedBefore_Specified()
    {
        // Arrange
        using ScaffoldNeitherContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GetModel(context));
        Assert.Contains("Exactly one", exception.Message);
    }

    #endregion

    #region ScaffoldOverload_Should_Return_RetentionPolicyStringBuilder_For_Chaining

    private class ScaffoldStringBuilderEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldStringBuilderContext : DbContext
    {
        public DbSet<ScaffoldStringBuilderEntity> Items => Set<ScaffoldStringBuilderEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldStringBuilderEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_string_builder");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null)
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Return_RetentionPolicyStringBuilder_For_Chaining()
    {
        // Arrange & Act
        using ScaffoldStringBuilderContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldStringBuilderEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.NotNull(entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart));
    }

    #endregion

    #region ScaffoldOverload_Should_Not_Write_InitialStart_Without_StringBuilder_Chain

    private class ScaffoldNoInitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldNoInitialStartContext : DbContext
    {
        public DbSet<ScaffoldNoInitialStartEntity> Items => Set<ScaffoldNoInitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldNoInitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_no_initial_start");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Should_Not_Write_InitialStart_Without_StringBuilder_Chain()
    {
        // Arrange & Act
        using ScaffoldNoInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldNoInitialStartEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart));
    }

    #endregion

    // ── RetentionPolicyStringBuilder tests ───────────────────────────────────

    #region StringBuilder_WithInitialStart_Sets_Annotation

    private class StringBuilderInitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilderInitialStartContext : DbContext
    {
        public DbSet<StringBuilderInitialStartEntity> Items => Set<StringBuilderInitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderInitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_initial_start");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null)
                    .WithInitialStart(new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void StringBuilder_WithInitialStart_Sets_Annotation()
    {
        // Arrange & Act
        using StringBuilderInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(StringBuilderInitialStartEntity))!;

        // Assert
        object? value = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(value);
        Assert.IsType<DateTime>(value);
        Assert.Equal(new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc), (DateTime)value);
    }

    #endregion

    #region StringBuilder_WithInitialStart_Returns_Builder_For_Chaining

    private class StringBuilderChainEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilderChainContext : DbContext
    {
        public DbSet<StringBuilderChainEntity> Items => Set<StringBuilderChainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderChainEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_chain");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                RetentionPolicyStringBuilder<StringBuilderChainEntity> builder =
                    entity.WithRetentionPolicy(
                        dropAfter: "7 days",
                        dropCreatedBefore: null,
                        scheduleInterval: null,
                        maxRuntime: null,
                        maxRetries: null,
                        retryPeriod: null);

                RetentionPolicyStringBuilder<StringBuilderChainEntity> chainResult =
                    builder.WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.Same(builder, chainResult);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithInitialStart_Returns_Builder_For_Chaining()
    {
        // Arrange & Act
        using StringBuilderChainContext context = new();
        GetModel(context);
    }

    #endregion

    #region StringBuilder_Repeated_WithInitialStart_Latest_Value_Wins

    private class StringBuilderRepeatedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilderRepeatedContext : DbContext
    {
        public DbSet<StringBuilderRepeatedEntity> Items => Set<StringBuilderRepeatedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderRepeatedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_repeated");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null)
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                    .WithInitialStart(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void StringBuilder_Repeated_WithInitialStart_Latest_Value_Wins()
    {
        // Arrange & Act
        using StringBuilderRepeatedContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(StringBuilderRepeatedEntity))!;

        // Assert
        object? value = entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(value);
        Assert.Equal(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc), (DateTime)value);
    }

    #endregion

    // ── ContinuousAggregateStringBuilder overload tests ─────────────────────

    #region CaggStringBuilder_WithRetentionPolicy_Should_Set_HasRetentionPolicy_And_DropAfter

    private class CaggDropAfterSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggDropAfterView
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggDropAfterContext : DbContext
    {
        public DbSet<CaggDropAfterSource> Metrics => Set<CaggDropAfterSource>();
        public DbSet<CaggDropAfterView> HourlyMetrics => Set<CaggDropAfterView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggDropAfterSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_drop_after_source");
            });

            modelBuilder.Entity<CaggDropAfterView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_drop_after_view");
                entity.IsContinuousAggregate<CaggDropAfterView>(
                    "cagg_drop_after_view",
                    "cagg_drop_after_source",
                    "1 hour",
                    "Timestamp")
                    .WithRetentionPolicy(
                        dropAfter: "7 days",
                        dropCreatedBefore: null,
                        scheduleInterval: null,
                        maxRuntime: null,
                        maxRetries: null,
                        retryPeriod: null);
            });
        }
    }

    [Fact]
    public void CaggStringBuilder_WithRetentionPolicy_Should_Set_HasRetentionPolicy_And_DropAfter()
    {
        // Arrange
        using CaggDropAfterContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggDropAfterView))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore));
    }

    #endregion

    #region CaggStringBuilder_WithRetentionPolicy_Should_Set_DropCreatedBefore

    private class CaggDropCreatedBeforeSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggDropCreatedBeforeView
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggDropCreatedBeforeContext : DbContext
    {
        public DbSet<CaggDropCreatedBeforeSource> Metrics => Set<CaggDropCreatedBeforeSource>();
        public DbSet<CaggDropCreatedBeforeView> HourlyMetrics => Set<CaggDropCreatedBeforeView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggDropCreatedBeforeSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_drop_created_before_source");
            });

            modelBuilder.Entity<CaggDropCreatedBeforeView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_drop_created_before_view");
                entity.IsContinuousAggregate<CaggDropCreatedBeforeView>(
                    "cagg_drop_created_before_view",
                    "cagg_drop_created_before_source",
                    "1 hour",
                    "Timestamp")
                    .WithRetentionPolicy(
                        dropAfter: null,
                        dropCreatedBefore: "30 days",
                        scheduleInterval: null,
                        maxRuntime: null,
                        maxRetries: null,
                        retryPeriod: null);
            });
        }
    }

    [Fact]
    public void CaggStringBuilder_WithRetentionPolicy_Should_Set_DropCreatedBefore()
    {
        // Arrange
        using CaggDropCreatedBeforeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggDropCreatedBeforeView))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropCreatedBefore)?.Value);
        Assert.Null(entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter));
    }

    #endregion

    #region CaggStringBuilder_WithRetentionPolicy_Should_Set_Optional_Intervals

    private class CaggOptionalIntervalsSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggOptionalIntervalsView
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggOptionalIntervalsContext : DbContext
    {
        public DbSet<CaggOptionalIntervalsSource> Metrics => Set<CaggOptionalIntervalsSource>();
        public DbSet<CaggOptionalIntervalsView> HourlyMetrics => Set<CaggOptionalIntervalsView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggOptionalIntervalsSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_optional_intervals_source");
            });

            modelBuilder.Entity<CaggOptionalIntervalsView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_optional_intervals_view");
                entity.IsContinuousAggregate<CaggOptionalIntervalsView>(
                    "cagg_optional_intervals_view",
                    "cagg_optional_intervals_source",
                    "1 hour",
                    "Timestamp")
                    .WithRetentionPolicy(
                        dropAfter: "14 days",
                        dropCreatedBefore: null,
                        scheduleInterval: "1 day",
                        maxRuntime: "01:00:00",
                        maxRetries: 3,
                        retryPeriod: "00:10:00");
            });
        }
    }

    [Fact]
    public void CaggStringBuilder_WithRetentionPolicy_Should_Set_Optional_Intervals()
    {
        // Arrange
        using CaggOptionalIntervalsContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggOptionalIntervalsView))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("14 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(RetentionPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("01:00:00", entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(RetentionPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("00:10:00", entityType.FindAnnotation(RetentionPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region CaggStringBuilder_WithRetentionPolicy_Should_Return_RetentionPolicyStringBuilder_For_Chaining

    private class CaggChainingSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggChainingView
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggChainingContext : DbContext
    {
        public DbSet<CaggChainingSource> Metrics => Set<CaggChainingSource>();
        public DbSet<CaggChainingView> HourlyMetrics => Set<CaggChainingView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggChainingSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_chaining_source");
            });

            modelBuilder.Entity<CaggChainingView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_chaining_view");
                RetentionPolicyStringBuilder<CaggChainingView> builder =
                    entity.IsContinuousAggregate<CaggChainingView>(
                        "cagg_chaining_view",
                        "cagg_chaining_source",
                        "1 hour",
                        "Timestamp")
                        .WithRetentionPolicy(
                            dropAfter: "7 days",
                            dropCreatedBefore: null,
                            scheduleInterval: null,
                            maxRuntime: null,
                            maxRetries: null,
                            retryPeriod: null);

                builder.WithInitialStart(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void CaggStringBuilder_WithRetentionPolicy_Should_Return_RetentionPolicyStringBuilder_For_Chaining()
    {
        // Arrange
        using CaggChainingContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggChainingView))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
        Assert.NotNull(entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart));
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            entityType.FindAnnotation(RetentionPolicyAnnotations.InitialStart)!.Value);
    }

    #endregion

    #region CaggStringBuilder_WithRetentionPolicy_Should_Throw_When_Both_DropAfter_And_DropCreatedBefore

    private class CaggBothSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggBothView
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggBothContext : DbContext
    {
        public DbSet<CaggBothSource> Metrics => Set<CaggBothSource>();
        public DbSet<CaggBothView> HourlyMetrics => Set<CaggBothView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggBothSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_both_source");
            });

            modelBuilder.Entity<CaggBothView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_both_view");
                entity.IsContinuousAggregate<CaggBothView>(
                    "cagg_both_view",
                    "cagg_both_source",
                    "1 hour",
                    "Timestamp")
                    .WithRetentionPolicy(
                        dropAfter: "7 days",
                        dropCreatedBefore: "30 days",
                        scheduleInterval: null,
                        maxRuntime: null,
                        maxRetries: null,
                        retryPeriod: null);
            });
        }
    }

    [Fact]
    public void CaggStringBuilder_WithRetentionPolicy_Should_Throw_When_Both_DropAfter_And_DropCreatedBefore()
    {
        // Arrange
        using CaggBothContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GetModel(context));
        Assert.Contains("mutually exclusive", exception.Message);
    }

    #endregion

    #region CaggStringBuilder_WithRetentionPolicy_Should_Throw_When_Neither_Specified

    private class CaggNeitherSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaggNeitherView
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggNeitherContext : DbContext
    {
        public DbSet<CaggNeitherSource> Metrics => Set<CaggNeitherSource>();
        public DbSet<CaggNeitherView> HourlyMetrics => Set<CaggNeitherView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggNeitherSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cagg_neither_source");
            });

            modelBuilder.Entity<CaggNeitherView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cagg_neither_view");
                entity.IsContinuousAggregate<CaggNeitherView>(
                    "cagg_neither_view",
                    "cagg_neither_source",
                    "1 hour",
                    "Timestamp")
                    .WithRetentionPolicy(
                        dropAfter: null,
                        dropCreatedBefore: null,
                        scheduleInterval: null,
                        maxRuntime: null,
                        maxRetries: null,
                        retryPeriod: null);
            });
        }
    }

    [Fact]
    public void CaggStringBuilder_WithRetentionPolicy_Should_Throw_When_Neither_Specified()
    {
        // Arrange
        using CaggNeitherContext context = new();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GetModel(context));
        Assert.Contains("Exactly one", exception.Message);
    }

    #endregion

    // ── Parity test: scaffold overload vs. named-param overload ──────────────

    #region ScaffoldOverload_Produces_Identical_Annotations_To_NamedParam_Overload

    private class ParityNamedParamEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ParityScaffoldEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ParityNamedParamContext : DbContext
    {
        public DbSet<ParityNamedParamEntity> Items => Set<ParityNamedParamEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParityNamedParamEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("parity_named");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    scheduleInterval: "1 day",
                    maxRuntime: "01:00:00",
                    maxRetries: 3,
                    retryPeriod: "00:10:00");
            });
        }
    }

    private class ParityScaffoldContext : DbContext
    {
        public DbSet<ParityScaffoldEntity> Items => Set<ParityScaffoldEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParityScaffoldEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("parity_scaffold");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    dropCreatedBefore: null,
                    scheduleInterval: "1 day",
                    maxRuntime: "01:00:00",
                    maxRetries: 3,
                    retryPeriod: "00:10:00");
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_Produces_Identical_Annotations_To_NamedParam_Overload()
    {
        // Arrange & Act
        using ParityNamedParamContext namedCtx = new();
        using ParityScaffoldContext scaffoldCtx = new();

        IModel namedModel = GetModel(namedCtx);
        IModel scaffoldModel = GetModel(scaffoldCtx);

        IEntityType namedEntity = namedModel.FindEntityType(typeof(ParityNamedParamEntity))!;
        IEntityType scaffoldEntity = scaffoldModel.FindEntityType(typeof(ParityScaffoldEntity))!;

        // Assert
        string[] sharedAnnotationKeys =
        [
            RetentionPolicyAnnotations.HasRetentionPolicy,
            RetentionPolicyAnnotations.DropAfter,
            RetentionPolicyAnnotations.ScheduleInterval,
            RetentionPolicyAnnotations.MaxRuntime,
            RetentionPolicyAnnotations.MaxRetries,
            RetentionPolicyAnnotations.RetryPeriod,
        ];

        foreach (string key in sharedAnnotationKeys)
        {
            object? namedValue = namedEntity.FindAnnotation(key)?.Value;
            object? scaffoldValue = scaffoldEntity.FindAnnotation(key)?.Value;
            Assert.Equal(namedValue, scaffoldValue);
        }
    }

    #endregion
}

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
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify ReorderPolicyConvention processes [ReorderPolicy] attribute correctly
/// and applies the same annotations as the Fluent API.
/// </summary>
public class ReorderPolicyConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Process_Minimal_ReorderPolicy_Attribute

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx")]
    private class MinimalReorderPolicyEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalAttributeContext : DbContext
    {
        public DbSet<MinimalReorderPolicyEntity> Entities => Set<MinimalReorderPolicyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalReorderPolicyEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MinimalReorderPolicy");
            });
        }
    }

    [Fact]
    public void Should_Process_Minimal_ReorderPolicy_Attribute()
    {
        using MinimalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalReorderPolicyEntity))!;

        Assert.NotNull(entityType);
        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("metrics_time_idx", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
    }

    #endregion

    #region Should_Process_ReorderPolicy_With_ScheduleInterval

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", ScheduleInterval = "12:00:00")]
    private class ScheduleIntervalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalAttributeContext : DbContext
    {
        public DbSet<ScheduleIntervalEntity> Entities => Set<ScheduleIntervalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ScheduleInterval");
            });
        }
    }

    [Fact]
    public void Should_Process_ReorderPolicy_With_ScheduleInterval()
    {
        using ScheduleIntervalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScheduleIntervalEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("12:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region Should_Process_ReorderPolicy_With_InitialStart

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", InitialStart = "2025-01-01T00:00:00Z")]
    private class InitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartAttributeContext : DbContext
    {
        public DbSet<InitialStartEntity> Entities => Set<InitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("InitialStart");
            });
        }
    }

    [Fact]
    public void Should_Process_ReorderPolicy_With_InitialStart()
    {
        using InitialStartAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(InitialStartEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);

        object? initialStartValue = entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);

        DateTime initialStart = (DateTime)initialStartValue;
        DateTime utcStart = initialStart.ToUniversalTime();
        Assert.Equal(2025, utcStart.Year);
        Assert.Equal(1, utcStart.Month);
        Assert.Equal(1, utcStart.Day);
        Assert.Equal(0, utcStart.Hour);
        Assert.Equal(0, utcStart.Minute);
        Assert.Equal(0, utcStart.Second);
    }

    #endregion

    #region Should_Throw_When_InitialStart_Has_Invalid_Format

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", InitialStart = "invalid-date-format")]
    private class InvalidInitialStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InvalidInitialStartContext : DbContext
    {
        public DbSet<InvalidInitialStartEntity> Entities => Set<InvalidInitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvalidInitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("InvalidInitialStart");
            });
        }
    }

    [Fact]
    public void Should_Throw_When_InitialStart_Has_Invalid_Format()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using InvalidInitialStartContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("InitialStart", exception.Message);
        Assert.Contains("not a valid DateTime format", exception.Message);
    }

    #endregion

    #region Should_Process_ReorderPolicy_With_MaxRetries

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", MaxRetries = 5)]
    private class MaxRetriesEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRetriesAttributeContext : DbContext
    {
        public DbSet<MaxRetriesEntity> Entities => Set<MaxRetriesEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRetriesEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MaxRetries");
            });
        }
    }

    [Fact]
    public void Should_Process_ReorderPolicy_With_MaxRetries()
    {
        using MaxRetriesAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRetriesEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal(5, entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value);
    }

    #endregion

    #region Should_Not_Set_MaxRetries_When_Using_Default_Value

    [Fact]
    public void Should_Not_Set_MaxRetries_When_Using_Default_Value()
    {
        using MinimalAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalReorderPolicyEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries));
    }

    #endregion

    #region Should_Process_ReorderPolicy_With_MaxRuntime

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", MaxRuntime = "01:00:00")]
    private class MaxRuntimeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MaxRuntimeAttributeContext : DbContext
    {
        public DbSet<MaxRuntimeEntity> Entities => Set<MaxRuntimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaxRuntimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("MaxRuntime");
            });
        }
    }

    [Fact]
    public void Should_Process_ReorderPolicy_With_MaxRuntime()
    {
        using MaxRuntimeAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRuntimeEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("01:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value);
    }

    #endregion

    #region Should_Process_ReorderPolicy_With_RetryPeriod

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", RetryPeriod = "00:10:00")]
    private class RetryPeriodEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetryPeriodAttributeContext : DbContext
    {
        public DbSet<RetryPeriodEntity> Entities => Set<RetryPeriodEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetryPeriodEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("RetryPeriod");
            });
        }
    }

    [Fact]
    public void Should_Process_ReorderPolicy_With_RetryPeriod()
    {
        using RetryPeriodAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RetryPeriodEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("00:10:00", entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region Should_Process_Fully_Configured_ReorderPolicy

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", ScheduleInterval = "06:00:00", MaxRuntime = "02:00:00", MaxRetries = 3, RetryPeriod = "00:15:00")]
    private class FullyConfiguredEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class FullyConfiguredAttributeContext : DbContext
    {
        public DbSet<FullyConfiguredEntity> Entities => Set<FullyConfiguredEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("FullyConfigured");
            });
        }
    }

    [Fact]
    public void Should_Process_Fully_Configured_ReorderPolicy()
    {
        using FullyConfiguredAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("metrics_time_idx", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
        Assert.Equal("06:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("02:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("00:15:00", entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region Should_Not_Process_Entity_Without_Attribute

    [Hypertable("Timestamp")]
    private class PlainEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoAttributeContext : DbContext
    {
        public DbSet<PlainEntity> Entities => Set<PlainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Plain");
            });
        }
    }

    [Fact]
    public void Should_Not_Process_Entity_Without_Attribute()
    {
        using NoAttributeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(PlainEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName));
    }

    #endregion

    #region Attribute_Should_Produce_Same_Annotations_As_FluentAPI

    [Hypertable("Timestamp")]
    [ReorderPolicy("metrics_time_idx", ScheduleInterval = "12:00:00", MaxRuntime = "01:00:00")]
    private class EquivalenceAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [Hypertable("Timestamp")]
    private class EquivalenceFluentEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AttributeBasedContext : DbContext
    {
        public DbSet<EquivalenceAttributeEntity> Entities => Set<EquivalenceAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquivalenceAttributeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Equivalence");
            });
        }
    }

    private class FluentApiBasedContext : DbContext
    {
        public DbSet<EquivalenceFluentEntity> Entities => Set<EquivalenceFluentEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquivalenceFluentEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Equivalence");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    scheduleInterval: "12:00:00",
                    maxRuntime: "01:00:00"
                );
            });
        }
    }

    [Fact]
    public void Attribute_Should_Produce_Same_Annotations_As_FluentAPI()
    {
        using AttributeBasedContext attributeContext = new();
        using FluentApiBasedContext fluentContext = new();

        IModel attributeModel = GetModel(attributeContext);
        IModel fluentModel = GetModel(fluentContext);

        IEntityType attributeEntity = attributeModel.FindEntityType(typeof(EquivalenceAttributeEntity))!;
        IEntityType fluentEntity = fluentModel.FindEntityType(typeof(EquivalenceFluentEntity))!;

        Assert.Equal(
            attributeEntity.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value,
            fluentEntity.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value,
            fluentEntity.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value,
            fluentEntity.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value
        );
        Assert.Equal(
            attributeEntity.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value,
            fluentEntity.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value
        );
    }

    #endregion
}

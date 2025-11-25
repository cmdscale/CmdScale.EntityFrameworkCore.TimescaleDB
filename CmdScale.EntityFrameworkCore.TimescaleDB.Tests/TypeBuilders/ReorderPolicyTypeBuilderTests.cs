using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests that verify ReorderPolicyTypeBuilder Fluent API methods correctly apply annotations.
/// </summary>
public class ReorderPolicyTypeBuilderTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region WithReorderPolicy_Should_Set_HasReorderPolicy_Annotation

    private class MinimalEntity1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalContext1 : DbContext
    {
        public DbSet<MinimalEntity1> Metrics => Set<MinimalEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalEntity1>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_HasReorderPolicy_Annotation()
    {
        using MinimalContext1 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalEntity1))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Set_IndexName

    private class MinimalEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalContext2 : DbContext
    {
        public DbSet<MinimalEntity2> Metrics => Set<MinimalEntity2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalEntity2>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_IndexName()
    {
        using MinimalContext2 context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MinimalEntity2))!;

        Assert.Equal("metrics_time_idx", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Set_InitialStart_When_Provided

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                );
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_InitialStart_When_Provided()
    {
        using InitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(InitialStartEntity))!;

        object? initialStartValue = entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        Assert.IsType<DateTime>(initialStartValue);

        DateTime initialStart = (DateTime)initialStartValue;
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), initialStart);
    }

    #endregion

    #region WithReorderPolicy_Should_Not_Set_InitialStart_When_Null

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
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Not_Set_InitialStart_When_Null()
    {
        using NoInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoInitialStartEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart));
    }

    #endregion

    #region WithReorderPolicy_Should_Set_ScheduleInterval_When_Provided

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    scheduleInterval: "12:00:00"
                );
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_ScheduleInterval_When_Provided()
    {
        using ScheduleIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScheduleIntervalEntity))!;

        Assert.Equal("12:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Not_Set_ScheduleInterval_When_Null

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
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Not_Set_ScheduleInterval_When_Null()
    {
        using NoScheduleIntervalContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoScheduleIntervalEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region WithReorderPolicy_Should_Set_MaxRuntime_When_Provided

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    maxRuntime: "01:00:00"
                );
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_MaxRuntime_When_Provided()
    {
        using MaxRuntimeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRuntimeEntity))!;

        Assert.Equal("01:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Not_Set_MaxRuntime_When_Null

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
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Not_Set_MaxRuntime_When_Null()
    {
        using NoMaxRuntimeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoMaxRuntimeEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime));
    }

    #endregion

    #region WithReorderPolicy_Should_Set_MaxRetries_When_Provided

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    maxRetries: 5
                );
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_MaxRetries_When_Provided()
    {
        using MaxRetriesContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MaxRetriesEntity))!;

        Assert.Equal(5, entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Not_Set_MaxRetries_When_Null

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
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Not_Set_MaxRetries_When_Null()
    {
        using NoMaxRetriesContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoMaxRetriesEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries));
    }

    #endregion

    #region WithReorderPolicy_Should_Set_RetryPeriod_When_Provided

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    retryPeriod: "00:10:00"
                );
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Set_RetryPeriod_When_Provided()
    {
        using RetryPeriodContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RetryPeriodEntity))!;

        Assert.Equal("00:10:00", entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Not_Set_RetryPeriod_When_Null

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
                entity.WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Not_Set_RetryPeriod_When_Null()
    {
        using NoRetryPeriodContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoRetryPeriodEntity))!;

        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod));
    }

    #endregion

    #region WithReorderPolicy_Should_Support_All_Parameters

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
                entity.WithReorderPolicy(
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
    public void WithReorderPolicy_Should_Support_All_Parameters()
    {
        using FullyConfiguredContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(FullyConfiguredEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("metrics_time_idx", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);

        object? initialStartValue = entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(initialStartValue);
        DateTime initialStart = (DateTime)initialStartValue;
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), initialStart);

        Assert.Equal("06:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("02:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("00:15:00", entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region WithReorderPolicy_Should_Return_EntityTypeBuilder_For_Chaining

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
                      .WithReorderPolicy("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void WithReorderPolicy_Should_Return_EntityTypeBuilder_For_Chaining()
    {
        using MethodChainingContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MethodChainingEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal("Timestamp", entityType.FindAnnotation(HypertableAnnotations.HypertableTimeColumn)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("metrics_time_idx", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
    }

    #endregion
}

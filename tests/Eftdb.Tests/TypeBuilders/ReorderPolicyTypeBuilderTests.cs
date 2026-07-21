using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
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

    // ── EntityTypeBuilder-receiver scaffold overload ───────────────────────────

    #region ScaffoldOverload_EntityTypeBuilder_Sets_HasReorderPolicy_And_IndexName

    private class ScaffoldEtbBaseEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbBaseContext : DbContext
    {
        public DbSet<ScaffoldEtbBaseEntity> Items => Set<ScaffoldEtbBaseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbBaseEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_base");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_scaffold_etb_base", (string?)null, (string?)null, (int?)null, (string?)null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Sets_HasReorderPolicy_And_IndexName()
    {
        // Arrange
        using ScaffoldEtbBaseContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldEtbBaseEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("ix_scaffold_etb_base", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Sets_All_Optional_Annotations_When_Provided

    private class ScaffoldEtbFullEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbFullContext : DbContext
    {
        public DbSet<ScaffoldEtbFullEntity> Items => Set<ScaffoldEtbFullEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbFullEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_full");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_scaffold_etb_full", "2 days", "01:00:00", 3, "00:10:00");
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Sets_All_Optional_Annotations_When_Provided()
    {
        // Arrange
        using ScaffoldEtbFullContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldEtbFullEntity))!;

        // Assert
        Assert.Equal("2 days", entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("01:00:00", entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(3, entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal("00:10:00", entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Does_Not_Set_Optionals_When_All_Null

    private class ScaffoldEtbNullsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbNullsContext : DbContext
    {
        public DbSet<ScaffoldEtbNullsEntity> Items => Set<ScaffoldEtbNullsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbNullsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_nulls");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_scaffold_etb_nulls", (string?)null, (string?)null, (int?)null, (string?)null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Does_Not_Set_Optionals_When_All_Null()
    {
        // Arrange
        using ScaffoldEtbNullsContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldEtbNullsEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod));
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Returns_ReorderPolicyStringBuilder_Enabling_Chaining

    private class ScaffoldEtbChainEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbChainContext : DbContext
    {
        public DbSet<ScaffoldEtbChainEntity> Items => Set<ScaffoldEtbChainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbChainEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_chain");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_scaffold_etb_chain", (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Returns_ReorderPolicyStringBuilder_Enabling_Chaining()
    {
        // Arrange
        using ScaffoldEtbChainContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldEtbChainEntity))!;

        // Assert
        Assert.NotNull(entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart));
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)!.Value);
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Does_Not_Write_InitialStart_Itself

    private class ScaffoldEtbNoStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbNoStartContext : DbContext
    {
        public DbSet<ScaffoldEtbNoStartEntity> Items => Set<ScaffoldEtbNoStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbNoStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_no_start");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_scaffold_etb_no_start", "1 day", (string?)null, (int?)null, (string?)null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Does_Not_Write_InitialStart_Itself()
    {
        // Arrange
        using ScaffoldEtbNoStartContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldEtbNoStartEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart));
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Parity_With_UserFacing_Overload

    private class ScaffoldEtbParityUserEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbParityScaffoldEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldEtbParityUserContext : DbContext
    {
        public DbSet<ScaffoldEtbParityUserEntity> Items => Set<ScaffoldEtbParityUserEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbParityUserEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_parity_user");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy(
                    "ix_scaffold_etb_parity",
                    scheduleInterval: "2 days",
                    maxRuntime: "01:00:00",
                    maxRetries: 3,
                    retryPeriod: "00:10:00");
            });
        }
    }

    private class ScaffoldEtbParityScaffoldContext : DbContext
    {
        public DbSet<ScaffoldEtbParityScaffoldEntity> Items => Set<ScaffoldEtbParityScaffoldEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldEtbParityScaffoldEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_etb_parity_scaffold");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_scaffold_etb_parity", "2 days", "01:00:00", 3, "00:10:00");
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Parity_With_UserFacing_Overload()
    {
        // Arrange
        using ScaffoldEtbParityUserContext userContext = new();
        using ScaffoldEtbParityScaffoldContext scaffoldContext = new();

        // Act
        IEntityType userEntity = GetModel(userContext).FindEntityType(typeof(ScaffoldEtbParityUserEntity))!;
        IEntityType scaffoldEntity = GetModel(scaffoldContext).FindEntityType(typeof(ScaffoldEtbParityScaffoldEntity))!;

        // Assert
        Assert.Equal(
            userEntity.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value,
            scaffoldEntity.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal(
            userEntity.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value,
            scaffoldEntity.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
        Assert.Equal(
            userEntity.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value,
            scaffoldEntity.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal(
            userEntity.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value,
            scaffoldEntity.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime)?.Value);
        Assert.Equal(
            userEntity.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value,
            scaffoldEntity.FindAnnotation(ReorderPolicyAnnotations.MaxRetries)?.Value);
        Assert.Equal(
            userEntity.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value,
            scaffoldEntity.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod)?.Value);
    }

    #endregion

    // ── RetentionPolicyStringBuilder-receiver scaffold overload ───────────────

    #region ScaffoldOverload_RetentionBuilder_Sets_Both_HasRetentionPolicy_And_HasReorderPolicy

    private class ScaffoldRpRpEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldRpRpContext : DbContext
    {
        public DbSet<ScaffoldRpRpEntity> Items => Set<ScaffoldRpRpEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldRpRpEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_rp_rp");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy("7 days", (string?)null, (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithReorderPolicy("ix_scaffold_rp_rp", (string?)null, (string?)null, (int?)null, (string?)null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_RetentionBuilder_Sets_Both_HasRetentionPolicy_And_HasReorderPolicy()
    {
        // Arrange
        using ScaffoldRpRpContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldRpRpEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(ReorderPolicyAnnotations.HasReorderPolicy)?.Value);
        Assert.Equal("ix_scaffold_rp_rp", entityType.FindAnnotation(ReorderPolicyAnnotations.IndexName)?.Value);
    }

    #endregion

    #region ScaffoldOverload_RetentionBuilder_Preserves_DropAfter

    private class ScaffoldRpDropAfterEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldRpDropAfterContext : DbContext
    {
        public DbSet<ScaffoldRpDropAfterEntity> Items => Set<ScaffoldRpDropAfterEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldRpDropAfterEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_rp_drop_after");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy("30 days", (string?)null, (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithReorderPolicy("ix_scaffold_rp_drop_after", (string?)null, (string?)null, (int?)null, (string?)null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_RetentionBuilder_Preserves_DropAfter()
    {
        // Arrange
        using ScaffoldRpDropAfterContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldRpDropAfterEntity))!;

        // Assert
        Assert.Equal("30 days", entityType.FindAnnotation(RetentionPolicyAnnotations.DropAfter)?.Value);
    }

    #endregion

    #region ScaffoldOverload_RetentionBuilder_Returns_ReorderPolicyStringBuilder_Enabling_WithInitialStart

    private class ScaffoldRpStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldRpStartContext : DbContext
    {
        public DbSet<ScaffoldRpStartEntity> Items => Set<ScaffoldRpStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldRpStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_rp_start");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy("7 days", (string?)null, (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithReorderPolicy("ix_scaffold_rp_start", (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_RetentionBuilder_Returns_ReorderPolicyStringBuilder_Enabling_WithInitialStart()
    {
        // Arrange
        using ScaffoldRpStartContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldRpStartEntity))!;

        // Assert
        Assert.Equal(
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value);
    }

    #endregion

    #region ScaffoldOverload_RetentionBuilder_No_Optional_Reorder_Annotations_When_Null

    private class ScaffoldRpNullsEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScaffoldRpNullsContext : DbContext
    {
        public DbSet<ScaffoldRpNullsEntity> Items => Set<ScaffoldRpNullsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldRpNullsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("scaffold_rp_nulls");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy("7 days", (string?)null, (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithReorderPolicy("ix_scaffold_rp_nulls", (string?)null, (string?)null, (int?)null, (string?)null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_RetentionBuilder_No_Optional_Reorder_Annotations_When_Null()
    {
        // Arrange
        using ScaffoldRpNullsContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ScaffoldRpNullsEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.ScheduleInterval));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRuntime));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.MaxRetries));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.RetryPeriod));
        Assert.Null(entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart));
    }

    #endregion

    // ── ReorderPolicyStringBuilder ────────────────────────────────────────────

    #region ReorderPolicyStringBuilder_WithInitialStart_Sets_InitialStart_Annotation

    private class StringBuilderStartEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilderStartContext : DbContext
    {
        public DbSet<StringBuilderStartEntity> Items => Set<StringBuilderStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_start");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_sb_start", (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(new DateTime(2025, 5, 15, 12, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ReorderPolicyStringBuilder_WithInitialStart_Sets_InitialStart_Annotation()
    {
        // Arrange
        using StringBuilderStartContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(StringBuilderStartEntity))!;

        // Assert
        Assert.Equal(
            new DateTime(2025, 5, 15, 12, 0, 0, DateTimeKind.Utc),
            entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value);
    }

    #endregion

    #region ReorderPolicyStringBuilder_WithInitialStart_Returns_Same_Instance

    private class StringBuilderSameInstanceEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private (ReorderPolicyStringBuilder<StringBuilderSameInstanceEntity>? Original,
             ReorderPolicyStringBuilder<StringBuilderSameInstanceEntity>? Returned) _sbSameInstanceCapture;

    private class StringBuilderSameInstanceContext(ReorderPolicyTypeBuilderTests outer) : DbContext
    {
        public DbSet<StringBuilderSameInstanceEntity> Items => Set<StringBuilderSameInstanceEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderSameInstanceEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_same_instance");
                entity.IsHypertable(x => x.Timestamp);
                ReorderPolicyStringBuilder<StringBuilderSameInstanceEntity> original =
                    entity.WithReorderPolicy("ix_sb_same", (string?)null, (string?)null, (int?)null, (string?)null);
                ReorderPolicyStringBuilder<StringBuilderSameInstanceEntity> returned =
                    original.WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                outer._sbSameInstanceCapture = (original, returned);
            });
        }
    }

    [Fact]
    public void ReorderPolicyStringBuilder_WithInitialStart_Returns_Same_Instance()
    {
        // Arrange & Act
        using StringBuilderSameInstanceContext context = new(this);
        _ = GetModel(context);

        // Assert
        Assert.NotNull(_sbSameInstanceCapture.Original);
        Assert.Same(_sbSameInstanceCapture.Original, _sbSameInstanceCapture.Returned);
    }

    #endregion

    #region ReorderPolicyStringBuilder_WithInitialStart_Repeated_Call_Uses_Latest_Value

    private class StringBuilderRepeatEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class StringBuilderRepeatContext : DbContext
    {
        public DbSet<StringBuilderRepeatEntity> Items => Set<StringBuilderRepeatEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StringBuilderRepeatEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("sb_repeat");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("ix_sb_repeat", (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                      .WithInitialStart(new DateTime(2026, 7, 4, 8, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ReorderPolicyStringBuilder_WithInitialStart_Repeated_Call_Uses_Latest_Value()
    {
        // Arrange
        using StringBuilderRepeatContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(StringBuilderRepeatEntity))!;

        // Assert
        Assert.Equal(
            new DateTime(2026, 7, 4, 8, 0, 0, DateTimeKind.Utc),
            entityType.FindAnnotation(ReorderPolicyAnnotations.InitialStart)?.Value);
    }

    #endregion
}

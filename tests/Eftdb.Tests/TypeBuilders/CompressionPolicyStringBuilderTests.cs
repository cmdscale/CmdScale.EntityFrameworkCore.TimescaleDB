using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests for <c>CompressionPolicyStringBuilder</c> and the scaffold-targeting
/// <c>WithCompressionPolicy</c> overloads on <c>CompressionPolicyTypeBuilder</c>.
/// </summary>
public class CompressionPolicyStringBuilderTests
{
    private static IModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    // ── CompressionPolicyStringBuilder.WithInitialStart ──────────────────────

    // ── CompressionPolicyStringBuilder.EntityTypeBuilder accessor ────────────

    #region StringBuilder_EntityTypeBuilder_Returns_Same_Instance

    private class SbAccessorEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class SbAccessorContext : DbContext
    {
        public DbSet<SbAccessorEntity> Items => Set<SbAccessorEntity>();
        public EntityTypeBuilder<SbAccessorEntity>? CapturedEntityTypeBuilder { get; private set; }
        public CompressionPolicyStringBuilder<SbAccessorEntity>? CapturedStringBuilder { get; private set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SbAccessorEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_sb_accessor");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                CapturedEntityTypeBuilder = entity;
                CapturedStringBuilder = entity.WithCompressionPolicy(
                    after: "7 days",
                    createdBefore: null,
                    scheduleInterval: null,
                    timezone: null,
                    ifNotExists: null);
            });
        }
    }

    [Fact]
    public void StringBuilder_EntityTypeBuilder_Returns_Same_Instance()
    {
        // Arrange
        using SbAccessorContext context = new();

        // Act
        GetModel(context);

        // Assert
        Assert.NotNull(context.CapturedStringBuilder);
        Assert.Same(context.CapturedEntityTypeBuilder, context.CapturedStringBuilder.EntityTypeBuilder);
    }

    #endregion

    #region StringBuilder_WithInitialStart_Sets_Annotation

    private class SbInitialStartEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class SbInitialStartContext : DbContext
    {
        public DbSet<SbInitialStartEntity> Items => Set<SbInitialStartEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SbInitialStartEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_sb_initial_start");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithCompressionPolicy(
                    after: "7 days",
                    createdBefore: null,
                    scheduleInterval: null,
                    timezone: null,
                    ifNotExists: null)
                    .WithInitialStart(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void StringBuilder_WithInitialStart_Sets_Annotation()
    {
        // Arrange & Act
        using SbInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SbInitialStartEntity))!;

        // Assert
        object? value = entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart)?.Value;
        Assert.NotNull(value);
        Assert.IsType<DateTime>(value);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), (DateTime)value);
    }

    #endregion

    #region StringBuilder_WithInitialStart_Returns_Builder_For_Chaining

    private class SbChainEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class SbChainContext : DbContext
    {
        public DbSet<SbChainEntity> Items => Set<SbChainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SbChainEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_sb_chain");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");

                CompressionPolicyStringBuilder<SbChainEntity> builder =
                    entity.WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null);

                CompressionPolicyStringBuilder<SbChainEntity> chainResult =
                    builder.WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                Assert.Same(builder, chainResult);
            });
        }
    }

    [Fact]
    public void StringBuilder_WithInitialStart_Returns_Builder_For_Chaining()
    {
        // Arrange & Act
        using SbChainContext context = new();
        GetModel(context);
    }

    #endregion

    // ── EntityTypeBuilder scaffold overload ──────────────────────────────────

    #region ScaffoldOverload_EntityTypeBuilder_Should_Set_HasCompressionPolicy_And_After

    private class EtbAfterEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class EtbAfterContext : DbContext
    {
        public DbSet<EtbAfterEntity> Items => Set<EtbAfterEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EtbAfterEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_etb_after");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithCompressionPolicy(
                    after: "7 days",
                    createdBefore: null,
                    scheduleInterval: null,
                    timezone: null,
                    ifNotExists: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Should_Set_HasCompressionPolicy_And_After()
    {
        // Arrange & Act
        using EtbAfterContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EtbAfterEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.CreatedBefore));
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Should_Set_CreatedBefore

    private class EtbCreatedBeforeEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class EtbCreatedBeforeContext : DbContext
    {
        public DbSet<EtbCreatedBeforeEntity> Items => Set<EtbCreatedBeforeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EtbCreatedBeforeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_etb_created_before");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithCompressionPolicy(
                    after: null,
                    createdBefore: "30 days",
                    scheduleInterval: null,
                    timezone: null,
                    ifNotExists: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Should_Set_CreatedBefore()
    {
        // Arrange & Act
        using EtbCreatedBeforeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EtbCreatedBeforeEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("30 days", entityType.FindAnnotation(CompressionPolicyAnnotations.CreatedBefore)?.Value);
        Assert.Null(entityType.FindAnnotation(CompressionPolicyAnnotations.After));
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Should_Set_Optional_Annotations

    private class EtbAllParamsEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class EtbAllParamsContext : DbContext
    {
        public DbSet<EtbAllParamsEntity> Items => Set<EtbAllParamsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EtbAllParamsEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_etb_all_params");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithCompressionPolicy(
                    after: "7 days",
                    createdBefore: null,
                    scheduleInterval: "1 day",
                    timezone: "UTC",
                    ifNotExists: true);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Should_Set_Optional_Annotations()
    {
        // Arrange & Act
        using EtbAllParamsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EtbAllParamsEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
        Assert.Equal("1 day", entityType.FindAnnotation(CompressionPolicyAnnotations.ScheduleInterval)?.Value);
        Assert.Equal("UTC", entityType.FindAnnotation(CompressionPolicyAnnotations.Timezone)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.IfNotExists)?.Value);
    }

    #endregion

    #region ScaffoldOverload_EntityTypeBuilder_Should_Return_CompressionPolicyStringBuilder

    private class EtbReturnsBuilderEntity { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class EtbReturnsBuilderContext : DbContext
    {
        public DbSet<EtbReturnsBuilderEntity> Items => Set<EtbReturnsBuilderEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EtbReturnsBuilderEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_etb_returns_builder");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithCompressionPolicy(
                    after: "7 days",
                    createdBefore: null,
                    scheduleInterval: null,
                    timezone: null,
                    ifNotExists: null)
                    .WithInitialStart(new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_EntityTypeBuilder_Should_Return_CompressionPolicyStringBuilder()
    {
        // Arrange & Act
        using EtbReturnsBuilderContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EtbReturnsBuilderEntity))!;

        // Assert
        Assert.NotNull(entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart));
    }

    #endregion

    // ── RetentionPolicyStringBuilder scaffold overload ────────────────────────

    #region ScaffoldOverload_RetentionPolicyStringBuilder_Should_Set_CompressionPolicy

    private class RpsbSource { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class RpsbContext : DbContext
    {
        public DbSet<RpsbSource> Items => Set<RpsbSource>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpsbSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_rpsb");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "90 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null)
                    .WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_RetentionPolicyStringBuilder_Should_Set_CompressionPolicy()
    {
        // Arrange & Act
        using RpsbContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RpsbSource))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy)?.Value);
    }

    #endregion

    #region ScaffoldOverload_RetentionPolicyStringBuilder_Returns_CompressionPolicyStringBuilder_With_InitialStart

    private class RpsbInitialStartSource { public DateTime Timestamp { get; set; } public double Value { get; set; } }

    private class RpsbInitialStartContext : DbContext
    {
        public DbSet<RpsbInitialStartSource> Items => Set<RpsbInitialStartSource>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpsbInitialStartSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_rpsb_initial_start");
                entity.Property(x => x.Timestamp).HasColumnName("timestamp");
                entity.WithRetentionPolicy(
                    dropAfter: "90 days",
                    dropCreatedBefore: null,
                    scheduleInterval: null,
                    maxRuntime: null,
                    maxRetries: null,
                    retryPeriod: null)
                    .WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null)
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_RetentionPolicyStringBuilder_Returns_CompressionPolicyStringBuilder_With_InitialStart()
    {
        // Arrange & Act
        using RpsbInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(RpsbInitialStartSource))!;

        // Assert
        Assert.NotNull(entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart));
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            (DateTime)entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart)!.Value!);
    }

    #endregion

    // ── ContinuousAggregateStringBuilder scaffold overload ────────────────────

    #region ScaffoldOverload_CaggStringBuilder_Should_Set_CompressionPolicy

    private class CaggCpSource { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class CaggCpView { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class CaggCpContext : DbContext
    {
        public DbSet<CaggCpSource> Metrics => Set<CaggCpSource>();
        public DbSet<CaggCpView> Aggregates => Set<CaggCpView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggCpSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_cagg_source");
            });

            modelBuilder.Entity<CaggCpView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cp_cagg_view");
                entity.IsContinuousAggregate<CaggCpView>(
                    "cp_cagg_view",
                    "cp_cagg_source",
                    "1 hour",
                    "Timestamp")
                    .WithCompression()
                    .WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_CaggStringBuilder_Should_Set_CompressionPolicy()
    {
        // Arrange & Act
        using CaggCpContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggCpView))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
    }

    #endregion

    #region ScaffoldOverload_CaggStringBuilder_Returns_CompressionPolicyStringBuilder_With_InitialStart

    private class CaggCpInitialStartSource { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class CaggCpInitialStartView { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class CaggCpInitialStartContext : DbContext
    {
        public DbSet<CaggCpInitialStartSource> Metrics => Set<CaggCpInitialStartSource>();
        public DbSet<CaggCpInitialStartView> Aggregates => Set<CaggCpInitialStartView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggCpInitialStartSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_cagg_is_source");
            });

            modelBuilder.Entity<CaggCpInitialStartView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cp_cagg_is_view");
                entity.IsContinuousAggregate<CaggCpInitialStartView>(
                    "cp_cagg_is_view",
                    "cp_cagg_is_source",
                    "1 hour",
                    "Timestamp")
                    .WithCompression()
                    .WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null)
                    .WithInitialStart(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_CaggStringBuilder_Returns_CompressionPolicyStringBuilder_With_InitialStart()
    {
        // Arrange & Act
        using CaggCpInitialStartContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggCpInitialStartView))!;

        // Assert
        Assert.NotNull(entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart));
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            (DateTime)entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart)!.Value!);
    }

    #endregion

    // ── ContinuousAggregatePolicyStringBuilder scaffold overload ─────────────

    #region ScaffoldOverload_CaggPolicyStringBuilder_Should_Set_CompressionPolicy

    private class CaggPolCpSource { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class CaggPolCpView { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class CaggPolCpContext : DbContext
    {
        public DbSet<CaggPolCpSource> Metrics => Set<CaggPolCpSource>();
        public DbSet<CaggPolCpView> Aggregates => Set<CaggPolCpView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggPolCpSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_caggpol_source");
            });

            modelBuilder.Entity<CaggPolCpView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cp_caggpol_view");
                entity.IsContinuousAggregate<CaggPolCpView>(
                    "cp_caggpol_view",
                    "cp_caggpol_source",
                    "1 hour",
                    "Timestamp")
                    .WithCompression()
                    .WithRefreshPolicy(
                        startOffset: "1 month",
                        endOffset: null,
                        scheduleInterval: null)
                    .WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null);
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_CaggPolicyStringBuilder_Should_Set_CompressionPolicy()
    {
        // Arrange & Act
        using CaggPolCpContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggPolCpView))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy)?.Value);
        Assert.Equal("7 days", entityType.FindAnnotation(CompressionPolicyAnnotations.After)?.Value);
    }

    #endregion

    #region ScaffoldOverload_CaggPolicyStringBuilder_Returns_CompressionPolicyStringBuilder_With_InitialStart

    private class CaggPolCpIsSource { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class CaggPolCpIsView { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class CaggPolCpIsContext : DbContext
    {
        public DbSet<CaggPolCpIsSource> Metrics => Set<CaggPolCpIsSource>();
        public DbSet<CaggPolCpIsView> Aggregates => Set<CaggPolCpIsView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggPolCpIsSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("cp_caggpolis_source");
            });

            modelBuilder.Entity<CaggPolCpIsView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("cp_caggpolis_view");
                entity.IsContinuousAggregate<CaggPolCpIsView>(
                    "cp_caggpolis_view",
                    "cp_caggpolis_source",
                    "1 hour",
                    "Timestamp")
                    .WithCompression()
                    .WithRefreshPolicy(
                        startOffset: "1 month",
                        endOffset: null,
                        scheduleInterval: null)
                    .WithCompressionPolicy(
                        after: "7 days",
                        createdBefore: null,
                        scheduleInterval: null,
                        timezone: null,
                        ifNotExists: null)
                    .WithInitialStart(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void ScaffoldOverload_CaggPolicyStringBuilder_Returns_CompressionPolicyStringBuilder_With_InitialStart()
    {
        // Arrange & Act
        using CaggPolCpIsContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CaggPolCpIsView))!;

        // Assert
        Assert.NotNull(entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart));
        Assert.Equal(new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            (DateTime)entityType.FindAnnotation(CompressionPolicyAnnotations.InitialStart)!.Value!);
    }

    #endregion
}

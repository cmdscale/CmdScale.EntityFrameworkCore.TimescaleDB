using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests that verify every policy builder normalizes InitialStart to an unboxed Utc-kind DateTime
/// annotation regardless of the DateTimeKind supplied by the caller.
/// </summary>
public class PolicyInitialStartNormalizationTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static readonly DateTime UtcInstant = new(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc);

    private static DateTime AssertUtcDateTime(IEntityType entityType, string annotationKey)
    {
        object? value = entityType.FindAnnotation(annotationKey)?.Value;
        Assert.NotNull(value);
        Assert.IsType<DateTime>(value);
        DateTime dateTime = (DateTime)value;
        Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
        return dateTime;
    }

    // ── Reorder policy: typed builder ──────────────────────────────────────────

    #region Reorder_TypedBuilder_Local_Stores_Utc_Instant

    private class ReorderTypedLocalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderTypedLocalContext : DbContext
    {
        public DbSet<ReorderTypedLocalEntity> Metrics => Set<ReorderTypedLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderTypedLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reorder_typed_local");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("reorder_typed_local_idx", initialStart: UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void Reorder_TypedBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using ReorderTypedLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(ReorderTypedLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ReorderPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region Reorder_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class ReorderTypedUnspecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderTypedUnspecifiedContext : DbContext
    {
        public DbSet<ReorderTypedUnspecifiedEntity> Metrics => Set<ReorderTypedUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderTypedUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reorder_typed_unspecified");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy(
                    "reorder_typed_unspecified_idx",
                    initialStart: new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void Reorder_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using ReorderTypedUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(ReorderTypedUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ReorderPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    #region Reorder_TypedBuilder_Utc_Stores_Unchanged

    private class ReorderTypedUtcEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderTypedUtcContext : DbContext
    {
        public DbSet<ReorderTypedUtcEntity> Metrics => Set<ReorderTypedUtcEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderTypedUtcEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reorder_typed_utc");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("reorder_typed_utc_idx", initialStart: UtcInstant);
            });
        }
    }

    [Fact]
    public void Reorder_TypedBuilder_Utc_Stores_Unchanged()
    {
        // Arrange
        using ReorderTypedUtcContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(ReorderTypedUtcEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ReorderPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    // ── Reorder policy: string builder ─────────────────────────────────────────

    #region Reorder_StringBuilder_Local_Stores_Utc_Instant

    private class ReorderStringLocalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderStringLocalContext : DbContext
    {
        public DbSet<ReorderStringLocalEntity> Metrics => Set<ReorderStringLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderStringLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reorder_string_local");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("reorder_string_local_idx", (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void Reorder_StringBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using ReorderStringLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(ReorderStringLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ReorderPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region Reorder_StringBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class ReorderStringUnspecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderStringUnspecifiedContext : DbContext
    {
        public DbSet<ReorderStringUnspecifiedEntity> Metrics => Set<ReorderStringUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderStringUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reorder_string_unspecified");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("reorder_string_unspecified_idx", (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void Reorder_StringBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using ReorderStringUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(ReorderStringUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ReorderPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    // ── Retention policy: typed builder ────────────────────────────────────────

    #region Retention_TypedBuilder_Local_Stores_Utc_Instant

    private class RetentionTypedLocalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionTypedLocalContext : DbContext
    {
        public DbSet<RetentionTypedLocalEntity> Metrics => Set<RetentionTypedLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionTypedLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("retention_typed_local");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days", initialStart: UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void Retention_TypedBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using RetentionTypedLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(RetentionTypedLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, RetentionPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region Retention_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class RetentionTypedUnspecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionTypedUnspecifiedContext : DbContext
    {
        public DbSet<RetentionTypedUnspecifiedEntity> Metrics => Set<RetentionTypedUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionTypedUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("retention_typed_unspecified");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    initialStart: new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void Retention_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using RetentionTypedUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(RetentionTypedUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, RetentionPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    #region Retention_TypedBuilder_Utc_Stores_Unchanged

    private class RetentionTypedUtcEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionTypedUtcContext : DbContext
    {
        public DbSet<RetentionTypedUtcEntity> Metrics => Set<RetentionTypedUtcEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionTypedUtcEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("retention_typed_utc");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days", initialStart: UtcInstant);
            });
        }
    }

    [Fact]
    public void Retention_TypedBuilder_Utc_Stores_Unchanged()
    {
        // Arrange
        using RetentionTypedUtcContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(RetentionTypedUtcEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, RetentionPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    // ── Retention policy: string builder ───────────────────────────────────────

    #region Retention_StringBuilder_Local_Stores_Utc_Instant

    private class RetentionStringLocalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionStringLocalContext : DbContext
    {
        public DbSet<RetentionStringLocalEntity> Metrics => Set<RetentionStringLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionStringLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("retention_string_local");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy("7 days", (string?)null, (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void Retention_StringBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using RetentionStringLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(RetentionStringLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, RetentionPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region Retention_StringBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class RetentionStringUnspecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionStringUnspecifiedContext : DbContext
    {
        public DbSet<RetentionStringUnspecifiedEntity> Metrics => Set<RetentionStringUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionStringUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("retention_string_unspecified");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy("7 days", (string?)null, (string?)null, (string?)null, (int?)null, (string?)null)
                      .WithInitialStart(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void Retention_StringBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using RetentionStringUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(RetentionStringUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, RetentionPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    // ── Compression policy: typed builder ──────────────────────────────────────

    #region Compression_TypedBuilder_Local_Stores_Utc_Instant

    private class CompressionTypedLocalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionTypedLocalContext : DbContext
    {
        public DbSet<CompressionTypedLocalEntity> Metrics => Set<CompressionTypedLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionTypedLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_typed_local");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days", initialStart: UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void Compression_TypedBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using CompressionTypedLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CompressionTypedLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, CompressionPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region Compression_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class CompressionTypedUnspecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionTypedUnspecifiedContext : DbContext
    {
        public DbSet<CompressionTypedUnspecifiedEntity> Metrics => Set<CompressionTypedUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionTypedUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_typed_unspecified");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(
                    after: "7 days",
                    initialStart: new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void Compression_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using CompressionTypedUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CompressionTypedUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, CompressionPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    #region Compression_TypedBuilder_Utc_Stores_Unchanged

    private class CompressionTypedUtcEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionTypedUtcContext : DbContext
    {
        public DbSet<CompressionTypedUtcEntity> Metrics => Set<CompressionTypedUtcEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionTypedUtcEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_typed_utc");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days", initialStart: UtcInstant);
            });
        }
    }

    [Fact]
    public void Compression_TypedBuilder_Utc_Stores_Unchanged()
    {
        // Arrange
        using CompressionTypedUtcContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CompressionTypedUtcEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, CompressionPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    // ── Compression policy: string builder ─────────────────────────────────────

    #region Compression_StringBuilder_Local_Stores_Utc_Instant

    private class CompressionStringLocalEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionStringLocalContext : DbContext
    {
        public DbSet<CompressionStringLocalEntity> Metrics => Set<CompressionStringLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionStringLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_string_local");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy("7 days", (string?)null, (string?)null, (string?)null, (bool?)null)
                      .WithInitialStart(UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void Compression_StringBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using CompressionStringLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CompressionStringLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, CompressionPolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region Compression_StringBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class CompressionStringUnspecifiedEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionStringUnspecifiedContext : DbContext
    {
        public DbSet<CompressionStringUnspecifiedEntity> Metrics => Set<CompressionStringUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionStringUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_string_unspecified");
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy("7 days", (string?)null, (string?)null, (string?)null, (bool?)null)
                      .WithInitialStart(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void Compression_StringBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using CompressionStringUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CompressionStringUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, CompressionPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    // ── Continuous aggregate policy: typed builder ─────────────────────────────

    #region CAggPolicy_TypedBuilder_Local_Stores_Utc_Instant

    private class CAggMetricSourceLocal
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CAggViewLocalEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CAggPolicyLocalContext : DbContext
    {
        public DbSet<CAggMetricSourceLocal> Metrics => Set<CAggMetricSourceLocal>();
        public DbSet<CAggViewLocalEntity> Aggregates => Set<CAggViewLocalEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CAggMetricSourceLocal>(entity =>
            {
                entity.ToTable("cagg_policy_local_src");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CAggViewLocalEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CAggViewLocalEntity, CAggMetricSourceLocal>(
                        "cagg_policy_local", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithInitialStart(UtcInstant.ToLocalTime());
            });
        }
    }

    [Fact]
    public void CAggPolicy_TypedBuilder_Local_Stores_Utc_Instant()
    {
        // Arrange
        using CAggPolicyLocalContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CAggViewLocalEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ContinuousAggregatePolicyAnnotations.InitialStart);
        Assert.Equal(UtcInstant, stored);
    }

    #endregion

    #region CAggPolicy_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc

    private class CAggMetricSourceUnspecified
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CAggViewUnspecifiedEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CAggPolicyUnspecifiedContext : DbContext
    {
        public DbSet<CAggMetricSourceUnspecified> Metrics => Set<CAggMetricSourceUnspecified>();
        public DbSet<CAggViewUnspecifiedEntity> Aggregates => Set<CAggViewUnspecifiedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CAggMetricSourceUnspecified>(entity =>
            {
                entity.ToTable("cagg_policy_unspecified_src");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CAggViewUnspecifiedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CAggViewUnspecifiedEntity, CAggMetricSourceUnspecified>(
                        "cagg_policy_unspecified", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithInitialStart(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Unspecified));
            });
        }
    }

    [Fact]
    public void CAggPolicy_TypedBuilder_Unspecified_Stores_SpecifyKind_Utc()
    {
        // Arrange
        using CAggPolicyUnspecifiedContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CAggViewUnspecifiedEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ContinuousAggregatePolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion
}

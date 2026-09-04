using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

/// <summary>
/// Tests that verify every policy differ compares InitialStart kind-insensitively: a Local-kind
/// annotation and the equivalent Utc-kind annotation must not produce an alter/recreate operation,
/// while a genuinely different instant must.
/// </summary>
public class PolicyInitialStartDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    private static readonly DateTime UtcInstant = new(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc);
    private static readonly DateTime LocalEquivalent = UtcInstant.ToLocalTime();
    private static readonly DateTime DifferentUtcInstant = new(2026, 3, 14, 1, 2, 3, DateTimeKind.Utc);

    // ── Reorder policy differ ──────────────────────────────────────────────────

    #region Reorder_Local_Vs_Equivalent_Utc_Emits_No_Alter

    private class ReorderMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderLocalSourceContext1 : DbContext
    {
        public DbSet<ReorderMetric1> Metrics => Set<ReorderMetric1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderMetric1>(entity =>
            {
                entity.ToTable("reorder_diff_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                entity.HasAnnotation(ReorderPolicyAnnotations.IndexName, "reorder_diff_idx");
                entity.HasAnnotation(ReorderPolicyAnnotations.InitialStart, LocalEquivalent);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("reorder_diff_idx");
            });
        }
    }

    private class ReorderUtcTargetContext1 : DbContext
    {
        public DbSet<ReorderMetric1> Metrics => Set<ReorderMetric1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderMetric1>(entity =>
            {
                entity.ToTable("reorder_diff_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                entity.HasAnnotation(ReorderPolicyAnnotations.IndexName, "reorder_diff_idx");
                entity.HasAnnotation(ReorderPolicyAnnotations.InitialStart, UtcInstant);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("reorder_diff_idx");
            });
        }
    }

    [Fact]
    public void Reorder_Local_Vs_Equivalent_Utc_Emits_No_Alter()
    {
        // Arrange
        using ReorderLocalSourceContext1 sourceContext = new();
        using ReorderUtcTargetContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        ReorderPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations.OfType<AlterReorderPolicyOperation>());
    }

    #endregion

    #region Reorder_Different_Instant_Emits_Alter

    private class ReorderMetric2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderSourceContext2 : DbContext
    {
        public DbSet<ReorderMetric2> Metrics => Set<ReorderMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderMetric2>(entity =>
            {
                entity.ToTable("reorder_diff2_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                entity.HasAnnotation(ReorderPolicyAnnotations.IndexName, "reorder_diff2_idx");
                entity.HasAnnotation(ReorderPolicyAnnotations.InitialStart, UtcInstant);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("reorder_diff2_idx");
            });
        }
    }

    private class ReorderTargetContext2 : DbContext
    {
        public DbSet<ReorderMetric2> Metrics => Set<ReorderMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderMetric2>(entity =>
            {
                entity.ToTable("reorder_diff2_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                entity.HasAnnotation(ReorderPolicyAnnotations.IndexName, "reorder_diff2_idx");
                entity.HasAnnotation(ReorderPolicyAnnotations.InitialStart, DifferentUtcInstant);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("reorder_diff2_idx");
            });
        }
    }

    [Fact]
    public void Reorder_Different_Instant_Emits_Alter()
    {
        // Arrange
        using ReorderSourceContext2 sourceContext = new();
        using ReorderTargetContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        ReorderPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterReorderPolicyOperation? alterOp = operations.OfType<AlterReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(DifferentUtcInstant, alterOp.InitialStart);
    }

    #endregion

    // ── Retention policy differ ────────────────────────────────────────────────

    #region Retention_Local_Vs_Equivalent_Utc_Emits_No_Alter

    private class RetentionMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionLocalSourceContext1 : DbContext
    {
        public DbSet<RetentionMetric1> Metrics => Set<RetentionMetric1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionMetric1>(entity =>
            {
                entity.ToTable("retention_diff_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                entity.HasAnnotation(RetentionPolicyAnnotations.InitialStart, LocalEquivalent);
            });
        }
    }

    private class RetentionUtcTargetContext1 : DbContext
    {
        public DbSet<RetentionMetric1> Metrics => Set<RetentionMetric1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionMetric1>(entity =>
            {
                entity.ToTable("retention_diff_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                entity.HasAnnotation(RetentionPolicyAnnotations.InitialStart, UtcInstant);
            });
        }
    }

    [Fact]
    public void Retention_Local_Vs_Equivalent_Utc_Emits_No_Alter()
    {
        // Arrange
        using RetentionLocalSourceContext1 sourceContext = new();
        using RetentionUtcTargetContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        RetentionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations.OfType<AlterRetentionPolicyOperation>());
    }

    #endregion

    #region Retention_Different_Instant_Emits_Alter

    private class RetentionMetric2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionSourceContext2 : DbContext
    {
        public DbSet<RetentionMetric2> Metrics => Set<RetentionMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionMetric2>(entity =>
            {
                entity.ToTable("retention_diff2_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                entity.HasAnnotation(RetentionPolicyAnnotations.InitialStart, UtcInstant);
            });
        }
    }

    private class RetentionTargetContext2 : DbContext
    {
        public DbSet<RetentionMetric2> Metrics => Set<RetentionMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionMetric2>(entity =>
            {
                entity.ToTable("retention_diff2_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                entity.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                entity.HasAnnotation(RetentionPolicyAnnotations.InitialStart, DifferentUtcInstant);
            });
        }
    }

    [Fact]
    public void Retention_Different_Instant_Emits_Alter()
    {
        // Arrange
        using RetentionSourceContext2 sourceContext = new();
        using RetentionTargetContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        RetentionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(DifferentUtcInstant, alterOp.InitialStart);
    }

    #endregion

    // ── Compression policy differ ──────────────────────────────────────────────

    #region Compression_Local_Vs_Equivalent_Utc_Emits_No_Alter

    private class CompressionMetric1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionLocalSourceContext1 : DbContext
    {
        public DbSet<CompressionMetric1> Metrics => Set<CompressionMetric1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionMetric1>(entity =>
            {
                entity.ToTable("compression_diff_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);
                entity.HasAnnotation(CompressionPolicyAnnotations.After, "7 days");
                entity.HasAnnotation(CompressionPolicyAnnotations.InitialStart, LocalEquivalent);
            });
        }
    }

    private class CompressionUtcTargetContext1 : DbContext
    {
        public DbSet<CompressionMetric1> Metrics => Set<CompressionMetric1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionMetric1>(entity =>
            {
                entity.ToTable("compression_diff_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);
                entity.HasAnnotation(CompressionPolicyAnnotations.After, "7 days");
                entity.HasAnnotation(CompressionPolicyAnnotations.InitialStart, UtcInstant);
            });
        }
    }

    [Fact]
    public void Compression_Local_Vs_Equivalent_Utc_Emits_No_Alter()
    {
        // Arrange
        using CompressionLocalSourceContext1 sourceContext = new();
        using CompressionUtcTargetContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations.OfType<AlterCompressionPolicyOperation>());
    }

    #endregion

    #region Compression_Different_Instant_Emits_Alter

    private class CompressionMetric2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionSourceContext2 : DbContext
    {
        public DbSet<CompressionMetric2> Metrics => Set<CompressionMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionMetric2>(entity =>
            {
                entity.ToTable("compression_diff2_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);
                entity.HasAnnotation(CompressionPolicyAnnotations.After, "7 days");
                entity.HasAnnotation(CompressionPolicyAnnotations.InitialStart, UtcInstant);
            });
        }
    }

    private class CompressionTargetContext2 : DbContext
    {
        public DbSet<CompressionMetric2> Metrics => Set<CompressionMetric2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionMetric2>(entity =>
            {
                entity.ToTable("compression_diff2_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);
                entity.HasAnnotation(CompressionPolicyAnnotations.After, "7 days");
                entity.HasAnnotation(CompressionPolicyAnnotations.InitialStart, DifferentUtcInstant);
            });
        }
    }

    [Fact]
    public void Compression_Different_Instant_Emits_Alter()
    {
        // Arrange
        using CompressionSourceContext2 sourceContext = new();
        using CompressionTargetContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(DifferentUtcInstant, alterOp.InitialStart);
    }

    #endregion

    // ── Continuous aggregate policy differ ─────────────────────────────────────

    #region CAggPolicy_Local_Vs_Equivalent_Utc_Emits_No_Operations

    private class CAggSource1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CAggView1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CAggLocalSourceContext1 : DbContext
    {
        public DbSet<CAggSource1> Metrics => Set<CAggSource1>();
        public DbSet<CAggView1> Aggregates => Set<CAggView1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CAggSource1>(entity =>
            {
                entity.ToTable("cagg_diff_src");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CAggView1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CAggView1, CAggSource1>(
                        "cagg_diff_view", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart, LocalEquivalent);
            });
        }
    }

    private class CAggUtcTargetContext1 : DbContext
    {
        public DbSet<CAggSource1> Metrics => Set<CAggSource1>();
        public DbSet<CAggView1> Aggregates => Set<CAggView1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CAggSource1>(entity =>
            {
                entity.ToTable("cagg_diff_src");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CAggView1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CAggView1, CAggSource1>(
                        "cagg_diff_view", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart, UtcInstant);
            });
        }
    }

    [Fact]
    public void CAggPolicy_Local_Vs_Equivalent_Utc_Emits_No_Operations()
    {
        // Arrange
        using CAggLocalSourceContext1 sourceContext = new();
        using CAggUtcTargetContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations.OfType<RemoveContinuousAggregatePolicyOperation>());
        Assert.Empty(operations.OfType<AddContinuousAggregatePolicyOperation>());
    }

    #endregion

    #region CAggPolicy_Different_Instant_Emits_Recreate

    private class CAggSource2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CAggView2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CAggSourceContext2 : DbContext
    {
        public DbSet<CAggSource2> Metrics => Set<CAggSource2>();
        public DbSet<CAggView2> Aggregates => Set<CAggView2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CAggSource2>(entity =>
            {
                entity.ToTable("cagg_diff2_src");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CAggView2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CAggView2, CAggSource2>(
                        "cagg_diff2_view", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart, UtcInstant);
            });
        }
    }

    private class CAggTargetContext2 : DbContext
    {
        public DbSet<CAggSource2> Metrics => Set<CAggSource2>();
        public DbSet<CAggView2> Aggregates => Set<CAggView2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CAggSource2>(entity =>
            {
                entity.ToTable("cagg_diff2_src");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CAggView2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CAggView2, CAggSource2>(
                        "cagg_diff2_view", "1 hour", x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart, DifferentUtcInstant);
            });
        }
    }

    [Fact]
    public void CAggPolicy_Different_Instant_Emits_Recreate()
    {
        // Arrange
        using CAggSourceContext2 sourceContext = new();
        using CAggTargetContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.NotEmpty(operations.OfType<RemoveContinuousAggregatePolicyOperation>());
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal(DifferentUtcInstant, addOp.InitialStart);
    }

    #endregion
}

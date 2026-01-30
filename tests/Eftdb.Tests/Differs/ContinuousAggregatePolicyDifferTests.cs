using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class ContinuousAggregatePolicyDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region Should_Detect_New_Policy

    private class MetricEntity1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithoutPolicyContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();
        public DbSet<AggregateEntity1> Aggregates => Set<AggregateEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity1>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity1, MetricEntity1>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class WithPolicyContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();
        public DbSet<AggregateEntity1> Aggregates => Set<AggregateEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity1>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity1>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity1, MetricEntity1>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Detect_New_Policy()
    {
        // Arrange
        using WithoutPolicyContext1 sourceContext = new();
        using WithPolicyContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("hourly_metrics", addOp.MaterializedViewName);
        Assert.Equal("1 month", addOp.StartOffset);
        Assert.Equal("1 hour", addOp.EndOffset);
        Assert.Equal("1 hour", addOp.ScheduleInterval);
    }

    #endregion

    #region Should_Detect_Removed_Policy

    private class MetricEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithPolicyContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<AggregateEntity2> Aggregates => Set<AggregateEntity2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity2>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity2, MetricEntity2>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    private class WithoutPolicyContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<AggregateEntity2> Aggregates => Set<AggregateEntity2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity2>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity2>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity2, MetricEntity2>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Detect_Removed_Policy()
    {
        // Arrange
        using WithPolicyContext2 sourceContext = new();
        using WithoutPolicyContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        RemoveContinuousAggregatePolicyOperation? removeOp = operations.OfType<RemoveContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(removeOp);
        Assert.Equal("hourly_metrics", removeOp.MaterializedViewName);
        Assert.True(removeOp.IfExists);
    }

    #endregion

    #region Should_Detect_Modified_StartOffset

    private class MetricEntity3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity3
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<AggregateEntity3> Aggregates => Set<AggregateEntity3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity3>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity3>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity3, MetricEntity3>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    private class ModifiedContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<AggregateEntity3> Aggregates => Set<AggregateEntity3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity3>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity3>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity3, MetricEntity3>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "1 hour"); // <-- Changed from "1 month"
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_StartOffset()
    {
        // Arrange
        using OriginalContext3 sourceContext = new();
        using ModifiedContext3 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        RemoveContinuousAggregatePolicyOperation? removeOp = operations.OfType<RemoveContinuousAggregatePolicyOperation>().FirstOrDefault();
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(removeOp);
        Assert.NotNull(addOp);
        Assert.Equal("7 days", addOp.StartOffset);
    }

    #endregion

    #region Should_Detect_Modified_EndOffset

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();
        public DbSet<AggregateEntity4> Aggregates => Set<AggregateEntity4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity4>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity4, MetricEntity4>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    private class ModifiedContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();
        public DbSet<AggregateEntity4> Aggregates => Set<AggregateEntity4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity4>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity4, MetricEntity4>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "30 minutes", scheduleInterval: "1 hour"); // <-- Changed from "1 hour"
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_EndOffset()
    {
        // Arrange
        using OriginalContext4 sourceContext = new();
        using ModifiedContext4 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("30 minutes", addOp.EndOffset);
    }

    #endregion

    #region Should_Detect_Modified_ScheduleInterval

    private class MetricEntity5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity5
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();
        public DbSet<AggregateEntity5> Aggregates => Set<AggregateEntity5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity5>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity5, MetricEntity5>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    private class ModifiedContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();
        public DbSet<AggregateEntity5> Aggregates => Set<AggregateEntity5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity5>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity5, MetricEntity5>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "30 minutes"); // <-- Changed from "1 hour"
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_ScheduleInterval()
    {
        // Arrange
        using OriginalContext5 sourceContext = new();
        using ModifiedContext5 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("30 minutes", addOp.ScheduleInterval);
    }

    #endregion

    #region Should_Detect_Modified_InitialStart

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity7
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();
        public DbSet<AggregateEntity7> Aggregates => Set<AggregateEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity7>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity7>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity7, MetricEntity7>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithInitialStart(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    private class ModifiedContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();
        public DbSet<AggregateEntity7> Aggregates => Set<AggregateEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity7>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity7>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity7, MetricEntity7>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithInitialStart(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)); // <-- Changed from 2025-01-01
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_InitialStart()
    {
        // Arrange
        using OriginalContext7 sourceContext = new();
        using ModifiedContext7 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.NotNull(addOp.InitialStart);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), addOp.InitialStart.Value.ToUniversalTime());
    }

    #endregion

    #region Should_Detect_Modified_IncludeTieredData

    private class MetricEntity8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();
        public DbSet<AggregateEntity8> Aggregates => Set<AggregateEntity8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity8>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity8, MetricEntity8>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithIncludeTieredData(false);
            });
        }
    }

    private class ModifiedContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();
        public DbSet<AggregateEntity8> Aggregates => Set<AggregateEntity8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity8>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity8, MetricEntity8>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithIncludeTieredData(true); // <-- Changed from false
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_IncludeTieredData()
    {
        // Arrange
        using OriginalContext8 sourceContext = new();
        using ModifiedContext8 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.True(addOp.IncludeTieredData);
    }

    #endregion

    #region Should_Detect_Modified_BucketsPerBatch

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity9
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();
        public DbSet<AggregateEntity9> Aggregates => Set<AggregateEntity9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity9>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity9>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity9, MetricEntity9>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithBucketsPerBatch(5);
            });
        }
    }

    private class ModifiedContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();
        public DbSet<AggregateEntity9> Aggregates => Set<AggregateEntity9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity9>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity9>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity9, MetricEntity9>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithBucketsPerBatch(10); // <-- Changed from 5
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_BucketsPerBatch()
    {
        // Arrange
        using OriginalContext9 sourceContext = new();
        using ModifiedContext9 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal(10, addOp.BucketsPerBatch);
    }

    #endregion

    #region Should_Detect_Modified_MaxBatchesPerExecution

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity10
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();
        public DbSet<AggregateEntity10> Aggregates => Set<AggregateEntity10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity10>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity10>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity10, MetricEntity10>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithMaxBatchesPerExecution(5);
            });
        }
    }

    private class ModifiedContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();
        public DbSet<AggregateEntity10> Aggregates => Set<AggregateEntity10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity10>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity10>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity10, MetricEntity10>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithMaxBatchesPerExecution(10); // <-- Changed from 5
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_MaxBatchesPerExecution()
    {
        // Arrange
        using OriginalContext10 sourceContext = new();
        using ModifiedContext10 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal(10, addOp.MaxBatchesPerExecution);
    }

    #endregion

    #region Should_Detect_Modified_RefreshNewestFirst

    private class MetricEntity11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity11
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OriginalContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();
        public DbSet<AggregateEntity11> Aggregates => Set<AggregateEntity11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity11>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity11>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity11, MetricEntity11>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithRefreshNewestFirst(true);
            });
        }
    }

    private class ModifiedContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();
        public DbSet<AggregateEntity11> Aggregates => Set<AggregateEntity11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity11>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity11>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity11, MetricEntity11>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour")
                    .WithRefreshNewestFirst(false); // <-- Changed from true
            });
        }
    }

    [Fact]
    public void Should_Detect_Modified_RefreshNewestFirst()
    {
        // Arrange
        using OriginalContext11 sourceContext = new();
        using ModifiedContext11 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.False(addOp.RefreshNewestFirst);
    }

    #endregion

    #region Should_Return_Empty_When_No_Changes

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity12
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class UnchangedContext12 : DbContext
    {
        public DbSet<MetricEntity12> Metrics => Set<MetricEntity12>();
        public DbSet<AggregateEntity12> Aggregates => Set<AggregateEntity12>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity12>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity12>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity12, MetricEntity12>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_Changes()
    {
        // Arrange
        using UnchangedContext12 sourceContext = new();
        using UnchangedContext12 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Handle_Null_Source_Model

    private class MetricEntity13
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity13
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithPolicyContext13 : DbContext
    {
        public DbSet<MetricEntity13> Metrics => Set<MetricEntity13>();
        public DbSet<AggregateEntity13> Aggregates => Set<AggregateEntity13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity13>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity13>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity13, MetricEntity13>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Source_Model()
    {
        // Arrange
        using WithPolicyContext13 targetContext = new();
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, targetModel);

        // Assert
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("hourly_metrics", addOp.MaterializedViewName);
    }

    #endregion

    #region Should_Handle_Null_Target_Model

    private class MetricEntity14
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity14
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WithPolicyContext14 : DbContext
    {
        public DbSet<MetricEntity14> Metrics => Set<MetricEntity14>();
        public DbSet<AggregateEntity14> Aggregates => Set<AggregateEntity14>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity14>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity14, MetricEntity14>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Target_Model()
    {
        // Arrange
        using WithPolicyContext14 sourceContext = new();
        IRelationalModel sourceModel = GetModel(sourceContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, null);

        // Assert
        RemoveContinuousAggregatePolicyOperation? removeOp = operations.OfType<RemoveContinuousAggregatePolicyOperation>().FirstOrDefault();
        Assert.NotNull(removeOp);
        Assert.Equal("hourly_metrics", removeOp.MaterializedViewName);
    }

    #endregion

    #region Should_Detect_Multiple_Policy_Changes

    private class MetricEntity15a
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AggregateEntity15a
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MetricEntity15b
    {
        public DateTime Timestamp { get; set; }
        public double Temperature { get; set; }
    }

    private class AggregateEntity15b
    {
        public DateTime TimeBucket { get; set; }
        public double AvgTemperature { get; set; }
    }

    private class MultipleOriginalContext15 : DbContext
    {
        public DbSet<MetricEntity15a> Metrics => Set<MetricEntity15a>();
        public DbSet<AggregateEntity15a> MetricAggregates => Set<AggregateEntity15a>();
        public DbSet<MetricEntity15b> Weather => Set<MetricEntity15b>();
        public DbSet<AggregateEntity15b> WeatherAggregates => Set<AggregateEntity15b>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15a>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity15a>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity15a, MetricEntity15a>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });

            modelBuilder.Entity<MetricEntity15b>(entity =>
            {
                entity.ToTable("Weather");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity15b>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity15b, MetricEntity15b>(
                        "hourly_weather",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgTemperature, x => x.Temperature, EAggregateFunction.Avg);
            });
        }
    }

    private class MultipleModifiedContext15 : DbContext
    {
        public DbSet<MetricEntity15a> Metrics => Set<MetricEntity15a>();
        public DbSet<AggregateEntity15a> MetricAggregates => Set<AggregateEntity15a>();
        public DbSet<MetricEntity15b> Weather => Set<MetricEntity15b>();
        public DbSet<AggregateEntity15b> WeatherAggregates => Set<AggregateEntity15b>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15a>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity15a>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity15a, MetricEntity15a>(
                        "hourly_metrics",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg); // <-- Policy removed
            });

            modelBuilder.Entity<MetricEntity15b>(entity =>
            {
                entity.ToTable("Weather");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<AggregateEntity15b>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<AggregateEntity15b, MetricEntity15b>(
                        "hourly_weather",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgTemperature, x => x.Temperature, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "30 minutes", scheduleInterval: "30 minutes"); // <-- Policy added
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Policy_Changes()
    {
        // Arrange
        using MultipleOriginalContext15 sourceContext = new();
        using MultipleModifiedContext15 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Equal(2, operations.Count);

        RemoveContinuousAggregatePolicyOperation? removeOp = operations.OfType<RemoveContinuousAggregatePolicyOperation>()
            .FirstOrDefault(op => op.MaterializedViewName == "hourly_metrics");
        Assert.NotNull(removeOp);

        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>()
            .FirstOrDefault(op => op.MaterializedViewName == "hourly_weather");
        Assert.NotNull(addOp);
        Assert.Equal("7 days", addOp.StartOffset);
    }

    #endregion
}

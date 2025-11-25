using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class ReorderPolicyDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region Should_Detect_New_ReorderPolicy

    private class MetricEntity1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HypertableWithoutPolicyContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();

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
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class BasicReorderPolicyContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_New_ReorderPolicy()
    {
        using HypertableWithoutPolicyContext1 sourceContext = new();
        using BasicReorderPolicyContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AddReorderPolicyOperation? addOp = operations.OfType<AddReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("Metrics", addOp.TableName);
        Assert.Equal("metrics_time_idx", addOp.IndexName);
        Assert.Equal("1 day", addOp.ScheduleInterval);
    }

    #endregion

    #region Should_Detect_Multiple_New_Policies

    private class MetricEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LogEntity2
    {
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
    }

    private class MultipleHypertablesContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<LogEntity2> Logs => Set<LogEntity2>();

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
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });

            modelBuilder.Entity<LogEntity2>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("logs_time_idx");
            });
        }
    }

    private class MultipleReorderPoliciesContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<LogEntity2> Logs => Set<LogEntity2>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });

            modelBuilder.Entity<LogEntity2>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("logs_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("logs_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_New_Policies()
    {
        using MultipleHypertablesContext2 sourceContext = new();
        using MultipleReorderPoliciesContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<AddReorderPolicyOperation> addOps = [.. operations.OfType<AddReorderPolicyOperation>()];
        Assert.Equal(2, addOps.Count);
        Assert.Contains(addOps, op => op.TableName == "Metrics");
        Assert.Contains(addOps, op => op.TableName == "Logs");
    }

    #endregion

    #region Should_Detect_Policy_With_Custom_Parameters

    private class MetricEntity3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HypertableWithoutPolicyContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();

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
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class CustomScheduleReorderPolicyContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    scheduleInterval: "12:00:00",
                    maxRuntime: "01:00:00",
                    maxRetries: 5,
                    retryPeriod: "00:10:00"
                );
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_Policy_With_Custom_Parameters()
    {
        using HypertableWithoutPolicyContext3 sourceContext = new();
        using CustomScheduleReorderPolicyContext3 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AddReorderPolicyOperation? addOp = operations.OfType<AddReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("12:00:00", addOp.ScheduleInterval);
        Assert.Equal("01:00:00", addOp.MaxRuntime);
        Assert.Equal(5, addOp.MaxRetries);
        Assert.Equal("00:10:00", addOp.RetryPeriod);
    }

    #endregion

    #region Should_Detect_ScheduleInterval_Change

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class CustomScheduleReorderPolicyContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    scheduleInterval: "12:00:00",
                    maxRuntime: "01:00:00",
                    maxRetries: 5,
                    retryPeriod: "00:10:00"
                );
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_ScheduleInterval_Change()
    {
        using BasicReorderPolicyContext4 sourceContext = new();
        using CustomScheduleReorderPolicyContext4 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterReorderPolicyOperation? alterOp = operations.OfType<AlterReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("1 day", alterOp.OldScheduleInterval);
        Assert.Equal("12:00:00", alterOp.ScheduleInterval);
    }

    #endregion

    #region Should_Detect_IndexName_Change

    private class MetricEntity5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class DifferentIndexReorderPolicyContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();

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
                entity.WithReorderPolicy("metrics_timestamp_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_timestamp_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_IndexName_Change()
    {
        using BasicReorderPolicyContext5 sourceContext = new();
        using DifferentIndexReorderPolicyContext5 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterReorderPolicyOperation? alterOp = operations.OfType<AlterReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("metrics_time_idx", alterOp.OldIndexName);
        Assert.Equal("metrics_timestamp_idx", alterOp.IndexName);
    }

    #endregion

    #region Should_Detect_MaxRetries_Change

    private class MetricEntity6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomScheduleReorderPolicyContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity6>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    scheduleInterval: "12:00:00",
                    maxRuntime: "01:00:00",
                    maxRetries: 5,
                    retryPeriod: "00:10:00"
                );
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class ModifiedMaxRetriesContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity6>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    scheduleInterval: "12:00:00",
                    maxRuntime: "01:00:00",
                    maxRetries: 10,
                    retryPeriod: "00:10:00"
                );
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_MaxRetries_Change()
    {
        using CustomScheduleReorderPolicyContext6 sourceContext = new();
        using ModifiedMaxRetriesContext6 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterReorderPolicyOperation? alterOp = operations.OfType<AlterReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(5, alterOp.OldMaxRetries);
        Assert.Equal(10, alterOp.MaxRetries);
    }

    #endregion

    #region Should_Detect_Multiple_Parameter_Changes

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class FullyCustomReorderPolicyContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();

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
                entity.WithReorderPolicy(
                    indexName: "metrics_time_idx",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    scheduleInterval: "06:00:00",
                    maxRuntime: "02:00:00",
                    maxRetries: 3,
                    retryPeriod: "00:15:00"
                );
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Parameter_Changes()
    {
        using BasicReorderPolicyContext7 sourceContext = new();
        using FullyCustomReorderPolicyContext7 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterReorderPolicyOperation? alterOp = operations.OfType<AlterReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotEqual(alterOp.OldScheduleInterval, alterOp.ScheduleInterval);
        Assert.NotEqual(alterOp.OldMaxRuntime, alterOp.MaxRuntime);
        Assert.NotEqual(alterOp.OldMaxRetries, alterOp.MaxRetries);
        Assert.NotEqual(alterOp.OldRetryPeriod, alterOp.RetryPeriod);
    }

    #endregion

    #region Should_Detect_Dropped_ReorderPolicy

    private class MetricEntity8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    private class HypertableWithoutPolicyContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();

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
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_Dropped_ReorderPolicy()
    {
        using BasicReorderPolicyContext8 sourceContext = new();
        using HypertableWithoutPolicyContext8 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        DropReorderPolicyOperation? dropOp = operations.OfType<DropReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
        Assert.Equal("Metrics", dropOp.TableName);
    }

    #endregion

    #region Should_Detect_Multiple_Dropped_Policies

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LogEntity9
    {
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
    }

    private class MultipleReorderPoliciesContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();
        public DbSet<LogEntity9> Logs => Set<LogEntity9>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });

            modelBuilder.Entity<LogEntity9>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("logs_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("logs_time_idx");
            });
        }
    }

    private class MultipleHypertablesContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();
        public DbSet<LogEntity9> Logs => Set<LogEntity9>();

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
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });

            modelBuilder.Entity<LogEntity9>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("logs_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Dropped_Policies()
    {
        using MultipleReorderPoliciesContext9 sourceContext = new();
        using MultipleHypertablesContext9 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<DropReorderPolicyOperation> dropOps = [.. operations.OfType<DropReorderPolicyOperation>()];
        Assert.Equal(2, dropOps.Count);
    }

    #endregion

    #region Should_Not_Generate_Operations_When_No_Changes

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operations_When_No_Changes()
    {
        using BasicReorderPolicyContext10 sourceContext = new();
        using BasicReorderPolicyContext10 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Handle_Null_Source_Model

    private class MetricEntity11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Source_Model()
    {
        using BasicReorderPolicyContext11 targetContext = new();
        IRelationalModel targetModel = GetModel(targetContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, targetModel);

        AddReorderPolicyOperation? addOp = operations.OfType<AddReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
    }

    #endregion

    #region Should_Handle_Null_Target_Model

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicReorderPolicyContext12 : DbContext
    {
        public DbSet<MetricEntity12> Metrics => Set<MetricEntity12>();

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
                entity.WithReorderPolicy("metrics_time_idx");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("metrics_time_idx");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Target_Model()
    {
        using BasicReorderPolicyContext12 sourceContext = new();
        IRelationalModel sourceModel = GetModel(sourceContext);

        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, null);

        DropReorderPolicyOperation? dropOp = operations.OfType<DropReorderPolicyOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
    }

    #endregion

    #region Should_Handle_Both_Null_Models

    [Fact]
    public void Should_Handle_Both_Null_Models()
    {
        ReorderPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, null);

        Assert.Empty(operations);
    }

    #endregion
}

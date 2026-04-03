using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class RetentionPolicyDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region Should_Detect_New_RetentionPolicy

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
            });
        }
    }

    private class RetentionPolicyContext1 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_New_RetentionPolicy()
    {
        using HypertableWithoutPolicyContext1 sourceContext = new();
        using RetentionPolicyContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AddRetentionPolicyOperation? addOp = operations.OfType<AddRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("Metrics", addOp.TableName);
        Assert.Equal("7 days", addOp.DropAfter);
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
            });

            modelBuilder.Entity<LogEntity2>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class MultipleRetentionPoliciesContext2 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });

            modelBuilder.Entity<LogEntity2>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "30 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_New_Policies()
    {
        using MultipleHypertablesContext2 sourceContext = new();
        using MultipleRetentionPoliciesContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<AddRetentionPolicyOperation> addOps = [.. operations.OfType<AddRetentionPolicyOperation>()];
        Assert.Equal(2, addOps.Count);
        Assert.Contains(addOps, op => op.TableName == "Metrics");
        Assert.Contains(addOps, op => op.TableName == "Logs");
    }

    #endregion

    #region Should_Detect_Policy_With_All_Custom_Parameters

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
            });
        }
    }

    private class FullyCustomRetentionPolicyContext3 : DbContext
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
                entity.WithRetentionPolicy(
                    dropAfter: "14 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    scheduleInterval: "12:00:00",
                    maxRuntime: "01:00:00",
                    maxRetries: 5,
                    retryPeriod: "00:10:00"
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_Policy_With_All_Custom_Parameters()
    {
        using HypertableWithoutPolicyContext3 sourceContext = new();
        using FullyCustomRetentionPolicyContext3 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AddRetentionPolicyOperation? addOp = operations.OfType<AddRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("14 days", addOp.DropAfter);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), addOp.InitialStart);
        Assert.Equal("12:00:00", addOp.ScheduleInterval);
        Assert.Equal("01:00:00", addOp.MaxRuntime);
        Assert.Equal(5, addOp.MaxRetries);
        Assert.Equal("00:10:00", addOp.RetryPeriod);
    }

    #endregion

    #region Should_Detect_DropAfter_Change

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext4 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class ModifiedDropAfterContext4 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "14 days"); // <-- Changed from "7 days"
            });
        }
    }

    [Fact]
    public void Should_Detect_DropAfter_Change()
    {
        using RetentionPolicyContext4 sourceContext = new();
        using ModifiedDropAfterContext4 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("7 days", alterOp.OldDropAfter);
        Assert.Equal("14 days", alterOp.DropAfter);
    }

    #endregion

    #region Should_Detect_DropMethod_Switch_DropAfter_To_DropCreatedBefore

    private class MetricEntity5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropAfterPolicyContext5 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class DropCreatedBeforePolicyContext5 : DbContext
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
                entity.WithRetentionPolicy(dropCreatedBefore: "30 days"); // <-- Changed from dropAfter: "7 days"
            });
        }
    }

    [Fact]
    public void Should_Detect_DropMethod_Switch_DropAfter_To_DropCreatedBefore()
    {
        using DropAfterPolicyContext5 sourceContext = new();
        using DropCreatedBeforePolicyContext5 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("7 days", alterOp.OldDropAfter);
        Assert.Null(alterOp.DropAfter);
        Assert.Null(alterOp.OldDropCreatedBefore);
        Assert.Equal("30 days", alterOp.DropCreatedBefore);
    }

    #endregion

    #region Should_Detect_ScheduleInterval_Change

    private class MetricEntity6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext6 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class ModifiedScheduleIntervalContext6 : DbContext
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
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    scheduleInterval: "12:00:00" // <-- Changed from default "1 day"
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_ScheduleInterval_Change()
    {
        using RetentionPolicyContext6 sourceContext = new();
        using ModifiedScheduleIntervalContext6 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("1 day", alterOp.OldScheduleInterval);
        Assert.Equal("12:00:00", alterOp.ScheduleInterval);
    }

    #endregion

    #region Should_Detect_MaxRetries_Change

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext7 : DbContext
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
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    maxRetries: 5
                );
            });
        }
    }

    private class ModifiedMaxRetriesContext7 : DbContext
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
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    maxRetries: 10 // <-- Changed from 5
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_MaxRetries_Change()
    {
        using RetentionPolicyContext7 sourceContext = new();
        using ModifiedMaxRetriesContext7 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(5, alterOp.OldMaxRetries);
        Assert.Equal(10, alterOp.MaxRetries);
    }

    #endregion

    #region Should_Detect_InitialStart_Change

    private class MetricEntity8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext8 : DbContext
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
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                );
            });
        }
    }

    private class ModifiedInitialStartContext8 : DbContext
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
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    initialStart: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) // <-- Changed from 2025-01-01
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_InitialStart_Change()
    {
        using RetentionPolicyContext8 sourceContext = new();
        using ModifiedInitialStartContext8 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), alterOp.OldInitialStart);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), alterOp.InitialStart);
    }

    #endregion

    #region Should_Detect_Multiple_Parameter_Changes

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();

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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class FullyCustomRetentionPolicyContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();

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
                entity.WithRetentionPolicy(
                    dropAfter: "30 days", // <-- Changed from "7 days"
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    scheduleInterval: "06:00:00", // <-- Changed from default "1 day"
                    maxRuntime: "02:00:00", // <-- Changed from default "00:00:00"
                    maxRetries: 3, // <-- Changed from default -1
                    retryPeriod: "00:15:00" // <-- Changed from default "1 day"
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Parameter_Changes()
    {
        using RetentionPolicyContext9 sourceContext = new();
        using FullyCustomRetentionPolicyContext9 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotEqual(alterOp.OldDropAfter, alterOp.DropAfter);
        Assert.NotEqual(alterOp.OldScheduleInterval, alterOp.ScheduleInterval);
        Assert.NotEqual(alterOp.OldMaxRuntime, alterOp.MaxRuntime);
        Assert.NotEqual(alterOp.OldMaxRetries, alterOp.MaxRetries);
        Assert.NotEqual(alterOp.OldRetryPeriod, alterOp.RetryPeriod);
    }

    #endregion

    #region Should_Detect_Dropped_RetentionPolicy

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext10 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class HypertableWithoutPolicyContext10 : DbContext
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
            });
        }
    }

    [Fact]
    public void Should_Detect_Dropped_RetentionPolicy()
    {
        using RetentionPolicyContext10 sourceContext = new();
        using HypertableWithoutPolicyContext10 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        DropRetentionPolicyOperation? dropOp = operations.OfType<DropRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
        Assert.Equal("Metrics", dropOp.TableName);
    }

    #endregion

    #region Should_Detect_Multiple_Dropped_Policies

    private class MetricEntity11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LogEntity11
    {
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
    }

    private class MultipleRetentionPoliciesContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();
        public DbSet<LogEntity11> Logs => Set<LogEntity11>();

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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });

            modelBuilder.Entity<LogEntity11>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "30 days");
            });
        }
    }

    private class MultipleHypertablesContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();
        public DbSet<LogEntity11> Logs => Set<LogEntity11>();

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

            modelBuilder.Entity<LogEntity11>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Dropped_Policies()
    {
        using MultipleRetentionPoliciesContext11 sourceContext = new();
        using MultipleHypertablesContext11 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<DropRetentionPolicyOperation> dropOps = [.. operations.OfType<DropRetentionPolicyOperation>()];
        Assert.Equal(2, dropOps.Count);
    }

    #endregion

    #region Should_Not_Generate_Operations_When_No_Changes

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext12 : DbContext
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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operations_When_No_Changes()
    {
        using RetentionPolicyContext12 sourceContext = new();
        using RetentionPolicyContext12 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Handle_Null_Source_Model

    private class MetricEntity13
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext13 : DbContext
    {
        public DbSet<MetricEntity13> Metrics => Set<MetricEntity13>();

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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Source_Model()
    {
        using RetentionPolicyContext13 targetContext = new();
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, targetModel);

        AddRetentionPolicyOperation? addOp = operations.OfType<AddRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
    }

    #endregion

    #region Should_Handle_Null_Target_Model

    private class MetricEntity14
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext14 : DbContext
    {
        public DbSet<MetricEntity14> Metrics => Set<MetricEntity14>();

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
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Target_Model()
    {
        using RetentionPolicyContext14 sourceContext = new();
        IRelationalModel sourceModel = GetModel(sourceContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, null);

        DropRetentionPolicyOperation? dropOp = operations.OfType<DropRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
    }

    #endregion

    #region Should_Handle_Both_Null_Models

    [Fact]
    public void Should_Handle_Both_Null_Models()
    {
        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, null);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Detect_MaxRuntime_Change

    private class MetricEntity15
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    maxRuntime: "00:30:00"
                );
            });
        }
    }

    private class ModifiedMaxRuntimeContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    maxRuntime: "02:00:00" // <-- Changed from "00:30:00"
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_MaxRuntime_Change()
    {
        using RetentionPolicyContext15 sourceContext = new();
        using ModifiedMaxRuntimeContext15 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("00:30:00", alterOp.OldMaxRuntime);
        Assert.Equal("02:00:00", alterOp.MaxRuntime);
    }

    #endregion

    #region Should_Detect_RetryPeriod_Change

    private class MetricEntity16
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicyContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    retryPeriod: "00:05:00"
                );
            });
        }
    }

    private class ModifiedRetryPeriodContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(
                    dropAfter: "7 days",
                    retryPeriod: "00:30:00" // <-- Changed from "00:05:00"
                );
            });
        }
    }

    [Fact]
    public void Should_Detect_RetryPeriod_Change()
    {
        using RetentionPolicyContext16 sourceContext = new();
        using ModifiedRetryPeriodContext16 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        RetentionPolicyDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterRetentionPolicyOperation? alterOp = operations.OfType<AlterRetentionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("00:05:00", alterOp.OldRetryPeriod);
        Assert.Equal("00:30:00", alterOp.RetryPeriod);
    }

    #endregion
}

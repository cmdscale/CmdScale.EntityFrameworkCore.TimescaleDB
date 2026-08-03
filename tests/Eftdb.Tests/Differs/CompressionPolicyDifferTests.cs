using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class CompressionPolicyDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region Should_Detect_New_CompressionPolicy

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

    private class CompressionPolicyContext1 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_New_CompressionPolicy()
    {
        // Arrange
        using HypertableWithoutPolicyContext1 sourceContext = new();
        using CompressionPolicyContext1 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AddCompressionPolicyOperation? addOp = operations.OfType<AddCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("Metrics", addOp.TableName);
        Assert.Equal("7 days", addOp.After);
        Assert.Null(addOp.CreatedBefore);
    }

    #endregion

    #region Should_Detect_New_CompressionPolicy_With_CreatedBefore

    private class MetricEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HypertableWithoutPolicyContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();

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
        }
    }

    private class CreatedBeforePolicyContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();

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
                entity.WithCompressionPolicy(createdBefore: "30 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_New_CompressionPolicy_With_CreatedBefore()
    {
        // Arrange
        using HypertableWithoutPolicyContext2 sourceContext = new();
        using CreatedBeforePolicyContext2 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AddCompressionPolicyOperation? addOp = operations.OfType<AddCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("Metrics", addOp.TableName);
        Assert.Equal("30 days", addOp.CreatedBefore);
        Assert.Null(addOp.After);
    }

    #endregion

    #region Should_Detect_Multiple_New_Policies

    private class MetricEntity3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LogEntity3
    {
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
    }

    private class MultipleHypertablesContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<LogEntity3> Logs => Set<LogEntity3>();

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

            modelBuilder.Entity<LogEntity3>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class MultipleCompressionPoliciesContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();
        public DbSet<LogEntity3> Logs => Set<LogEntity3>();

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
                entity.WithCompressionPolicy(after: "7 days");
            });

            modelBuilder.Entity<LogEntity3>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "30 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_New_Policies()
    {
        // Arrange
        using MultipleHypertablesContext3 sourceContext = new();
        using MultipleCompressionPoliciesContext3 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        List<AddCompressionPolicyOperation> addOps = [.. operations.OfType<AddCompressionPolicyOperation>()];
        Assert.Equal(2, addOps.Count);
        Assert.Contains(addOps, op => op.TableName == "Metrics");
        Assert.Contains(addOps, op => op.TableName == "Logs");
    }

    #endregion

    #region Should_Detect_Policy_With_All_Optional_Parameters

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HypertableWithoutPolicyContext4 : DbContext
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
            });
        }
    }

    private class FullyConfiguredPolicyContext4 : DbContext
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
                entity.WithCompressionPolicy(
                    after: "14 days",
                    scheduleInterval: "12 hours",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    timezone: "UTC",
                    ifNotExists: true);
            });
        }
    }

    [Fact]
    public void Should_Detect_Policy_With_All_Optional_Parameters()
    {
        // Arrange
        using HypertableWithoutPolicyContext4 sourceContext = new();
        using FullyConfiguredPolicyContext4 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AddCompressionPolicyOperation? addOp = operations.OfType<AddCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
        Assert.Equal("14 days", addOp.After);
        Assert.Equal("12 hours", addOp.ScheduleInterval);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), addOp.InitialStart);
        Assert.Equal("UTC", addOp.Timezone);
        Assert.Equal(true, addOp.IfNotExists);
    }

    #endregion

    #region Should_Detect_After_Change

    private class MetricEntity5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext5 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class ModifiedAfterContext5 : DbContext
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
                entity.WithCompressionPolicy(after: "14 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_After_Change()
    {
        // Arrange
        using CompressionPolicyContext5 sourceContext = new();
        using ModifiedAfterContext5 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("7 days", alterOp.OldAfter);
        Assert.Equal("14 days", alterOp.After);
    }

    #endregion

    #region Should_Detect_Switch_From_After_To_CreatedBefore

    private class MetricEntity6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AfterPolicyContext6 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class CreatedBeforePolicyContext6 : DbContext
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
                entity.WithCompressionPolicy(createdBefore: "30 days");
            });
        }
    }

    [Fact]
    public void Should_Detect_Switch_From_After_To_CreatedBefore()
    {
        // Arrange
        using AfterPolicyContext6 sourceContext = new();
        using CreatedBeforePolicyContext6 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("7 days", alterOp.OldAfter);
        Assert.Null(alterOp.After);
        Assert.Null(alterOp.OldCreatedBefore);
        Assert.Equal("30 days", alterOp.CreatedBefore);
    }

    #endregion

    #region Should_Detect_ScheduleInterval_Change

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext7 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days", scheduleInterval: "1 day");
            });
        }
    }

    private class ModifiedScheduleContext7 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days", scheduleInterval: "12 hours");
            });
        }
    }

    [Fact]
    public void Should_Detect_ScheduleInterval_Change()
    {
        // Arrange
        using CompressionPolicyContext7 sourceContext = new();
        using ModifiedScheduleContext7 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("1 day", alterOp.OldScheduleInterval);
        Assert.Equal("12 hours", alterOp.ScheduleInterval);
    }

    #endregion

    #region Should_Detect_InitialStart_Change

    private class MetricEntity8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext8 : DbContext
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
                entity.WithCompressionPolicy(
                    after: "7 days",
                    initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
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
                entity.WithCompressionPolicy(
                    after: "7 days",
                    initialStart: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Detect_InitialStart_Change()
    {
        // Arrange
        using CompressionPolicyContext8 sourceContext = new();
        using ModifiedInitialStartContext8 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), alterOp.OldInitialStart);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), alterOp.InitialStart);
    }

    #endregion

    #region Should_Detect_Timezone_Change

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext9 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days", timezone: "UTC");
            });
        }
    }

    private class ModifiedTimezoneContext9 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days", timezone: "Europe/Berlin");
            });
        }
    }

    [Fact]
    public void Should_Detect_Timezone_Change()
    {
        // Arrange
        using CompressionPolicyContext9 sourceContext = new();
        using ModifiedTimezoneContext9 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("UTC", alterOp.OldTimezone);
        Assert.Equal("Europe/Berlin", alterOp.Timezone);
    }

    #endregion

    #region Should_Detect_Dropped_CompressionPolicy

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext10 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
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
    public void Should_Detect_Dropped_CompressionPolicy()
    {
        // Arrange
        using CompressionPolicyContext10 sourceContext = new();
        using HypertableWithoutPolicyContext10 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        DropCompressionPolicyOperation? dropOp = operations.OfType<DropCompressionPolicyOperation>().FirstOrDefault();
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

    private class MultipleCompressionPoliciesContext11 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });

            modelBuilder.Entity<LogEntity11>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "30 days");
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
        // Arrange
        using MultipleCompressionPoliciesContext11 sourceContext = new();
        using MultipleHypertablesContext11 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        List<DropCompressionPolicyOperation> dropOps = [.. operations.OfType<DropCompressionPolicyOperation>()];
        Assert.Equal(2, dropOps.Count);
    }

    #endregion

    #region Should_Not_Generate_Operations_When_No_Changes

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext12 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operations_When_No_Changes()
    {
        // Arrange
        using CompressionPolicyContext12 sourceContext = new();
        using CompressionPolicyContext12 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

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

    private class CompressionPolicyContext13 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Source_Model()
    {
        // Arrange
        using CompressionPolicyContext13 targetContext = new();
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, targetModel);

        // Assert
        AddCompressionPolicyOperation? addOp = operations.OfType<AddCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(addOp);
    }

    #endregion

    #region Should_Handle_Null_Target_Model

    private class MetricEntity14
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyContext14 : DbContext
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
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Target_Model()
    {
        // Arrange
        using CompressionPolicyContext14 sourceContext = new();
        IRelationalModel sourceModel = GetModel(sourceContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, null);

        // Assert
        DropCompressionPolicyOperation? dropOp = operations.OfType<DropCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(dropOp);
    }

    #endregion

    #region Should_Handle_Both_Null_Models

    [Fact]
    public void Should_Handle_Both_Null_Models()
    {
        // Arrange
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, null);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    #region Should_Suppress_Alter_When_Null_Becomes_Computed_Default_On_Sub_Day_Chunk_Interval

    private class MetricEntity16
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SubDayNullScheduleContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics16");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp).WithChunkTimeInterval("4 hours");
                entity.WithCompressionPolicy(after: "1 day");
            });
        }
    }

    private class SubDayComputedDefaultScheduleContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics16");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp).WithChunkTimeInterval("4 hours");
                entity.WithCompressionPolicy(after: "1 day", scheduleInterval: "2 hours");
            });
        }
    }

    [Fact]
    public void Should_Suppress_Alter_When_Null_Becomes_Computed_Default_On_Sub_Day_Chunk_Interval()
    {
        // Arrange
        using SubDayNullScheduleContext16 sourceContext = new();
        using SubDayComputedDefaultScheduleContext16 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations.OfType<AlterCompressionPolicyOperation>());
    }

    #endregion

    #region Should_Emit_Alter_When_Schedule_Interval_Is_Explicit_Non_Default_On_Sub_Day_Chunk_Interval

    private class MetricEntity17
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SubDayNullScheduleContext17 : DbContext
    {
        public DbSet<MetricEntity17> Metrics => Set<MetricEntity17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity17>(entity =>
            {
                entity.ToTable("Metrics17");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp).WithChunkTimeInterval("4 hours");
                entity.WithCompressionPolicy(after: "1 day");
            });
        }
    }

    private class SubDayExplicitScheduleContext17 : DbContext
    {
        public DbSet<MetricEntity17> Metrics => Set<MetricEntity17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity17>(entity =>
            {
                entity.ToTable("Metrics17");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp).WithChunkTimeInterval("4 hours");
                entity.WithCompressionPolicy(after: "1 day", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Emit_Alter_When_Schedule_Interval_Is_Explicit_Non_Default_On_Sub_Day_Chunk_Interval()
    {
        // Arrange
        using SubDayNullScheduleContext17 sourceContext = new();
        using SubDayExplicitScheduleContext17 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterCompressionPolicyOperation? alterOp = operations.OfType<AlterCompressionPolicyOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("1 hour", alterOp.ScheduleInterval);
    }

    #endregion

    #region Should_Handle_Table_Rename_As_No_Op

    private class MetricEntity15
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OldNameContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("OldTableName");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class NewNameContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("NewTableName");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Handle_Table_Rename_As_No_Op()
    {
        // Arrange
        using OldNameContext15 sourceContext = new();
        using NewNameContext15 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            TableRenames = new Dictionary<(string, string), (string, string)>
            {
                [("public", "OldTableName")] = ("public", "NewTableName")
            }
        };

        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert
        Assert.Empty(operations.OfType<AddCompressionPolicyOperation>());
        Assert.Empty(operations.OfType<DropCompressionPolicyOperation>());
    }

    #endregion

    // ── B5: null scheduleInterval equals computed default for day-or-longer chunk interval ──

    #region Should_Suppress_Alter_When_Null_Becomes_Computed_Default_On_Day_Chunk_Interval

    private class MetricEntity18
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DayChunkNullScheduleContext18 : DbContext
    {
        public DbSet<MetricEntity18> Metrics => Set<MetricEntity18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity18>(entity =>
            {
                entity.ToTable("Metrics18");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class DayChunkExplicitDefaultScheduleContext18 : DbContext
    {
        public DbSet<MetricEntity18> Metrics => Set<MetricEntity18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity18>(entity =>
            {
                entity.ToTable("Metrics18");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days", scheduleInterval: "12 hours");
            });
        }
    }

    [Fact]
    public void Should_Suppress_Alter_When_Null_Becomes_Computed_Default_On_Day_Chunk_Interval()
    {
        // Arrange
        using DayChunkNullScheduleContext18 sourceContext = new();
        using DayChunkExplicitDefaultScheduleContext18 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations.OfType<AlterCompressionPolicyOperation>());
    }

    #endregion

    // ── B5: adding a policy produces exactly one Add and no Drop ──

    #region Should_Produce_Exactly_One_Add_And_No_Drop_When_Policy_Added

    private class MetricEntity19
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoPolicyContext19 : DbContext
    {
        public DbSet<MetricEntity19> Metrics => Set<MetricEntity19>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity19>(entity =>
            {
                entity.ToTable("Metrics19");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class WithPolicyContext19 : DbContext
    {
        public DbSet<MetricEntity19> Metrics => Set<MetricEntity19>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity19>(entity =>
            {
                entity.ToTable("Metrics19");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Produce_Exactly_One_Add_And_No_Drop_When_Policy_Added()
    {
        // Arrange
        using NoPolicyContext19 sourceContext = new();
        using WithPolicyContext19 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);
        CompressionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        List<AddCompressionPolicyOperation> addOps = [.. operations.OfType<AddCompressionPolicyOperation>()];
        List<DropCompressionPolicyOperation> dropOps = [.. operations.OfType<DropCompressionPolicyOperation>()];

        Assert.Single(addOps);
        Assert.Empty(dropOps);
        Assert.Equal("Metrics19", addOps[0].TableName);
    }

    #endregion
}

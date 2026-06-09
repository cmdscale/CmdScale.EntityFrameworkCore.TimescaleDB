using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// In-memory ordering tests that drive the real orchestrator (<c>TimescaleMigrationsModelDiffer</c> via
/// <see cref="IMigrationsModelDiffer"/>) and assert relative ordering of returned operations for cases not
/// already covered by <c>MigrationOperationOrderingTests</c>.
/// </summary>
public class OperationOrderingTests
{
    private static IReadOnlyList<MigrationOperation> GenerateMigrationOperations(DbContext? sourceContext, DbContext targetContext)
    {
        IMigrationsModelDiffer differ = targetContext.GetService<IMigrationsModelDiffer>();
        IRelationalModel? sourceModel = sourceContext?.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        IRelationalModel targetModel = targetContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        return differ.GetDifferences(sourceModel, targetModel);
    }

    #region Should_Order_CreateHypertable_Then_CreateContinuousAggregate_Then_AddPolicy

    private class OrderMetricA
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderAggregateA
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CreateOrderContextA : DbContext
    {
        public DbSet<OrderMetricA> Metrics => Set<OrderMetricA>();
        public DbSet<OrderAggregateA> Aggregates => Set<OrderAggregateA>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricA>(entity =>
            {
                entity.ToTable("order_metrics_a");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<OrderAggregateA>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<OrderAggregateA, OrderMetricA>(
                        "order_view_a",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Order_CreateHypertable_Then_CreateContinuousAggregate_Then_AddPolicy()
    {
        // Arrange
        using CreateOrderContextA targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(null, targetContext)];

        // Assert
        int hypertableIndex = operations.FindIndex(op => op is CreateHypertableOperation);
        int createAggregateIndex = operations.FindIndex(op => op is CreateContinuousAggregateOperation);
        int addPolicyIndex = operations.FindIndex(op => op is AddContinuousAggregatePolicyOperation);

        Assert.NotEqual(-1, hypertableIndex);
        Assert.NotEqual(-1, createAggregateIndex);
        Assert.NotEqual(-1, addPolicyIndex);

        Assert.True(hypertableIndex < createAggregateIndex,
            $"CreateHypertable ({hypertableIndex}) should precede CreateContinuousAggregate ({createAggregateIndex})");
        Assert.True(createAggregateIndex < addPolicyIndex,
            $"CreateContinuousAggregate ({createAggregateIndex}) should precede AddContinuousAggregatePolicy ({addPolicyIndex})");
    }

    #endregion

    #region Should_Order_DropContinuousAggregate_Before_DropHypertable

    private class OrderMetricB
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderAggregateB
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DropOrderSourceContextB : DbContext
    {
        public DbSet<OrderMetricB> Metrics => Set<OrderMetricB>();
        public DbSet<OrderAggregateB> Aggregates => Set<OrderAggregateB>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricB>(entity =>
            {
                entity.ToTable("order_metrics_b");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<OrderAggregateB>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<OrderAggregateB, OrderMetricB>(
                        "order_view_b",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class DropOrderTargetContextB : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();
    }

    [Fact]
    public void Should_Order_DropContinuousAggregate_Before_DropHypertable()
    {
        // Arrange — source has a hypertable + a dependent continuous aggregate; target is empty.
        using DropOrderSourceContextB sourceContext = new();
        using DropOrderTargetContextB targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert — the aggregate (depends on the hypertable) must be dropped before the parent table.
        int dropAggregateIndex = operations.FindIndex(op => op is DropContinuousAggregateOperation);
        int dropTableIndex = operations.FindIndex(op =>
            op is DropTableOperation dropTable && dropTable.Name == "order_metrics_b");

        Assert.NotEqual(-1, dropAggregateIndex);
        Assert.NotEqual(-1, dropTableIndex);
        Assert.True(dropAggregateIndex < dropTableIndex,
            $"DropContinuousAggregate ({dropAggregateIndex}) should precede DropTable ({dropTableIndex})");
    }

    #endregion

    #region Should_Order_Drop_Policies_Before_DropContinuousAggregate

    private class OrderMetricC
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderAggregateC
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DropPoliciesSourceContextC : DbContext
    {
        public DbSet<OrderMetricC> Metrics => Set<OrderMetricC>();
        public DbSet<OrderAggregateC> Aggregates => Set<OrderAggregateC>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricC>(entity =>
            {
                entity.ToTable("order_metrics_c");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<OrderAggregateC>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<OrderAggregateC, OrderMetricC>(
                        "order_view_c",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.WithRetentionPolicy(dropAfter: "30 days");
            });
        }
    }

    private class DropPoliciesTargetContextC : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();
    }

    [Fact]
    public void Should_Order_Drop_Policies_Before_DropContinuousAggregate()
    {
        // The retention and refresh policies depend on the continuous aggregate, so both drops must precede
        // the DropContinuousAggregate. Per GetOperationPriority: DropRetentionPolicy (-60) and
        // RemoveContinuousAggregatePolicy (-50) both come before DropContinuousAggregate (-40).

        // Arrange
        using DropPoliciesSourceContextC sourceContext = new();
        using DropPoliciesTargetContextC targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        int dropRetentionIndex = operations.FindIndex(op => op is DropRetentionPolicyOperation);
        int removeCaPolicyIndex = operations.FindIndex(op => op is RemoveContinuousAggregatePolicyOperation);
        int dropAggregateIndex = operations.FindIndex(op => op is DropContinuousAggregateOperation);

        Assert.NotEqual(-1, dropRetentionIndex);
        Assert.NotEqual(-1, removeCaPolicyIndex);
        Assert.NotEqual(-1, dropAggregateIndex);

        Assert.True(dropRetentionIndex < dropAggregateIndex,
            $"DropRetentionPolicy ({dropRetentionIndex}) should precede DropContinuousAggregate ({dropAggregateIndex})");
        Assert.True(removeCaPolicyIndex < dropAggregateIndex,
            $"RemoveContinuousAggregatePolicy ({removeCaPolicyIndex}) should precede DropContinuousAggregate ({dropAggregateIndex})");

        // The two policy drops are ordered relative to each other: retention (-60) before CA policy (-50).
        Assert.True(dropRetentionIndex < removeCaPolicyIndex,
            $"DropRetentionPolicy ({dropRetentionIndex}) should precede RemoveContinuousAggregatePolicy ({removeCaPolicyIndex})");
    }

    #endregion
}

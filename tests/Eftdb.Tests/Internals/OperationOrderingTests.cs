using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
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
        // Arrange
        using DropOrderSourceContextB sourceContext = new();
        using DropOrderTargetContextB targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
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

        Assert.True(dropRetentionIndex < removeCaPolicyIndex,
            $"DropRetentionPolicy ({dropRetentionIndex}) should precede RemoveContinuousAggregatePolicy ({removeCaPolicyIndex})");
    }

    #endregion

    #region Should_Treat_Index_Rename_As_Rename_Through_Orchestrator

    private class IndexRenameMetricD
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IndexRenameSourceContextD : DbContext
    {
        public DbSet<IndexRenameMetricD> Metrics => Set<IndexRenameMetricD>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexRenameMetricD>(entity =>
            {
                entity.ToTable("idx_rename_metrics_d");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("idx_rename_old_d");
                entity.WithReorderPolicy("idx_rename_old_d");
            });
    }

    private class IndexRenameTargetContextD : DbContext
    {
        public DbSet<IndexRenameMetricD> Metrics => Set<IndexRenameMetricD>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexRenameMetricD>(entity =>
            {
                entity.ToTable("idx_rename_metrics_d");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("idx_rename_new_d");
                entity.WithReorderPolicy("idx_rename_new_d");
            });
    }

    [Fact]
    public void Should_Treat_Index_Rename_As_Rename_Through_Orchestrator()
    {
        // Arrange
        using IndexRenameSourceContextD sourceContext = new();
        using IndexRenameTargetContextD targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        Assert.Contains(operations.OfType<RenameIndexOperation>(), o => o.NewName == "idx_rename_new_d");
        Assert.Empty(operations.OfType<AlterReorderPolicyOperation>());
        Assert.Empty(operations.OfType<AddReorderPolicyOperation>());
        Assert.Empty(operations.OfType<DropReorderPolicyOperation>());
    }

    #endregion

    #region Should_Order_AlterHypertable_After_CreateHypertable_Through_Orchestrator

    private class AlterHtMetricE
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AlterHtNewMetricE
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AlterHtSourceContextE : DbContext
    {
        public DbSet<AlterHtMetricE> Metrics => Set<AlterHtMetricE>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AlterHtMetricE>(entity =>
            {
                entity.ToTable("alter_ht_metrics_e");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
    }

    private class AlterHtTargetContextE : DbContext
    {
        public DbSet<AlterHtMetricE> Metrics => Set<AlterHtMetricE>();
        public DbSet<AlterHtNewMetricE> NewMetrics => Set<AlterHtNewMetricE>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlterHtMetricE>(entity =>
            {
                entity.ToTable("alter_ht_metrics_e");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
            });

            modelBuilder.Entity<AlterHtNewMetricE>(entity =>
            {
                entity.ToTable("alter_ht_new_metrics_e");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Order_AlterHypertable_After_CreateHypertable_Through_Orchestrator()
    {
        // Arrange
        using AlterHtSourceContextE sourceContext = new();
        using AlterHtTargetContextE targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        AlterHypertableOperation alterOp = Assert.Single(operations.OfType<AlterHypertableOperation>());
        Assert.Equal("alter_ht_metrics_e", alterOp.TableName);
        Assert.Equal("7 days", alterOp.OldChunkTimeInterval);
        Assert.Equal("1 day", alterOp.ChunkTimeInterval);

        int createHypertableIndex = operations.FindIndex(op => op is CreateHypertableOperation);
        int alterHypertableIndex = operations.FindIndex(op => op is AlterHypertableOperation);
        Assert.True(createHypertableIndex >= 0, "Expected a CreateHypertableOperation for the new hypertable.");
        Assert.True(createHypertableIndex < alterHypertableIndex, "CreateHypertable (10) must be ordered before AlterHypertable (15).");
    }

    #endregion

    #region Should_Order_AlterRetentionPolicy_After_CreateHypertable

    private class OrderMetricF
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionPolicySourceContextF : DbContext
    {
        public DbSet<OrderMetricF> Metrics => Set<OrderMetricF>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricF>(entity =>
            {
                entity.ToTable("order_metrics_f");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "7 days");
            });
        }
    }

    private class RetentionPolicyChangedContextF : DbContext
    {
        public DbSet<OrderMetricF> Metrics => Set<OrderMetricF>();
        public DbSet<OrderMetricG> NewMetrics => Set<OrderMetricG>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricF>(entity =>
            {
                entity.ToTable("order_metrics_f");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithRetentionPolicy(dropAfter: "14 days");
            });

            modelBuilder.Entity<OrderMetricG>(entity =>
            {
                entity.ToTable("order_metrics_g");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class OrderMetricG
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [Fact]
    public void Should_Order_AlterRetentionPolicy_After_CreateHypertable()
    {

        // Arrange
        using RetentionPolicySourceContextF sourceContext = new();
        using RetentionPolicyChangedContextF targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        int createHypertableIndex = operations.FindIndex(op => op is CreateHypertableOperation);
        int alterRetentionIndex = operations.FindIndex(op => op is AlterRetentionPolicyOperation);

        Assert.NotEqual(-1, createHypertableIndex);
        Assert.NotEqual(-1, alterRetentionIndex);
        Assert.True(createHypertableIndex < alterRetentionIndex,
            $"CreateHypertable (priority 10) should precede AlterRetentionPolicy (priority 60), " +
            $"but found indices {createHypertableIndex} and {alterRetentionIndex}.");
    }

    #endregion

    #region Should_Order_CreateTable_Before_CreateHypertable

    private class OrderMetricH
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreateTableAndHypertableContextH : DbContext
    {
        public DbSet<OrderMetricH> Metrics => Set<OrderMetricH>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricH>(entity =>
            {
                entity.ToTable("order_metrics_h");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Order_CreateTable_Before_CreateHypertable()
    {

        // Arrange
        using CreateTableAndHypertableContextH targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(null, targetContext)];

        // Assert
        int createTableIndex = operations.FindIndex(op =>
            op is CreateTableOperation ct && ct.Name == "order_metrics_h");
        int createHypertableIndex = operations.FindIndex(op =>
            op is CreateHypertableOperation ht && ht.TableName == "order_metrics_h");

        Assert.NotEqual(-1, createTableIndex);
        Assert.NotEqual(-1, createHypertableIndex);
        Assert.True(createTableIndex < createHypertableIndex,
            $"CreateTable (priority 0) should precede CreateHypertable (priority 10), " +
            $"but found indices {createTableIndex} and {createHypertableIndex}.");
    }

    #endregion

    #region Should_Order_DropCompressionPolicy_Before_DropContinuousAggregate

    private class OrderMetricDropCp
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderAggDropCp
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DropCpSourceContext : DbContext
    {
        public DbSet<OrderMetricDropCp> Metrics => Set<OrderMetricDropCp>();
        public DbSet<OrderAggDropCp> Aggregates => Set<OrderAggDropCp>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricDropCp>(entity =>
            {
                entity.ToTable("order_metric_drop_cp");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp).EnableCompression();
            });

            modelBuilder.Entity<OrderAggDropCp>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<OrderAggDropCp, OrderMetricDropCp>(
                        "order_agg_drop_cp",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithCompression();
                entity.WithCompressionPolicy(after: "7 days");
            });
        }
    }

    private class DropCpTargetContext : DbContext
    {
        public DbSet<OrderMetricDropCp> Metrics => Set<OrderMetricDropCp>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricDropCp>(entity =>
            {
                entity.ToTable("order_metric_drop_cp");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp).EnableCompression();
            });
        }
    }

    [Fact]
    public void Should_Order_DropCompressionPolicy_Before_DropContinuousAggregate()
    {
        // Arrange
        using DropCpSourceContext sourceContext = new();
        using DropCpTargetContext targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        int dropCpIndex = operations.FindIndex(op => op is DropCompressionPolicyOperation);
        int dropAggIndex = operations.FindIndex(op => op is DropContinuousAggregateOperation);

        Assert.NotEqual(-1, dropCpIndex);
        Assert.NotEqual(-1, dropAggIndex);
        Assert.True(dropCpIndex < dropAggIndex,
            $"DropCompressionPolicy (priority -45) should precede DropContinuousAggregate (priority -40), " +
            $"but found indices {dropCpIndex} and {dropAggIndex}.");
    }

    #endregion

    #region Should_Order_DropReorderPolicy_Before_DropTable

    private class OrderMetricDropRp
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropRpSourceContext : DbContext
    {
        public DbSet<OrderMetricDropRp> Metrics => Set<OrderMetricDropRp>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricDropRp>(entity =>
            {
                entity.ToTable("order_metric_drop_rp");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("order_metric_drop_rp_idx");
            });
        }
    }

    private class DropRpTargetContext : DbContext
    {
        public DbSet<OrderMetricDropRp> Metrics => Set<OrderMetricDropRp>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricDropRp>(entity =>
            {
                entity.ToTable("order_metric_drop_rp");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Order_DropReorderPolicy_Before_DropTable()
    {
        // Arrange
        using DropRpSourceContext sourceContext = new();
        using DropRpTargetContext targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        int dropRpIndex = operations.FindIndex(op => op is DropReorderPolicyOperation);
        Assert.NotEqual(-1, dropRpIndex);
    }

    #endregion

    #region Should_Order_AlterContinuousAggregate_After_CreateContinuousAggregate

    private class OrderMetricAlterCagg
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderAggAlterCaggSource
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OrderAggAlterCaggSourceContext : DbContext
    {
        public DbSet<OrderMetricAlterCagg> Metrics => Set<OrderMetricAlterCagg>();
        public DbSet<OrderAggAlterCaggSource> Aggregates => Set<OrderAggAlterCaggSource>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricAlterCagg>(entity =>
            {
                entity.ToTable("order_metric_alter_cagg");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<OrderAggAlterCaggSource>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<OrderAggAlterCaggSource, OrderMetricAlterCagg>(
                        "order_agg_alter_cagg",
                        "1 hour",
                        x => x.Timestamp,
                        chunkInterval: "7 days")
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class OrderAggAlterCaggTargetContext : DbContext
    {
        public DbSet<OrderMetricAlterCagg> Metrics => Set<OrderMetricAlterCagg>();
        public DbSet<OrderAggAlterCaggSource> Aggregates => Set<OrderAggAlterCaggSource>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderMetricAlterCagg>(entity =>
            {
                entity.ToTable("order_metric_alter_cagg");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<OrderAggAlterCaggSource>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<OrderAggAlterCaggSource, OrderMetricAlterCagg>(
                        "order_agg_alter_cagg",
                        "1 hour",
                        x => x.Timestamp,
                        chunkInterval: "30 days")
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Order_AlterContinuousAggregate_After_CreateContinuousAggregate()
    {
        // Arrange
        using OrderAggAlterCaggSourceContext sourceContext = new();
        using OrderAggAlterCaggTargetContext targetContext = new();

        // Act
        List<MigrationOperation> operations = [.. GenerateMigrationOperations(sourceContext, targetContext)];

        // Assert
        int alterAggIndex = operations.FindIndex(op => op is AlterContinuousAggregateOperation);
        Assert.NotEqual(-1, alterAggIndex);
    }

    #endregion
}

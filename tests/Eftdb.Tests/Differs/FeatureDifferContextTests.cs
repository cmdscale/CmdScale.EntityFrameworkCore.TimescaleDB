using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

/// <summary>
/// Differ-level tests that hand a feature differ an explicit <see cref="FeatureDiffContext"/> carrying a
/// rename or a recreated-aggregate signal, and assert the differ treats it as a rename/cascade rather than
/// emitting drop+create operations. Models are built in-memory; no database is touched.
/// </summary>
public class FeatureDifferContextTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region HypertableDiffer_Resolves_Table_Rename

    private class HtRenameMetricSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HtRenameMetricTarget
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HtRenameSourceContext : DbContext
    {
        public DbSet<HtRenameMetricSource> Metrics => Set<HtRenameMetricSource>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HtRenameMetricSource>(entity =>
            {
                entity.ToTable("ht_rename_old");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class HtRenameTargetContext : DbContext
    {
        public DbSet<HtRenameMetricTarget> Metrics => Set<HtRenameMetricTarget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HtRenameMetricTarget>(entity =>
            {
                entity.ToTable("ht_rename_new"); // <-- Renamed from ht_rename_old
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void HypertableDiffer_Treats_Table_Rename_As_Rename_Not_Recreate()
    {
        // Arrange
        using HtRenameSourceContext sourceContext = new();
        using HtRenameTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            TableRenames = new Dictionary<(string, string), (string, string)>
            {
                [(DefaultValues.DefaultSchema, "ht_rename_old")] = (DefaultValues.DefaultSchema, "ht_rename_new"),
            },
        };

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert - a pure rename should produce no hypertable operations at all
        Assert.DoesNotContain(operations, op => op is CreateHypertableOperation);
        Assert.DoesNotContain(operations, op => op is AlterHypertableOperation);
    }

    #endregion

    #region HypertableDiffer_Rewrites_CompressionOrderBy_Column_Rename

    private class HtOrderByMetricSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HtOrderByMetricTarget
    {
        public DateTime Moment { get; set; } // <-- Renamed from Timestamp
        public double Value { get; set; }
    }

    private class HtOrderBySourceContext : DbContext
    {
        public DbSet<HtOrderByMetricSource> Metrics => Set<HtOrderByMetricSource>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HtOrderByMetricSource>(entity =>
            {
                entity.ToTable("ht_orderby_rename");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(b => [b.ByDescending(x => x.Timestamp)]);
            });
        }
    }

    private class HtOrderByTargetContext : DbContext
    {
        public DbSet<HtOrderByMetricTarget> Metrics => Set<HtOrderByMetricTarget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HtOrderByMetricTarget>(entity =>
            {
                entity.ToTable("ht_orderby_rename");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Moment)
                      .WithCompressionOrderBy(b => [b.ByDescending(x => x.Moment)]);
            });
        }
    }

    [Fact]
    public void HypertableDiffer_Rewrites_OrderBy_Column_On_Rename_And_Preserves_Direction()
    {
        // The time column AND the compression order-by reference the same renamed column. With a column-rename
        // context, the source order-by ("Timestamp DESC") rewrites to ("Moment DESC") and compares equal to the
        // target, so no AlterHypertableOperation is emitted.

        // Arrange
        using HtOrderBySourceContext sourceContext = new();
        using HtOrderByTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            ColumnRenames = new Dictionary<(string, string, string), string>
            {
                // Keyed on the post-rename table name (table name unchanged here).
                [(DefaultValues.DefaultSchema, "ht_orderby_rename", "Timestamp")] = "Moment",
            },
        };

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert - the order-by column rename is resolved (direction suffix preserved), so no alter is produced.
        Assert.DoesNotContain(operations, op => op is AlterHypertableOperation);
        Assert.DoesNotContain(operations, op => op is CreateHypertableOperation);
    }

    #endregion

    #region ReorderPolicyDiffer_Resolves_Index_Rename

    private class RpRenameMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RpRenameSourceContext : DbContext
    {
        public DbSet<RpRenameMetric> Metrics => Set<RpRenameMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpRenameMetric>(entity =>
            {
                entity.ToTable("rp_rename_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("rp_idx_old");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("rp_idx_old");
            });
        }
    }

    private class RpRenameTargetContext : DbContext
    {
        public DbSet<RpRenameMetric> Metrics => Set<RpRenameMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpRenameMetric>(entity =>
            {
                entity.ToTable("rp_rename_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("rp_idx_new"); // <-- Index renamed from rp_idx_old
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("rp_idx_new");
            });
        }
    }

    [Fact]
    public void ReorderPolicyDiffer_Treats_Index_Rename_As_Rename_Not_Alter()
    {
        // Arrange
        using RpRenameSourceContext sourceContext = new();
        using RpRenameTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            IndexRenames = new Dictionary<(string, string), (string, string)>
            {
                [(DefaultValues.DefaultSchema, "rp_idx_old")] = (DefaultValues.DefaultSchema, "rp_idx_new"),
            },
        };

        ReorderPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert - only the index changed, and that change is a known rename, so no alter is needed.
        Assert.DoesNotContain(operations, op => op is AlterReorderPolicyOperation);
        Assert.DoesNotContain(operations, op => op is AddReorderPolicyOperation);
        Assert.DoesNotContain(operations, op => op is DropReorderPolicyOperation);
    }

    #endregion

    #region RetentionPolicyDiffer_Recreate_Cascade

    private class RetCascadeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetCascadeAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class RetCascadeContext : DbContext
    {
        public DbSet<RetCascadeMetric> Metrics => Set<RetCascadeMetric>();
        public DbSet<RetCascadeAggregate> Aggregates => Set<RetCascadeAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetCascadeMetric>(entity =>
            {
                entity.ToTable("ret_cascade_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<RetCascadeAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<RetCascadeAggregate, RetCascadeMetric>(
                        "ret_cascade_view",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
                entity.WithRetentionPolicy(dropAfter: "30 days");
            });
        }
    }

    [Fact]
    public void RetentionPolicyDiffer_Readds_Policy_When_Aggregate_Is_Recreated()
    {
        // Source and target are identical, so a normal diff would produce nothing. But the orchestrator has
        // decided to recreate the "ret_cascade_view" aggregate, which cascades to drop its retention policy.
        // The differ must therefore re-add the retention policy.

        // Arrange
        using RetCascadeContext sourceContext = new();
        using RetCascadeContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            RecreatedAggregates = new HashSet<(string, string)>
            {
                (DefaultValues.DefaultSchema, "ret_cascade_view"),
            },
        };

        RetentionPolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert
        AddRetentionPolicyOperation? addOp = operations.OfType<AddRetentionPolicyOperation>()
            .FirstOrDefault(op => op.TableName == "ret_cascade_view");
        Assert.NotNull(addOp);
        Assert.Equal("30 days", addOp.DropAfter);

        // It must NOT also drop the policy for the recreated view.
        Assert.DoesNotContain(operations, op => op is DropRetentionPolicyOperation);
    }

    #endregion

    #region ContinuousAggregatePolicyDiffer_Recreate_Cascade

    private class CapCascadeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CapCascadeAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CapCascadeContext : DbContext
    {
        public DbSet<CapCascadeMetric> Metrics => Set<CapCascadeMetric>();
        public DbSet<CapCascadeAggregate> Aggregates => Set<CapCascadeAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CapCascadeMetric>(entity =>
            {
                entity.ToTable("cap_cascade_metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CapCascadeAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CapCascadeAggregate, CapCascadeMetric>(
                        "cap_cascade_view",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "1 month", endOffset: "1 hour", scheduleInterval: "1 hour");
            });
        }
    }

    [Fact]
    public void ContinuousAggregatePolicyDiffer_Readds_Refresh_Policy_When_Aggregate_Is_Recreated()
    {
        // Identical source/target: a normal diff yields nothing. With the view marked as recreated, the refresh
        // policy is dropped by the recreate cascade and must be re-added without a matching remove.

        // Arrange
        using CapCascadeContext sourceContext = new();
        using CapCascadeContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            RecreatedAggregates = new HashSet<(string, string)>
            {
                (DefaultValues.DefaultSchema, "cap_cascade_view"),
            },
        };

        ContinuousAggregatePolicyDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert
        AddContinuousAggregatePolicyOperation? addOp = operations.OfType<AddContinuousAggregatePolicyOperation>()
            .FirstOrDefault(op => op.MaterializedViewName == "cap_cascade_view");
        Assert.NotNull(addOp);

        // No remove for the recreated view.
        Assert.DoesNotContain(operations, op =>
            op is RemoveContinuousAggregatePolicyOperation remove && remove.MaterializedViewName == "cap_cascade_view");
    }

    #endregion

    #region ContinuousAggregateDiffer_Resolves_Parent_Table_Rename

    private class CaParentRenameSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaParentRenameTargetMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaParentRenameAggregate
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaParentRenameSourceContext : DbContext
    {
        public DbSet<CaParentRenameSourceMetric> Metrics => Set<CaParentRenameSourceMetric>();
        public DbSet<CaParentRenameAggregate> Aggregates => Set<CaParentRenameAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaParentRenameSourceMetric>(entity =>
            {
                entity.ToTable("ca_parent_old");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CaParentRenameAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CaParentRenameAggregate, CaParentRenameSourceMetric>(
                        "ca_parent_rename_view",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class CaParentRenameTargetContext : DbContext
    {
        public DbSet<CaParentRenameTargetMetric> Metrics => Set<CaParentRenameTargetMetric>();
        public DbSet<CaParentRenameAggregate> Aggregates => Set<CaParentRenameAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaParentRenameTargetMetric>(entity =>
            {
                entity.ToTable("ca_parent_new"); // <-- Parent table renamed from ca_parent_old
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CaParentRenameAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CaParentRenameAggregate, CaParentRenameTargetMetric>(
                        "ca_parent_rename_view",
                        "1 hour",
                        x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void ContinuousAggregateDiffer_Treats_Parent_Table_Rename_As_Rename_Not_Recreate()
    {
        // Only the CA's parent hypertable was renamed. With the rename in the context, the differ should NOT
        // drop and recreate the aggregate. (CA column/where-clause rewriting is explicitly out of scope; only
        // the parent-table rename is asserted here.)

        // Arrange
        using CaParentRenameSourceContext sourceContext = new();
        using CaParentRenameTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        FeatureDiffContext context = new()
        {
            TableRenames = new Dictionary<(string, string), (string, string)>
            {
                [(DefaultValues.DefaultSchema, "ca_parent_old")] = (DefaultValues.DefaultSchema, "ca_parent_new"),
            },
        };

        ContinuousAggregateDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel, context);

        // Assert
        Assert.DoesNotContain(operations, op => op is DropContinuousAggregateOperation);
        Assert.DoesNotContain(operations, op => op is CreateContinuousAggregateOperation);
    }

    #endregion
}

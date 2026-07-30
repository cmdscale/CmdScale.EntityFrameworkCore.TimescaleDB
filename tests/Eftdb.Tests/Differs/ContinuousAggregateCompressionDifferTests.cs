using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class ContinuousAggregateCompressionDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const string ConnStr = "Host=localhost;Database=test;Username=test;Password=test";

    private static DbContextOptionsBuilder BaseOptions(DbContextOptionsBuilder b)
        => b.UseNpgsql(ConnStr).UseTimescaleDb();

    #region Should_Detect_Compression_Enabled_On_New_CAgg

    private class CompNewSource1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompNewAgg1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoCompressionContext1 : DbContext
    {
        public DbSet<CompNewSource1> Metrics => Set<CompNewSource1>();
        public DbSet<CompNewAgg1> Hourly => Set<CompNewAgg1>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompNewSource1>(e => { e.ToTable("comp_new_source1"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompNewAgg1>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompNewAgg1, CompNewSource1>("comp_new_cagg1", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    private class WithCompressionContext1 : DbContext
    {
        public DbSet<CompNewSource1> Metrics => Set<CompNewSource1>();
        public DbSet<CompNewAgg1> Hourly => Set<CompNewAgg1>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompNewSource1>(e => { e.ToTable("comp_new_source1"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompNewAgg1>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompNewAgg1, CompNewSource1>("comp_new_cagg1", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompression();
            });
        }
    }

    [Fact]
    public void Should_Detect_Compression_Enabled_On_New_CAgg()
    {
        // Arrange
        using NoCompressionContext1 source = new();
        using WithCompressionContext1 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        AlterContinuousAggregateOperation? alter = ops.OfType<AlterContinuousAggregateOperation>().SingleOrDefault();
        Assert.NotNull(alter);
        Assert.True(alter.EnableCompression);
        Assert.False(alter.OldEnableCompression);
        Assert.Equal("comp_new_cagg1", alter.MaterializedViewName);
    }

    #endregion

    #region Should_Detect_Compression_Disabled

    private class CompDisableSource2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompDisableAgg2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CompressionEnabledContext2 : DbContext
    {
        public DbSet<CompDisableSource2> Metrics => Set<CompDisableSource2>();
        public DbSet<CompDisableAgg2> Hourly => Set<CompDisableAgg2>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompDisableSource2>(e => { e.ToTable("comp_disable_source2"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompDisableAgg2>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompDisableAgg2, CompDisableSource2>("comp_disable_cagg2", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompression();
            });
        }
    }

    private class CompressionRemovedContext2 : DbContext
    {
        public DbSet<CompDisableSource2> Metrics => Set<CompDisableSource2>();
        public DbSet<CompDisableAgg2> Hourly => Set<CompDisableAgg2>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompDisableSource2>(e => { e.ToTable("comp_disable_source2"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompDisableAgg2>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompDisableAgg2, CompDisableSource2>("comp_disable_cagg2", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Detect_Compression_Disabled()
    {
        // Arrange
        using CompressionEnabledContext2 source = new();
        using CompressionRemovedContext2 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        AlterContinuousAggregateOperation? alter = ops.OfType<AlterContinuousAggregateOperation>().SingleOrDefault();
        Assert.NotNull(alter);
        Assert.False(alter.EnableCompression);
        Assert.True(alter.OldEnableCompression);
    }

    #endregion

    #region Should_Detect_SegmentBy_Change

    private class CompSegSource3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Region { get; set; } = string.Empty;
    }

    private class CompSegAgg3
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = string.Empty;
    }

    private class SegmentByRegionContext3 : DbContext
    {
        public DbSet<CompSegSource3> Metrics => Set<CompSegSource3>();
        public DbSet<CompSegAgg3> Hourly => Set<CompSegAgg3>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompSegSource3>(e => { e.ToTable("comp_seg_source3"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompSegAgg3>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompSegAgg3, CompSegSource3>("comp_seg_cagg3", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.Region);
            });
        }
    }

    private class SegmentByChangedContext3 : DbContext
    {
        public DbSet<CompSegSource3> Metrics => Set<CompSegSource3>();
        public DbSet<CompSegAgg3> Hourly => Set<CompSegAgg3>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompSegSource3>(e => { e.ToTable("comp_seg_source3"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompSegAgg3>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompSegAgg3, CompSegSource3>("comp_seg_cagg3", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.TimeBucket);
            });
        }
    }

    [Fact]
    public void Should_Detect_SegmentBy_Change()
    {
        // Arrange
        using SegmentByRegionContext3 source = new();
        using SegmentByChangedContext3 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        AlterContinuousAggregateOperation? alter = ops.OfType<AlterContinuousAggregateOperation>().SingleOrDefault();
        Assert.NotNull(alter);
        Assert.NotEqual(alter.CompressionSegmentBy, alter.OldCompressionSegmentBy);
    }

    #endregion

    #region Should_Detect_OrderBy_Change

    private class CompOrdSource4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompOrdAgg4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class OrderByAscContext4 : DbContext
    {
        public DbSet<CompOrdSource4> Metrics => Set<CompOrdSource4>();
        public DbSet<CompOrdAgg4> Hourly => Set<CompOrdAgg4>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompOrdSource4>(e => { e.ToTable("comp_ord_source4"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompOrdAgg4>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompOrdAgg4, CompOrdSource4>("comp_ord_cagg4", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(s => s.By(x => x.TimeBucket));
            });
        }
    }

    private class OrderByDescContext4 : DbContext
    {
        public DbSet<CompOrdSource4> Metrics => Set<CompOrdSource4>();
        public DbSet<CompOrdAgg4> Hourly => Set<CompOrdAgg4>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompOrdSource4>(e => { e.ToTable("comp_ord_source4"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompOrdAgg4>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompOrdAgg4, CompOrdSource4>("comp_ord_cagg4", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(s => s.ByDescending(x => x.TimeBucket));
            });
        }
    }

    [Fact]
    public void Should_Detect_OrderBy_Change()
    {
        // Arrange
        using OrderByAscContext4 source = new();
        using OrderByDescContext4 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        AlterContinuousAggregateOperation? alter = ops.OfType<AlterContinuousAggregateOperation>().SingleOrDefault();
        Assert.NotNull(alter);
        Assert.NotNull(alter.CompressionOrderBy);
        Assert.NotNull(alter.OldCompressionOrderBy);
    }

    #endregion

    #region Should_Not_Generate_Operation_When_Compression_Unchanged

    private class CompNoopSource5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompNoopAgg5
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CompUnchangedContext5 : DbContext
    {
        public DbSet<CompNoopSource5> Metrics => Set<CompNoopSource5>();
        public DbSet<CompNoopAgg5> Hourly => Set<CompNoopAgg5>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompNoopSource5>(e => { e.ToTable("comp_noop_source5"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompNoopAgg5>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompNoopAgg5, CompNoopSource5>("comp_noop_cagg5", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompression();
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operation_When_Compression_Unchanged()
    {
        // Arrange
        using CompUnchangedContext5 source = new();
        using CompUnchangedContext5 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        Assert.Empty(ops);
    }

    #endregion

    #region Should_Detect_Compression_On_Newly_Created_CAgg

    private class CompCreateSource6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompCreateAgg6
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoCAggContext6 : DbContext
    {
        public DbSet<CompCreateSource6> Metrics => Set<CompCreateSource6>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompCreateSource6>(e => { e.ToTable("comp_create_source6"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
        }
    }

    private class CompressedCAggContext6 : DbContext
    {
        public DbSet<CompCreateSource6> Metrics => Set<CompCreateSource6>();
        public DbSet<CompCreateAgg6> Hourly => Set<CompCreateAgg6>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompCreateSource6>(e => { e.ToTable("comp_create_source6"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompCreateAgg6>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompCreateAgg6, CompCreateSource6>("comp_create_cagg6", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.TimeBucket)
                 .WithCompressionOrderBy(s => s.By(x => x.TimeBucket));
            });
        }
    }

    [Fact]
    public void Should_Detect_Compression_On_Newly_Created_CAgg()
    {
        // Arrange
        using NoCAggContext6 source = new();
        using CompressedCAggContext6 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        CreateContinuousAggregateOperation? create = ops.OfType<CreateContinuousAggregateOperation>().SingleOrDefault();
        Assert.NotNull(create);
        Assert.True(create.EnableCompression);
        Assert.NotNull(create.CompressionSegmentBy);
        Assert.NotEmpty(create.CompressionSegmentBy);
        Assert.NotNull(create.CompressionOrderBy);
        Assert.NotEmpty(create.CompressionOrderBy);
    }

    #endregion

    #region Should_Populate_OldCompression_Values_In_Alter

    private class CompOldValSource7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Region { get; set; } = string.Empty;
    }

    private class CompOldValAgg7
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = string.Empty;
    }

    private class OldValInitialContext7 : DbContext
    {
        public DbSet<CompOldValSource7> Metrics => Set<CompOldValSource7>();
        public DbSet<CompOldValAgg7> Hourly => Set<CompOldValAgg7>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompOldValSource7>(e => { e.ToTable("comp_oldval_source7"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompOldValAgg7>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompOldValAgg7, CompOldValSource7>("comp_oldval_cagg7", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.Region)
                 .WithCompressionOrderBy(s => s.By(x => x.TimeBucket));
            });
        }
    }

    private class OldValModifiedContext7 : DbContext
    {
        public DbSet<CompOldValSource7> Metrics => Set<CompOldValSource7>();
        public DbSet<CompOldValAgg7> Hourly => Set<CompOldValAgg7>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompOldValSource7>(e => { e.ToTable("comp_oldval_source7"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompOldValAgg7>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompOldValAgg7, CompOldValSource7>("comp_oldval_cagg7", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.TimeBucket)
                 .WithCompressionOrderBy(s => s.ByDescending(x => x.TimeBucket));
            });
        }
    }

    [Fact]
    public void Should_Populate_OldCompression_Values_In_Alter()
    {
        // Arrange
        using OldValInitialContext7 source = new();
        using OldValModifiedContext7 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        AlterContinuousAggregateOperation? alter = ops.OfType<AlterContinuousAggregateOperation>().SingleOrDefault();
        Assert.NotNull(alter);
        Assert.NotNull(alter.OldCompressionSegmentBy);
        Assert.NotEmpty(alter.OldCompressionSegmentBy);
        Assert.NotNull(alter.OldCompressionOrderBy);
        Assert.NotEmpty(alter.OldCompressionOrderBy);
    }

    #endregion

    #region Should_Treat_Implicit_And_Explicit_ASC_As_Equal

    private class CompOrderNormSource8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompOrderNormAgg8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ImplicitAscContext8 : DbContext
    {
        public DbSet<CompOrderNormSource8> Metrics => Set<CompOrderNormSource8>();
        public DbSet<CompOrderNormAgg8> Hourly => Set<CompOrderNormAgg8>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompOrderNormSource8>(e => { e.ToTable("comp_ordnorm_source8"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompOrderNormAgg8>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompOrderNormAgg8, CompOrderNormSource8>("comp_ordnorm_cagg8", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(s => s.By(x => x.TimeBucket));
            });
        }
    }

    private class ExplicitAscContext8 : DbContext
    {
        public DbSet<CompOrderNormSource8> Metrics => Set<CompOrderNormSource8>();
        public DbSet<CompOrderNormAgg8> Hourly => Set<CompOrderNormAgg8>();

        protected override void OnConfiguring(DbContextOptionsBuilder b) => BaseOptions(b);

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompOrderNormSource8>(e => { e.ToTable("comp_ordnorm_source8"); e.HasNoKey(); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompOrderNormAgg8>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<CompOrderNormAgg8, CompOrderNormSource8>("comp_ordnorm_cagg8", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(new OrderBy("TimeBucket", isAscending: true));
            });
        }
    }

    [Fact]
    public void Should_Treat_Implicit_And_Explicit_ASC_As_Equal()
    {
        // Arrange
        using ImplicitAscContext8 source = new();
        using ExplicitAscContext8 target = new();

        // Act
        IReadOnlyList<MigrationOperation> ops = new ContinuousAggregateDiffer()
            .GetDifferences(GetModel(source), GetModel(target));

        // Assert
        Assert.DoesNotContain(ops, op => op is AlterContinuousAggregateOperation);
    }

    #endregion
}

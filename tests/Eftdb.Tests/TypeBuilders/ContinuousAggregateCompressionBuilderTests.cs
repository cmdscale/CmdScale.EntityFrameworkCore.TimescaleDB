using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

public class ContinuousAggregateCompressionBuilderTests
{
    private static IModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    private const string ConnStr = "Host=localhost;Database=test;Username=test;Password=test";

    // ── WithCompression fluent API ────────────────────────────────────────────

    #region WithCompression_SetsEnableCompressionAnnotation

    private class WCSource1 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class WCAgg1 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class WithCompressionContext1 : DbContext
    {
        public DbSet<WCSource1> Metrics => Set<WCSource1>();
        public DbSet<WCAgg1> Hourly => Set<WCAgg1>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<WCSource1>(e => { e.HasNoKey(); e.ToTable("wc_src1"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<WCAgg1>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<WCAgg1, WCSource1>("wc_cagg1", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompression();
            });
        }
    }

    [Fact]
    public void WithCompression_SetsEnableCompressionAnnotation()
    {
        // Arrange
        using WithCompressionContext1 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(WCAgg1))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region WithCompression_False_SetsAnnotationFalse

    private class WCFSource2 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class WCFAgg2 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class WithCompressionFalseContext2 : DbContext
    {
        public DbSet<WCFSource2> Metrics => Set<WCFSource2>();
        public DbSet<WCFAgg2> Hourly => Set<WCFAgg2>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<WCFSource2>(e => { e.HasNoKey(); e.ToTable("wcf_src2"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<WCFAgg2>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<WCFAgg2, WCFSource2>("wcf_cagg2", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompression(false);
            });
        }
    }

    [Fact]
    public void WithCompression_False_SetsAnnotationFalse()
    {
        // Arrange
        using WithCompressionFalseContext2 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(WCFAgg2))!;

        // Assert
        Assert.Equal(false, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region WithCompressionSegmentBy_SetsAnnotationAndImpliesEnable

    private class SegSrc3 { public DateTime Timestamp { get; set; } public double Value { get; set; } public string Region { get; set; } = ""; }
    private class SegAgg3 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } public string Region { get; set; } = ""; }

    private class SegmentByContext3 : DbContext
    {
        public DbSet<SegSrc3> Metrics => Set<SegSrc3>();
        public DbSet<SegAgg3> Hourly => Set<SegAgg3>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<SegSrc3>(e => { e.HasNoKey(); e.ToTable("seg_src3"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<SegAgg3>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<SegAgg3, SegSrc3>("seg_cagg3", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.Region);
            });
        }
    }

    [Fact]
    public void WithCompressionSegmentBy_SetsAnnotationAndImpliesEnable()
    {
        // Arrange
        using SegmentByContext3 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(SegAgg3))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        string? segBy = et.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        Assert.NotNull(segBy);
        Assert.Contains("Region", segBy);
    }

    #endregion

    #region WithCompressionSegmentBy_MultipleColumns_JoinedWithComma

    private class MultiSegSrc4 { public DateTime Timestamp { get; set; } public double Value { get; set; } public string Region { get; set; } = ""; public string Device { get; set; } = ""; }
    private class MultiSegAgg4 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } public string Region { get; set; } = ""; public string Device { get; set; } = ""; }

    private class MultiSegmentByContext4 : DbContext
    {
        public DbSet<MultiSegSrc4> Metrics => Set<MultiSegSrc4>();
        public DbSet<MultiSegAgg4> Hourly => Set<MultiSegAgg4>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<MultiSegSrc4>(e => { e.HasNoKey(); e.ToTable("multiseg_src4"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<MultiSegAgg4>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<MultiSegAgg4, MultiSegSrc4>("multiseg_cagg4", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionSegmentBy(x => x.Region, x => x.Device);
            });
        }
    }

    [Fact]
    public void WithCompressionSegmentBy_MultipleColumns_JoinedWithComma()
    {
        // Arrange
        using MultiSegmentByContext4 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(MultiSegAgg4))!;

        // Assert
        string? segBy = et.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        Assert.NotNull(segBy);
        Assert.Contains("Region", segBy);
        Assert.Contains("Device", segBy);
        Assert.Contains(",", segBy);
    }

    #endregion

    #region WithCompressionOrderBy_SetsAnnotationAndImpliesEnable

    private class OrdSrc5 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class OrdAgg5 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class OrderByContext5 : DbContext
    {
        public DbSet<OrdSrc5> Metrics => Set<OrdSrc5>();
        public DbSet<OrdAgg5> Hourly => Set<OrdAgg5>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<OrdSrc5>(e => { e.HasNoKey(); e.ToTable("ord_src5"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<OrdAgg5>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<OrdAgg5, OrdSrc5>("ord_cagg5", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(s => s.By(x => x.TimeBucket));
            });
        }
    }

    [Fact]
    public void WithCompressionOrderBy_SetsAnnotationAndImpliesEnable()
    {
        // Arrange
        using OrderByContext5 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(OrdAgg5))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        string? orderBy = et.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        Assert.NotNull(orderBy);
        Assert.Contains("TimeBucket", orderBy);
    }

    #endregion

    #region WithCompressionOrderBy_Desc_ContainsDesc

    private class OrdDescSrc6 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class OrdDescAgg6 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class OrderByDescContext6 : DbContext
    {
        public DbSet<OrdDescSrc6> Metrics => Set<OrdDescSrc6>();
        public DbSet<OrdDescAgg6> Hourly => Set<OrdDescAgg6>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<OrdDescSrc6>(e => { e.HasNoKey(); e.ToTable("ord_desc_src6"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<OrdDescAgg6>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<OrdDescAgg6, OrdDescSrc6>("ord_desc_cagg6", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(s => s.ByDescending(x => x.TimeBucket));
            });
        }
    }

    [Fact]
    public void WithCompressionOrderBy_Desc_ContainsDesc()
    {
        // Arrange
        using OrderByDescContext6 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(OrdDescAgg6))!;

        // Assert
        string? orderBy = et.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        Assert.NotNull(orderBy);
        Assert.Contains("DESC", orderBy);
    }

    #endregion

    #region WithCompressionOrderBy_RawString_Variant_SetsAnnotation

    private class StrOrdSrc7 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class StrOrdAgg7 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class StringOrderByContext7 : DbContext
    {
        public DbSet<StrOrdSrc7> Metrics => Set<StrOrdSrc7>();
        public DbSet<StrOrdAgg7> Hourly => Set<StrOrdAgg7>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<StrOrdSrc7>(e => { e.HasNoKey(); e.ToTable("str_ord_src7"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<StrOrdAgg7>(e =>
            {
                e.HasNoKey();
                // Uses IsContinuousAggregate without type params (string builder route)
                e.IsContinuousAggregate<StrOrdAgg7, StrOrdSrc7>("str_ord_cagg7", "1 hour", x => x.Timestamp)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                 .WithCompressionOrderBy(new OrderBy("time_bucket", isAscending: false));
            });
        }
    }

    [Fact]
    public void WithCompressionOrderBy_RawString_Variant_SetsAnnotation()
    {
        // Arrange
        using StringOrderByContext7 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(StrOrdAgg7))!;

        // Assert
        string? orderBy = et.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        Assert.NotNull(orderBy);
        Assert.Contains("time_bucket", orderBy);
        Assert.Contains("DESC", orderBy);
    }

    #endregion

    // ── ContinuousAggregateStringBuilder (scaffolded code path) ──────────────

    #region StringBuilder_WithCompression_SetsEnableAnnotation

    private class SBSrc8 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class SBAgg8 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class StringBuilderCompressionContext8 : DbContext
    {
        public DbSet<SBSrc8> Metrics => Set<SBSrc8>();
        public DbSet<SBAgg8> Hourly => Set<SBAgg8>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<SBSrc8>(e => { e.HasNoKey(); e.ToTable("sb_src8"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<SBAgg8>(e =>
            {
                e.HasNoKey();
                ContinuousAggregateTypeBuilder.IsContinuousAggregate<SBAgg8>(
                    e, "sb_cagg8", nameof(SBSrc8), "1 hour", "Timestamp")
                 .AddAggregateFunction("AvgValue", "Value", EAggregateFunction.Avg)
                 .WithCompression();
            });
        }
    }

    [Fact]
    public void StringBuilder_WithCompression_SetsEnableAnnotation()
    {
        // Arrange
        using StringBuilderCompressionContext8 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(SBAgg8))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region StringBuilder_WithCompressionSegmentBy_SetsAnnotation

    private class SBSegSrc9 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class SBSegAgg9 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class StringBuilderSegContext9 : DbContext
    {
        public DbSet<SBSegSrc9> Metrics => Set<SBSegSrc9>();
        public DbSet<SBSegAgg9> Hourly => Set<SBSegAgg9>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<SBSegSrc9>(e => { e.HasNoKey(); e.ToTable("sb_seg_src9"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<SBSegAgg9>(e =>
            {
                e.HasNoKey();
                ContinuousAggregateTypeBuilder.IsContinuousAggregate<SBSegAgg9>(
                    e, "sb_seg_cagg9", nameof(SBSegSrc9), "1 hour", "Timestamp")
                 .AddAggregateFunction("AvgValue", "Value", EAggregateFunction.Avg)
                 .WithCompressionSegmentBy("device_id");
            });
        }
    }

    [Fact]
    public void StringBuilder_WithCompressionSegmentBy_SetsAnnotation()
    {
        // Arrange
        using StringBuilderSegContext9 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(SBSegAgg9))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        string? segBy = et.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        Assert.Equal("device_id", segBy);
    }

    #endregion

    #region StringBuilder_WithCompressionOrderBy_SetsAnnotation

    private class SBOrdSrc10 { public DateTime Timestamp { get; set; } public double Value { get; set; } }
    private class SBOrdAgg10 { public DateTime TimeBucket { get; set; } public double AvgValue { get; set; } }

    private class StringBuilderOrderContext10 : DbContext
    {
        public DbSet<SBOrdSrc10> Metrics => Set<SBOrdSrc10>();
        public DbSet<SBOrdAgg10> Hourly => Set<SBOrdAgg10>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<SBOrdSrc10>(e => { e.HasNoKey(); e.ToTable("sb_ord_src10"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<SBOrdAgg10>(e =>
            {
                e.HasNoKey();
                ContinuousAggregateTypeBuilder.IsContinuousAggregate<SBOrdAgg10>(
                    e, "sb_ord_cagg10", nameof(SBOrdSrc10), "1 hour", "Timestamp")
                 .AddAggregateFunction("AvgValue", "Value", EAggregateFunction.Avg)
                 .WithCompressionOrderBy("time_bucket DESC");
            });
        }
    }

    [Fact]
    public void StringBuilder_WithCompressionOrderBy_SetsAnnotation()
    {
        // Arrange
        using StringBuilderOrderContext10 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(SBOrdAgg10))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        string? orderBy = et.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        Assert.Equal("time_bucket DESC", orderBy);
    }

    #endregion
}

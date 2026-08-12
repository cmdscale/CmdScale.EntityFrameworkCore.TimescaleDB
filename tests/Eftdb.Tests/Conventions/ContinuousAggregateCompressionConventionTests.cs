using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

public class ContinuousAggregateCompressionConventionTests
{
    private static IModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    private const string ConnStr = "Host=localhost;Database=test;Username=test;Password=test";

    // ── Attribute parity tests ────────────────────────────────────────────────

    #region Attribute_EnableCompression_SetsAnnotation

    [ContinuousAggregate(MaterializedViewName = "attr_comp_cagg", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp",
        EnableCompression = true)]
    private class AttrCompAgg1
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AttrCompSource1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AttrCompContext1 : DbContext
    {
        public DbSet<AttrCompSource1> Metrics => Set<AttrCompSource1>();
        public DbSet<AttrCompAgg1> Hourly => Set<AttrCompAgg1>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<AttrCompSource1>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<AttrCompAgg1>(e => e.HasNoKey());
        }
    }

    [Fact]
    public void Attribute_EnableCompression_SetsAnnotation()
    {
        // Arrange
        using AttrCompContext1 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(AttrCompAgg1))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Attribute_CompressionSegmentBy_SetsAnnotationAndImpliesEnable

    [ContinuousAggregate(MaterializedViewName = "attr_seg_cagg", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp",
        CompressionSegmentBy = ["Region"])]
    private class AttrSegAgg2
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = "";
    }

    private class AttrSegSource2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AttrSegContext2 : DbContext
    {
        public DbSet<AttrSegSource2> Metrics => Set<AttrSegSource2>();
        public DbSet<AttrSegAgg2> Hourly => Set<AttrSegAgg2>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<AttrSegSource2>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<AttrSegAgg2>(e => e.HasNoKey());
        }
    }

    [Fact]
    public void Attribute_CompressionSegmentBy_SetsAnnotationAndImpliesEnable()
    {
        // Arrange
        using AttrSegContext2 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(AttrSegAgg2))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        string? segBy = et.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        Assert.NotNull(segBy);
        Assert.Contains("Region", segBy);
    }

    #endregion

    #region Attribute_CompressionOrderBy_SetsAnnotationAndImpliesEnable

    [ContinuousAggregate(MaterializedViewName = "attr_ord_cagg", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp",
        CompressionOrderBy = ["TimeBucket DESC"])]
    private class AttrOrdAgg3
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class AttrOrdSource3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AttrOrdContext3 : DbContext
    {
        public DbSet<AttrOrdSource3> Metrics => Set<AttrOrdSource3>();
        public DbSet<AttrOrdAgg3> Hourly => Set<AttrOrdAgg3>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<AttrOrdSource3>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<AttrOrdAgg3>(e => e.HasNoKey());
        }
    }

    [Fact]
    public void Attribute_CompressionOrderBy_SetsAnnotationAndImpliesEnable()
    {
        // Arrange
        using AttrOrdContext3 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(AttrOrdAgg3))!;

        // Assert
        Assert.Equal(true, et.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
        string? orderBy = et.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        Assert.NotNull(orderBy);
        Assert.Contains("TimeBucket", orderBy);
        Assert.Contains("DESC", orderBy);
    }

    #endregion

    #region Attribute_MultipleSegmentBy_JoinedWithComma

    [ContinuousAggregate(MaterializedViewName = "attr_multi_seg_cagg", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp",
        CompressionSegmentBy = ["Region", "DeviceId"])]
    private class AttrMultiSegAgg4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = "";
        public string DeviceId { get; set; } = "";
    }

    private class AttrMultiSegSource4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AttrMultiSegContext4 : DbContext
    {
        public DbSet<AttrMultiSegSource4> Metrics => Set<AttrMultiSegSource4>();
        public DbSet<AttrMultiSegAgg4> Hourly => Set<AttrMultiSegAgg4>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<AttrMultiSegSource4>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<AttrMultiSegAgg4>(e => e.HasNoKey());
        }
    }

    [Fact]
    public void Attribute_MultipleSegmentBy_JoinedWithComma()
    {
        // Arrange
        using AttrMultiSegContext4 ctx = new();

        // Act
        IModel model = GetModel(ctx);
        IEntityType et = model.FindEntityType(typeof(AttrMultiSegAgg4))!;

        // Assert
        string? segBy = et.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        Assert.NotNull(segBy);
        Assert.Contains("Region", segBy);
        Assert.Contains("DeviceId", segBy);
    }

    #endregion

    // ── CompressionPolicyConvention prerequisite guard ────────────────────────

    #region CompressionPolicyConvention_Without_Compression_Throws_With_CAgg_Message

    [ContinuousAggregate(MaterializedViewName = "no_comp_cagg_policy", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp")]
    [CompressionPolicy("7 days")]
    private class NoCompCAggPolicy5
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoCompCAggPolicySource5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoCompCAggPolicyContext5 : DbContext
    {
        public DbSet<NoCompCAggPolicySource5> Metrics => Set<NoCompCAggPolicySource5>();
        public DbSet<NoCompCAggPolicy5> Hourly => Set<NoCompCAggPolicy5>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<NoCompCAggPolicySource5>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<NoCompCAggPolicy5>(e => e.HasNoKey());
        }
    }

    [Fact]
    public void CompressionPolicyConvention_Without_Compression_Throws_With_CAgg_Message()
    {
        // Arrange
        using NoCompCAggPolicyContext5 ctx = new();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => GetModel(ctx));
        Assert.Contains("continuous aggregate", ex.Message);
        Assert.Contains("IsContinuousAggregate", ex.Message);
        Assert.Contains("WithCompression", ex.Message);
    }

    #endregion

    #region CompressionPolicyConvention_With_Compression_Does_Not_Throw

    [ContinuousAggregate(MaterializedViewName = "comp_cagg_policy_ok", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp",
        EnableCompression = true)]
    [CompressionPolicy("7 days")]
    private class CompCAggPolicyOk6
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CompCAggPolicySource6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompCAggPolicyContext6 : DbContext
    {
        public DbSet<CompCAggPolicySource6> Metrics => Set<CompCAggPolicySource6>();
        public DbSet<CompCAggPolicyOk6> Hourly => Set<CompCAggPolicyOk6>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<CompCAggPolicySource6>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<CompCAggPolicyOk6>(e => e.HasNoKey());
        }
    }

    [Fact]
    public void CompressionPolicyConvention_With_Compression_Does_Not_Throw()
    {
        // Arrange
        using CompCAggPolicyContext6 ctx = new();

        // Act
        IModel model = GetModel(ctx);

        // Assert
        Assert.NotNull(model.FindEntityType(typeof(CompCAggPolicyOk6)));
    }

    #endregion

    #region Hypertable_With_CompressionPolicy_And_No_Compression_Does_Not_Throw

    [Hypertable("Timestamp")]
    [CompressionPolicy("14 days")]
    private class HtNoCompEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HtNoCompPolicyContext7 : DbContext
    {
        public DbSet<HtNoCompEntity7> Metrics => Set<HtNoCompEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<HtNoCompEntity7>(e => { e.HasNoKey(); e.ToTable("ht_no_comp7"); });
        }
    }

    [Fact]
    public void Hypertable_With_CompressionPolicy_And_No_Compression_Does_Not_Throw()
    {
        // Arrange
        using HtNoCompPolicyContext7 ctx = new();

        // Act
        IModel model = GetModel(ctx);

        // Assert
        Assert.NotNull(model.FindEntityType(typeof(HtNoCompEntity7)));
    }

    #endregion

    // ── Attribute/fluent parity ───────────────────────────────────────────────

    #region Attribute_And_FluentAPI_Produce_Same_CompressionAnnotations

    private class ParitySource8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    [ContinuousAggregate(MaterializedViewName = "parity_attr_cagg", ParentName = "Metrics",
        TimeBucketWidth = "1 hour", TimeBucketSourceColumn = "Timestamp",
        EnableCompression = true,
        CompressionSegmentBy = ["Region"],
        CompressionOrderBy = ["TimeBucket DESC"])]
    private class ParityAttrAgg8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = "";
    }

    private class ParityFluentAgg8
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = "";
    }

    private class ParityAttrContext8 : DbContext
    {
        public DbSet<ParitySource8> Metrics => Set<ParitySource8>();
        public DbSet<ParityAttrAgg8> Hourly => Set<ParityAttrAgg8>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<ParitySource8>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<ParityAttrAgg8>(e => e.HasNoKey());
        }
    }

    private class ParityFluentContext8 : DbContext
    {
        public DbSet<ParitySource8> Metrics => Set<ParitySource8>();
        public DbSet<ParityFluentAgg8> Hourly => Set<ParityFluentAgg8>();

        protected override void OnConfiguring(DbContextOptionsBuilder b)
            => b.UseNpgsql(ConnStr).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder m)
        {
            m.Entity<ParitySource8>(e => { e.HasNoKey(); e.ToTable("Metrics"); e.IsHypertable(x => x.Timestamp); });
            m.Entity<ParityFluentAgg8>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<ParityFluentAgg8, ParitySource8>("parity_fluent_cagg", "1 hour", x => x.Timestamp)
                 .WithCompressionSegmentBy(x => x.Region)
                 .WithCompressionOrderBy(s => s.ByDescending(x => x.TimeBucket));
            });
        }
    }

    [Fact]
    public void Attribute_And_FluentAPI_Produce_Same_CompressionAnnotations()
    {
        // Arrange
        using ParityAttrContext8 attrCtx = new();
        using ParityFluentContext8 fluentCtx = new();

        // Act
        IModel attrModel = GetModel(attrCtx);
        IModel fluentModel = GetModel(fluentCtx);
        IEntityType attrEt = attrModel.FindEntityType(typeof(ParityAttrAgg8))!;
        IEntityType fluentEt = fluentModel.FindEntityType(typeof(ParityFluentAgg8))!;

        // Assert
        Assert.Equal(
            attrEt.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value,
            fluentEt.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);

        string? attrSeg = attrEt.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        string? fluentSeg = fluentEt.FindAnnotation(HypertableAnnotations.CompressionSegmentBy)?.Value as string;
        Assert.NotNull(attrSeg);
        Assert.NotNull(fluentSeg);
        Assert.Contains("Region", attrSeg);
        Assert.Contains("Region", fluentSeg);

        string? attrOrd = attrEt.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        string? fluentOrd = fluentEt.FindAnnotation(HypertableAnnotations.CompressionOrderBy)?.Value as string;
        Assert.NotNull(attrOrd);
        Assert.NotNull(fluentOrd);
        Assert.Contains("DESC", attrOrd);
        Assert.Contains("DESC", fluentOrd);
    }

    #endregion
}

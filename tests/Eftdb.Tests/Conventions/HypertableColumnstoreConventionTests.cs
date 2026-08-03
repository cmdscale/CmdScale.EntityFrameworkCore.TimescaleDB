using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

public class HypertableColumnstoreConventionTests
{
    private static IModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    // ── Fluent API: WithSparseIndex ──

    #region Should_Set_SparseIndex_Annotation_Via_FluentApi

    private class SparseIndexFluentEntity { public DateTime Ts { get; set; } }

    private class SparseIndexFluentContext : DbContext
    {
        public DbSet<SparseIndexFluentEntity> Items => Set<SparseIndexFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexFluentEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_sparse_fluent");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id), minmax(temperature)");
            });
    }

    [Fact]
    public void Should_Set_SparseIndex_Annotation_Via_FluentApi()
    {
        // Arrange
        using SparseIndexFluentContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SparseIndexFluentEntity))!;

        // Assert
        Assert.Equal("bloom(device_id), minmax(temperature)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
    }

    #endregion

    #region Should_Implicitly_Enable_Compression_When_SparseIndex_Set_Via_FluentApi

    private class SparseIndexImplicitCompressionEntity { public DateTime Ts { get; set; } }

    private class SparseIndexImplicitCompressionContext : DbContext
    {
        public DbSet<SparseIndexImplicitCompressionEntity> Items => Set<SparseIndexImplicitCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexImplicitCompressionEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_sparse_implicit_comp");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(col)");
            });
    }

    [Fact]
    public void Should_Implicitly_Enable_Compression_When_SparseIndex_Set_Via_FluentApi()
    {
        // Arrange
        using SparseIndexImplicitCompressionContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SparseIndexImplicitCompressionEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Preserve_Empty_String_SparseIndex_Via_FluentApi

    private class EmptySparseIndexFluentEntity { public DateTime Ts { get; set; } }

    private class EmptySparseIndexFluentContext : DbContext
    {
        public DbSet<EmptySparseIndexFluentEntity> Items => Set<EmptySparseIndexFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EmptySparseIndexFluentEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_sparse_empty");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(string.Empty);
            });
    }

    [Fact]
    public void Should_Preserve_Empty_String_SparseIndex_Via_FluentApi()
    {
        // Arrange
        using EmptySparseIndexFluentContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EmptySparseIndexFluentEntity))!;

        // Assert
        Assert.Equal(string.Empty, entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    // ── Fluent API: WithCompressChunkTimeInterval ──

    #region Should_Set_CompressChunkTimeInterval_Annotation_Via_FluentApi

    private class CompressChunkIntervalFluentEntity { public DateTime Ts { get; set; } }

    private class CompressChunkIntervalFluentContext : DbContext
    {
        public DbSet<CompressChunkIntervalFluentEntity> Items => Set<CompressChunkIntervalFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressChunkIntervalFluentEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_ccti_fluent");
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("24 hours");
            });
    }

    [Fact]
    public void Should_Set_CompressChunkTimeInterval_Annotation_Via_FluentApi()
    {
        // Arrange
        using CompressChunkIntervalFluentContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CompressChunkIntervalFluentEntity))!;

        // Assert
        Assert.Equal("24 hours", entityType.FindAnnotation(HypertableAnnotations.CompressChunkTimeInterval)?.Value);
    }

    #endregion

    #region Should_Implicitly_Enable_Compression_When_CompressChunkTimeInterval_Set_Via_FluentApi

    private class CompressChunkIntervalImplicitCompressionEntity { public DateTime Ts { get; set; } }

    private class CompressChunkIntervalImplicitCompressionContext : DbContext
    {
        public DbSet<CompressChunkIntervalImplicitCompressionEntity> Items => Set<CompressChunkIntervalImplicitCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressChunkIntervalImplicitCompressionEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_ccti_implicit_comp");
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("7 days");
            });
    }

    [Fact]
    public void Should_Implicitly_Enable_Compression_When_CompressChunkTimeInterval_Set_Via_FluentApi()
    {
        // Arrange
        using CompressChunkIntervalImplicitCompressionContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CompressChunkIntervalImplicitCompressionEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Throw_When_WithCompressChunkTimeInterval_Called_With_Null

    private class CompressChunkIntervalNullThrowEntity { public DateTime Ts { get; set; } }

    private class CompressChunkIntervalNullThrowContext : DbContext
    {
        public DbSet<CompressChunkIntervalNullThrowEntity> Items => Set<CompressChunkIntervalNullThrowEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressChunkIntervalNullThrowEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_ccti_null_throw");
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval(null!);
            });
    }

    [Fact]
    public void Should_Throw_When_WithCompressChunkTimeInterval_Called_With_Null()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            using CompressChunkIntervalNullThrowContext context = new();
            _ = GetModel(context);
        });
    }

    #endregion

    #region Should_Throw_When_WithCompressChunkTimeInterval_Called_With_Whitespace

    private class CompressChunkIntervalWhitespaceThrowEntity { public DateTime Ts { get; set; } }

    private class CompressChunkIntervalWhitespaceThrowContext : DbContext
    {
        public DbSet<CompressChunkIntervalWhitespaceThrowEntity> Items => Set<CompressChunkIntervalWhitespaceThrowEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressChunkIntervalWhitespaceThrowEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_ccti_ws_throw");
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("   ");
            });
    }

    [Fact]
    public void Should_Throw_When_WithCompressChunkTimeInterval_Called_With_Whitespace()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() =>
        {
            using CompressChunkIntervalWhitespaceThrowContext context = new();
            _ = GetModel(context);
        });
    }

    #endregion

    // ── Attribute: SparseIndex ──

    #region Should_Set_SparseIndex_Annotation_Via_Attribute

    [Hypertable("Ts")]
    [SparseIndex(ESparseIndexType.Bloom, "device_id")]
    private class SparseIndexAttributeEntity { public DateTime Ts { get; set; } }

    private class SparseIndexAttributeContext : DbContext
    {
        public DbSet<SparseIndexAttributeEntity> Items => Set<SparseIndexAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexAttributeEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_sparse_attr");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public void Should_Set_SparseIndex_Annotation_Via_Attribute()
    {
        // Arrange
        using SparseIndexAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SparseIndexAttributeEntity))!;

        // Assert
        Assert.Equal("bloom(device_id)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Preserve_Empty_SparseIndex_Via_Attribute

    [Hypertable("Ts", DisableAutoSparseIndexes = true)]
    private class EmptySparseIndexAttributeEntity { public DateTime Ts { get; set; } }

    private class EmptySparseIndexAttributeContext : DbContext
    {
        public DbSet<EmptySparseIndexAttributeEntity> Items => Set<EmptySparseIndexAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EmptySparseIndexAttributeEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_sparse_attr_empty");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public void Should_Preserve_Empty_SparseIndex_Via_Attribute()
    {
        // Arrange
        using EmptySparseIndexAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(EmptySparseIndexAttributeEntity))!;

        // Assert
        Assert.Equal(string.Empty, entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Not_Set_SparseIndex_Annotation_When_Attribute_Property_Is_Null

    [Hypertable("Ts")]
    private class NullSparseIndexAttributeEntity { public DateTime Ts { get; set; } }

    private class NullSparseIndexAttributeContext : DbContext
    {
        public DbSet<NullSparseIndexAttributeEntity> Items => Set<NullSparseIndexAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullSparseIndexAttributeEntity>(e => { e.HasNoKey(); e.ToTable("conv_sparse_attr_null"); });
    }

    [Fact]
    public void Should_Not_Set_SparseIndex_Annotation_When_Attribute_Property_Is_Null()
    {
        // Arrange
        using NullSparseIndexAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NullSparseIndexAttributeEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    // ── Attribute: CompressChunkTimeInterval ──

    #region Should_Set_CompressChunkTimeInterval_Annotation_Via_Attribute

    [Hypertable("Ts", CompressChunkTimeInterval = "24 hours")]
    private class CompressChunkIntervalAttributeEntity { public DateTime Ts { get; set; } }

    private class CompressChunkIntervalAttributeContext : DbContext
    {
        public DbSet<CompressChunkIntervalAttributeEntity> Items => Set<CompressChunkIntervalAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressChunkIntervalAttributeEntity>(e => { e.HasNoKey(); e.ToTable("conv_ccti_attr"); });
    }

    [Fact]
    public void Should_Set_CompressChunkTimeInterval_Annotation_Via_Attribute()
    {
        // Arrange
        using CompressChunkIntervalAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CompressChunkIntervalAttributeEntity))!;

        // Assert
        Assert.Equal("24 hours", entityType.FindAnnotation(HypertableAnnotations.CompressChunkTimeInterval)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Not_Set_CompressChunkTimeInterval_Annotation_When_Attribute_Property_Is_Null

    [Hypertable("Ts")]
    private class NullCompressChunkIntervalAttributeEntity { public DateTime Ts { get; set; } }

    private class NullCompressChunkIntervalAttributeContext : DbContext
    {
        public DbSet<NullCompressChunkIntervalAttributeEntity> Items => Set<NullCompressChunkIntervalAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullCompressChunkIntervalAttributeEntity>(e => { e.HasNoKey(); e.ToTable("conv_ccti_attr_null"); });
    }

    [Fact]
    public void Should_Not_Set_CompressChunkTimeInterval_Annotation_When_Attribute_Property_Is_Null()
    {
        // Arrange
        using NullCompressChunkIntervalAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NullCompressChunkIntervalAttributeEntity))!;

        // Assert
        Assert.Null(entityType.FindAnnotation(HypertableAnnotations.CompressChunkTimeInterval));
    }

    #endregion

    // ── Attribute path: Fluent vs Attribute equivalence ──

    #region FluentApi_And_Attribute_Produce_Same_Annotations_For_SparseIndex

    [Hypertable("Ts")]
    [SparseIndex(ESparseIndexType.Bloom, "device_id")]
    private class SparseIndexEquivAttributeEntity { public DateTime Ts { get; set; } }

    private class SparseIndexEquivFluentEntity { public DateTime Ts { get; set; } }

    private class SparseIndexEquivAttributeContext : DbContext
    {
        public DbSet<SparseIndexEquivAttributeEntity> Items => Set<SparseIndexEquivAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexEquivAttributeEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("equiv_sparse");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    private class SparseIndexEquivFluentContext : DbContext
    {
        public DbSet<SparseIndexEquivFluentEntity> Items => Set<SparseIndexEquivFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexEquivFluentEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("equiv_sparse");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    [Fact]
    public void FluentApi_And_Attribute_Produce_Same_Annotations_For_SparseIndex()
    {
        // Arrange
        using SparseIndexEquivAttributeContext attrContext = new();
        using SparseIndexEquivFluentContext fluentContext = new();

        // Act
        IEntityType attrEntity = GetModel(attrContext).FindEntityType(typeof(SparseIndexEquivAttributeEntity))!;
        IEntityType fluentEntity = GetModel(fluentContext).FindEntityType(typeof(SparseIndexEquivFluentEntity))!;

        // Assert
        Assert.Equal(
            attrEntity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(
            attrEntity.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value,
            fluentEntity.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    // ── Typed fluent API: selector form ──

    #region Should_Set_Bloom_Annotation_Via_Selector_FluentApi

    private class SelectorBloomEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class SelectorBloomContext : DbContext
    {
        public DbSet<SelectorBloomEntity> Items => Set<SelectorBloomEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SelectorBloomEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_selector_bloom");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(s => s.Bloom(x => x.DeviceId));
            });
    }

    [Fact]
    public void Should_Set_Bloom_Annotation_Via_Selector_FluentApi()
    {
        // Arrange
        using SelectorBloomContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SelectorBloomEntity))!;

        // Assert
        Assert.Equal("bloom(DeviceId)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Set_MinMax_Annotation_Via_Selector_FluentApi

    private class SelectorMinMaxEntity { public DateTime Ts { get; set; } public double Value { get; set; } }

    private class SelectorMinMaxContext : DbContext
    {
        public DbSet<SelectorMinMaxEntity> Items => Set<SelectorMinMaxEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SelectorMinMaxEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_selector_minmax");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(s => s.MinMax(x => x.Value));
            });
    }

    [Fact]
    public void Should_Set_MinMax_Annotation_Via_Selector_FluentApi()
    {
        // Arrange
        using SelectorMinMaxContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SelectorMinMaxEntity))!;

        // Assert
        Assert.Equal("minmax(Value)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Set_Mixed_Bloom_MinMax_Annotation_Via_Selector_FluentApi

    private class SelectorMixedEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } public double Value { get; set; } }

    private class SelectorMixedContext : DbContext
    {
        public DbSet<SelectorMixedEntity> Items => Set<SelectorMixedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SelectorMixedEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_selector_mixed");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(
                        s => s.Bloom(x => x.DeviceId),
                        s => s.MinMax(x => x.Value));
            });
    }

    [Fact]
    public void Should_Set_Mixed_Bloom_MinMax_Annotation_Via_Selector_FluentApi()
    {
        // Arrange
        using SelectorMixedContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SelectorMixedEntity))!;

        // Assert
        Assert.Equal("bloom(DeviceId), minmax(Value)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Set_Composite_Bloom_Annotation_Via_Selector_FluentApi

    private class SelectorCompositeBloomEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } public string TenantId { get; set; } = string.Empty; }

    private class SelectorCompositeBloomContext : DbContext
    {
        public DbSet<SelectorCompositeBloomEntity> Items => Set<SelectorCompositeBloomEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SelectorCompositeBloomEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_selector_composite_bloom");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(s => s.Bloom(x => x.DeviceId, x => x.TenantId));
            });
    }

    [Fact]
    public void Should_Set_Composite_Bloom_Annotation_Via_Selector_FluentApi()
    {
        // Arrange
        using SelectorCompositeBloomContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(SelectorCompositeBloomEntity))!;

        // Assert
        Assert.Equal("bloom(DeviceId,TenantId)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Set_SparseIndex_Annotation_Via_Array_FluentApi

    private class ArrayFormEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } public double Value { get; set; } }

    private class ArrayFormContext : DbContext
    {
        public DbSet<ArrayFormEntity> Items => Set<ArrayFormEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ArrayFormEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_array_form");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(
                        new SparseIndex(ESparseIndexType.Bloom, ["DeviceId"]),
                        new SparseIndex(ESparseIndexType.MinMax, ["Value"]));
            });
    }

    [Fact]
    public void Should_Set_SparseIndex_Annotation_Via_Array_FluentApi()
    {
        // Arrange
        using ArrayFormContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ArrayFormEntity))!;

        // Assert
        Assert.Equal("bloom(DeviceId), minmax(Value)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Set_Empty_Annotation_Via_WithoutAutoSparseIndexes_FluentApi

    private class WithoutAutoSparseIndexesEntity { public DateTime Ts { get; set; } }

    private class WithoutAutoSparseIndexesContext : DbContext
    {
        public DbSet<WithoutAutoSparseIndexesEntity> Items => Set<WithoutAutoSparseIndexesEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<WithoutAutoSparseIndexesEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_without_auto_si");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithoutAutoSparseIndexes();
            });
    }

    [Fact]
    public void Should_Set_Empty_Annotation_Via_WithoutAutoSparseIndexes_FluentApi()
    {
        // Arrange
        using WithoutAutoSparseIndexesContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(WithoutAutoSparseIndexesEntity))!;

        // Assert
        Assert.Equal(string.Empty, entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    // ── Attribute convention: multiple [SparseIndex] attributes ──

    #region Should_Combine_Multiple_SparseIndex_Attributes_In_Declaration_Order

    [Hypertable("Ts")]
    [SparseIndex(ESparseIndexType.Bloom, "DeviceId")]
    [SparseIndex(ESparseIndexType.MinMax, "Value")]
    private class MultiSparseIndexAttributeEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } public double Value { get; set; } }

    private class MultiSparseIndexAttributeContext : DbContext
    {
        public DbSet<MultiSparseIndexAttributeEntity> Items => Set<MultiSparseIndexAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MultiSparseIndexAttributeEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_multi_si_attr");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public void Should_Combine_Multiple_SparseIndex_Attributes_In_Declaration_Order()
    {
        // Arrange
        using MultiSparseIndexAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MultiSparseIndexAttributeEntity))!;

        // Assert
        Assert.Equal("bloom(DeviceId), minmax(Value)", entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    #region Should_Throw_When_SparseIndex_Attribute_And_DisableAutoSparseIndexes_Both_Set

    [Hypertable("Ts", DisableAutoSparseIndexes = true)]
    [SparseIndex(ESparseIndexType.Bloom, "DeviceId")]
    private class ConflictingSparseIndexEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class ConflictingSparseIndexContext : DbContext
    {
        public DbSet<ConflictingSparseIndexEntity> Items => Set<ConflictingSparseIndexEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConflictingSparseIndexEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_conflict_si");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public void Should_Throw_When_SparseIndex_Attribute_And_DisableAutoSparseIndexes_Both_Set()
    {
        // Arrange / Act / Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            using ConflictingSparseIndexContext context = new();
            _ = GetModel(context);
        });
    }

    #endregion

    #region Should_Set_Empty_Annotation_Via_DisableAutoSparseIndexes_Attribute

    [Hypertable("Ts", DisableAutoSparseIndexes = true)]
    private class DisableAutoSparseIndexesAttributeEntity { public DateTime Ts { get; set; } }

    private class DisableAutoSparseIndexesAttributeContext : DbContext
    {
        public DbSet<DisableAutoSparseIndexesAttributeEntity> Items => Set<DisableAutoSparseIndexesAttributeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DisableAutoSparseIndexesAttributeEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_disable_auto_si");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public void Should_Set_Empty_Annotation_Via_DisableAutoSparseIndexes_Attribute()
    {
        // Arrange
        using DisableAutoSparseIndexesAttributeContext context = new();

        // Act
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(DisableAutoSparseIndexesAttributeEntity))!;

        // Assert
        Assert.Equal(string.Empty, entityType.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.EnableCompression)?.Value);
    }

    #endregion

    // ── Validation: minmax with multiple columns ──

    #region Should_Throw_When_MinMax_Has_Multiple_Columns_Via_String_Overload

    private class MinMaxMultiColumnEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } public double Value { get; set; } }

    private class MinMaxMultiColumnContext : DbContext
    {
        public DbSet<MinMaxMultiColumnEntity> Items => Set<MinMaxMultiColumnEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MinMaxMultiColumnEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_minmax_multicol");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("minmax(DeviceId,Value)");
            });
    }

    [Fact]
    public void Should_Throw_When_MinMax_Has_Multiple_Columns_Via_String_Overload()
    {
        // Arrange / Act / Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using MinMaxMultiColumnContext context = new();
            _ = GetModel(context);
        });
        Assert.Contains("minmax supports a single column only", ex.Message);
        Assert.Contains("bloom(...)", ex.Message);
    }

    #endregion

    #region Should_Throw_When_MinMax_Has_Multiple_Columns_Via_SparseIndex_Attribute

    [Hypertable("Ts")]
    [SparseIndex(ESparseIndexType.MinMax, "DeviceId", "Value")]
    private class MinMaxMultiColAttrEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } public double Value { get; set; } }

    private class MinMaxMultiColAttrContext : DbContext
    {
        public DbSet<MinMaxMultiColAttrEntity> Items => Set<MinMaxMultiColAttrEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MinMaxMultiColAttrEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("conv_minmax_multicol_attr");
                e.IsHypertable(x => x.Ts).WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)]);
            });
    }

    [Fact]
    public void Should_Throw_When_MinMax_Has_Multiple_Columns_Via_SparseIndex_Attribute()
    {
        // Arrange / Act / Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using MinMaxMultiColAttrContext context = new();
            _ = GetModel(context);
        });
        Assert.Contains("minmax supports a single column only", ex.Message);
    }

    #endregion
}

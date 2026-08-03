using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators.AnnotationRenderers;

public class HypertableColumnstoreAnnotationRendererTests
{
    private sealed record StubAnnotation(string Name, object? Value) : IAnnotation;

    private static Dictionary<string, IAnnotation> Annotations(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => (IAnnotation)new StubAnnotation(p.Key, p.Value));

    private static IAnnotationCodeGenerator CreateAnnotationCodeGenerator()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
            .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();
        generator.ScaffoldMode = true;
        return generator;
    }

    private static IEntityType GetEntityType<T>(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!;

    private static List<string> CollectMethodChain(MethodCallCodeFragment? fragment)
    {
        List<string> methods = [];
        while (fragment != null) { methods.Add(fragment.Method); fragment = fragment.ChainedCall; }
        return methods;
    }

    // ── Fluent: WithSparseIndex chained when annotation present with value ──

    #region GenerateFluentApiCalls_Chains_WithSparseIndex_When_Annotation_Has_Value

    private class SparseIndexFluentEntity { public DateTime Ts { get; set; } }

    private class SparseIndexFluentContext : DbContext
    {
        public DbSet<SparseIndexFluentEntity> Items => Set<SparseIndexFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_si_value"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithSparseIndex_When_Annotation_Has_Value()
    {
        // Arrange
        using SparseIndexFluentContext context = new();
        IEntityType entityType = GetEntityType<SparseIndexFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(device_id)"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.Contains(nameof(HypertableTypeBuilder.WithSparseIndex), CollectMethodChain(hypertableCall));
    }

    #endregion

    // ── Fluent: WithoutAutoSparseIndexes chained when annotation is empty string ──

    #region GenerateFluentApiCalls_Chains_WithoutAutoSparseIndexes_When_Annotation_Is_Empty_String

    private class SparseIndexEmptyFluentEntity { public DateTime Ts { get; set; } }

    private class SparseIndexEmptyFluentContext : DbContext
    {
        public DbSet<SparseIndexEmptyFluentEntity> Items => Set<SparseIndexEmptyFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexEmptyFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_si_empty"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithoutAutoSparseIndexes_When_Annotation_Is_Empty_String()
    {
        // Arrange
        using SparseIndexEmptyFluentContext context = new();
        IEntityType entityType = GetEntityType<SparseIndexEmptyFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, string.Empty));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithoutAutoSparseIndexes), chain);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.WithSparseIndex), chain);
    }

    #endregion

    // ── Fluent: no WithSparseIndex when annotation absent ──

    #region GenerateFluentApiCalls_Does_Not_Chain_WithSparseIndex_When_Annotation_Absent

    private class NoSparseIndexFluentEntity { public DateTime Ts { get; set; } }

    private class NoSparseIndexFluentContext : DbContext
    {
        public DbSet<NoSparseIndexFluentEntity> Items => Set<NoSparseIndexFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoSparseIndexFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_si_absent"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_WithSparseIndex_When_Annotation_Absent()
    {
        // Arrange
        using NoSparseIndexFluentContext context = new();
        IEntityType entityType = GetEntityType<NoSparseIndexFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.WithSparseIndex), CollectMethodChain(hypertableCall));
    }

    #endregion

    // ── Fluent: WithCompressChunkTimeInterval chained when annotation present ──

    #region GenerateFluentApiCalls_Chains_WithCompressChunkTimeInterval

    private class CctiFluentEntity { public DateTime Ts { get; set; } }

    private class CctiFluentContext : DbContext
    {
        public DbSet<CctiFluentEntity> Items => Set<CctiFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ccti"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressChunkTimeInterval()
    {
        // Arrange
        using CctiFluentContext context = new();
        IEntityType entityType = GetEntityType<CctiFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressChunkTimeInterval, "24 hours"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.Contains(nameof(HypertableTypeBuilder.WithCompressChunkTimeInterval), CollectMethodChain(hypertableCall));
    }

    #endregion

    // ── Fluent: no WithCompressChunkTimeInterval when annotation absent ──

    #region GenerateFluentApiCalls_Does_Not_Chain_WithCompressChunkTimeInterval_When_Absent

    private class NoCctiFluentEntity { public DateTime Ts { get; set; } }

    private class NoCctiFluentContext : DbContext
    {
        public DbSet<NoCctiFluentEntity> Items => Set<NoCctiFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoCctiFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ccti_absent"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_WithCompressChunkTimeInterval_When_Absent()
    {
        // Arrange
        using NoCctiFluentContext context = new();
        IEntityType entityType = GetEntityType<NoCctiFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.WithCompressChunkTimeInterval), CollectMethodChain(hypertableCall));
    }

    #endregion

    // ── Fluent: SparseIndex suppresses standalone EnableCompression ──

    #region GenerateFluentApiCalls_SparseIndex_Suppresses_EnableCompression

    private class SparseIndexSuppressesEnableCompressionEntity { public DateTime Ts { get; set; } }

    private class SparseIndexSuppressesEnableCompressionContext : DbContext
    {
        public DbSet<SparseIndexSuppressesEnableCompressionEntity> Items => Set<SparseIndexSuppressesEnableCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexSuppressesEnableCompressionEntity>(e => { e.HasNoKey(); e.ToTable("fluent_si_suppress"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_SparseIndex_Suppresses_EnableCompression()
    {
        // Arrange
        using SparseIndexSuppressesEnableCompressionContext context = new();
        IEntityType entityType = GetEntityType<SparseIndexSuppressesEnableCompressionEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(device_id)"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithSparseIndex), chain);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.EnableCompression), chain);
    }

    #endregion

    // ── Fluent: CompressChunkTimeInterval suppresses standalone EnableCompression ──

    #region GenerateFluentApiCalls_CompressChunkTimeInterval_Suppresses_EnableCompression

    private class CctiSuppressesEnableCompressionEntity { public DateTime Ts { get; set; } }

    private class CctiSuppressesEnableCompressionContext : DbContext
    {
        public DbSet<CctiSuppressesEnableCompressionEntity> Items => Set<CctiSuppressesEnableCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiSuppressesEnableCompressionEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ccti_suppress"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_CompressChunkTimeInterval_Suppresses_EnableCompression()
    {
        // Arrange
        using CctiSuppressesEnableCompressionContext context = new();
        IEntityType entityType = GetEntityType<CctiSuppressesEnableCompressionEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true),
            (HypertableAnnotations.CompressChunkTimeInterval, "24 hours"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithCompressChunkTimeInterval), chain);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.EnableCompression), chain);
    }

    #endregion

    // ── ConsumeFeatureAnnotations removes both keys ──

    #region ConsumeFeatureAnnotations_Removes_SparseIndex_And_CompressChunkTimeInterval

    private class ConsumeColumnstoreEntity { public DateTime Ts { get; set; } }

    private class ConsumeColumnstoreContext : DbContext
    {
        public DbSet<ConsumeColumnstoreEntity> Items => Set<ConsumeColumnstoreEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConsumeColumnstoreEntity>(e => { e.HasNoKey(); e.ToTable("fluent_consume_cs"); });
    }

    [Fact]
    public void ConsumeFeatureAnnotations_Removes_SparseIndex_And_CompressChunkTimeInterval()
    {
        // Arrange
        using ConsumeColumnstoreContext context = new();
        IEntityType entityType = GetEntityType<ConsumeColumnstoreEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(device_id)"),
            (HypertableAnnotations.CompressChunkTimeInterval, "24 hours"));

        HypertableAnnotationRenderer renderer = new();

        // Act
        renderer.ConsumeFeatureAnnotations(entityType, annotations);

        // Assert
        Assert.DoesNotContain(HypertableAnnotations.CompressionSparseIndex, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.CompressChunkTimeInterval, annotations.Keys);
    }

    #endregion

    // ── Attribute: non-empty annotation emits a separate [SparseIndex] attribute fragment ──

    #region GenerateDataAnnotationAttributes_Emits_SparseIndexAttribute_Fragment_For_NonEmpty_Annotation

    private class AttrSparseIndexEntity { public DateTime Ts { get; set; } }

    private class AttrSparseIndexContext : DbContext
    {
        public DbSet<AttrSparseIndexEntity> Items => Set<AttrSparseIndexEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttrSparseIndexEntity>(e => { e.HasNoKey(); e.ToTable("attr_si_value"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Emits_SparseIndexAttribute_Fragment_For_NonEmpty_Annotation()
    {
        // Arrange
        using AttrSparseIndexContext context = new();
        IEntityType entityType = GetEntityType<AttrSparseIndexEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(device_id)"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? siAttr = result.FirstOrDefault(a => a.Type == typeof(SparseIndexAttribute));
        Assert.NotNull(siAttr);
        Assert.Equal(ESparseIndexType.Bloom, siAttr.Arguments[0]);
        Assert.Equal("device_id", siAttr.Arguments[1]);

        AttributeCodeFragment? htAttr = result.FirstOrDefault(a => a.Type == typeof(HypertableAttribute));
        Assert.NotNull(htAttr);
        Assert.DoesNotContain(nameof(HypertableAttribute.DisableAutoSparseIndexes), htAttr.NamedArguments.Keys);
    }

    #endregion

    // ── Attribute: empty annotation emits DisableAutoSparseIndexes = true on [Hypertable] ──

    #region GenerateDataAnnotationAttributes_Emits_DisableAutoSparseIndexes_For_Empty_Annotation

    private class AttrSparseIndexEmptyEntity { public DateTime Ts { get; set; } }

    private class AttrSparseIndexEmptyContext : DbContext
    {
        public DbSet<AttrSparseIndexEmptyEntity> Items => Set<AttrSparseIndexEmptyEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttrSparseIndexEmptyEntity>(e => { e.HasNoKey(); e.ToTable("attr_si_empty"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Emits_DisableAutoSparseIndexes_For_Empty_Annotation()
    {
        // Arrange
        using AttrSparseIndexEmptyContext context = new();
        IEntityType entityType = GetEntityType<AttrSparseIndexEmptyEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, string.Empty));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? htAttr = result.FirstOrDefault(a => a.Type == typeof(HypertableAttribute));
        Assert.NotNull(htAttr);
        Assert.True(htAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.DisableAutoSparseIndexes)));
        Assert.Equal(true, htAttr.NamedArguments[nameof(HypertableAttribute.DisableAutoSparseIndexes)]);
        Assert.DoesNotContain(result, a => a.Type == typeof(SparseIndexAttribute));
    }

    #endregion

    // ── Attribute: absent annotation emits no SparseIndex or DisableAutoSparseIndexes ──

    #region GenerateDataAnnotationAttributes_Omits_SparseIndex_And_DisableAutoSparseIndexes_When_Annotation_Absent

    private class AttrNoSparseIndexEntity { public DateTime Ts { get; set; } }

    private class AttrNoSparseIndexContext : DbContext
    {
        public DbSet<AttrNoSparseIndexEntity> Items => Set<AttrNoSparseIndexEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttrNoSparseIndexEntity>(e => { e.HasNoKey(); e.ToTable("attr_si_absent"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_SparseIndex_And_DisableAutoSparseIndexes_When_Annotation_Absent()
    {
        // Arrange
        using AttrNoSparseIndexContext context = new();
        IEntityType entityType = GetEntityType<AttrNoSparseIndexEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? htAttr = result.FirstOrDefault(a => a.Type == typeof(HypertableAttribute));
        Assert.NotNull(htAttr);
        Assert.DoesNotContain(nameof(HypertableAttribute.DisableAutoSparseIndexes), htAttr.NamedArguments.Keys);
        Assert.DoesNotContain(result, a => a.Type == typeof(SparseIndexAttribute));
    }

    #endregion

    // ── Attribute: CompressChunkTimeInterval named arg emitted ──

    #region GenerateDataAnnotationAttributes_Emits_CompressChunkTimeInterval_Named_Arg

    private class AttrCctiEntity { public DateTime Ts { get; set; } }

    private class AttrCctiContext : DbContext
    {
        public DbSet<AttrCctiEntity> Items => Set<AttrCctiEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttrCctiEntity>(e => { e.HasNoKey(); e.ToTable("attr_ccti"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Emits_CompressChunkTimeInterval_Named_Arg()
    {
        // Arrange
        using AttrCctiContext context = new();
        IEntityType entityType = GetEntityType<AttrCctiEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressChunkTimeInterval, "24 hours"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? htAttr = result.FirstOrDefault(a => a.Type == typeof(HypertableAttribute));
        Assert.NotNull(htAttr);
        Assert.True(htAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.CompressChunkTimeInterval)));
        Assert.Equal("24 hours", htAttr.NamedArguments[nameof(HypertableAttribute.CompressChunkTimeInterval)]);
    }

    #endregion

    // ── Attribute: CompressChunkTimeInterval absent when annotation not set ──

    #region GenerateDataAnnotationAttributes_Omits_CompressChunkTimeInterval_Named_Arg_When_Absent

    private class AttrNoCctiEntity { public DateTime Ts { get; set; } }

    private class AttrNoCctiContext : DbContext
    {
        public DbSet<AttrNoCctiEntity> Items => Set<AttrNoCctiEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttrNoCctiEntity>(e => { e.HasNoKey(); e.ToTable("attr_ccti_absent"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_CompressChunkTimeInterval_Named_Arg_When_Absent()
    {
        // Arrange
        using AttrNoCctiContext context = new();
        IEntityType entityType = GetEntityType<AttrNoCctiEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? htAttr = result.FirstOrDefault(a => a.Type == typeof(HypertableAttribute));
        Assert.NotNull(htAttr);
        Assert.DoesNotContain(nameof(HypertableAttribute.CompressChunkTimeInterval), htAttr.NamedArguments.Keys);
    }

    #endregion
}

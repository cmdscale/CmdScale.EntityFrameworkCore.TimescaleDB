using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.Hypertable;

/// <summary>
/// Tests for <c>HypertableAnnotationRenderer</c> exercised through the
/// <see cref="TimescaleDbAnnotationCodeGenerator"/> surface.
/// </summary>
public class HypertableAnnotationRendererTests
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

    private static MethodCallCodeFragment? FindChainedCall(IEnumerable<MethodCallCodeFragment> fragments, string method)
    {
        foreach (MethodCallCodeFragment fragment in fragments)
        {
            for (MethodCallCodeFragment? current = fragment; current != null; current = current.ChainedCall)
            {
                if (current.Method == method)
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static string DimensionsJson(params Dimension[] dimensions)
        => JsonSerializer.Serialize(dimensions.ToList());

    // ── GenerateFluentApiCalls ──────────────────────────────────────────────

    #region GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Not_Set

    private class NotHypertableEntity { public DateTime Ts { get; set; } }

    private class NotHypertableContext : DbContext
    {
        public DbSet<NotHypertableEntity> Items => Set<NotHypertableEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NotHypertableEntity>(e => { e.HasNoKey(); e.ToTable("fluent_not_ht"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Not_Set()
    {
        using NotHypertableContext context = new();
        IEntityType entityType = GetEntityType<NotHypertableEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations();

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_TimeColumn_Is_Missing

    private class NoTimeColumnEntity { public DateTime Ts { get; set; } }

    private class NoTimeColumnContext : DbContext
    {
        public DbSet<NoTimeColumnEntity> Items => Set<NoTimeColumnEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoTimeColumnEntity>(e => { e.HasNoKey(); e.ToTable("fluent_no_tc"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_TimeColumn_Is_Missing()
    {
        using NoTimeColumnContext context = new();
        IEntityType entityType = GetEntityType<NoTimeColumnEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
    }

    #endregion

    #region GenerateFluentApiCalls_Minimal_Returns_IsHypertable_Fragment

    private class MinimalFluentEntity { public DateTime Ts { get; set; } }

    private class MinimalFluentContext : DbContext
    {
        public DbSet<MinimalFluentEntity> Items => Set<MinimalFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MinimalFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_minimal"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Minimal_Returns_IsHypertable_Fragment()
    {
        using MinimalFluentContext context = new();
        IEntityType entityType = GetEntityType<MinimalFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.FirstOrDefault(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.NotNull(hypertableCall);
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.IsHypertable), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithChunkTimeInterval

    private class ChunkTimeIntervalFluentEntity { public DateTime Ts { get; set; } }

    private class ChunkTimeIntervalFluentContext : DbContext
    {
        public DbSet<ChunkTimeIntervalFluentEntity> Items => Set<ChunkTimeIntervalFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ChunkTimeIntervalFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_cti"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithChunkTimeInterval()
    {
        using ChunkTimeIntervalFluentContext context = new();
        IEntityType entityType = GetEntityType<ChunkTimeIntervalFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "1 day"));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithChunkTimeInterval), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithCompressionSegmentBy

    private class SegmentByFluentEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class SegmentByFluentContext : DbContext
    {
        public DbSet<SegmentByFluentEntity> Items => Set<SegmentByFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SegmentByFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_segby"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressionSegmentBy()
    {
        using SegmentByFluentContext context = new();
        IEntityType entityType = GetEntityType<SegmentByFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSegmentBy, "DeviceId"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithCompressionSegmentBy), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithCompressionOrderBy_Ascending

    private class OrderByAscFluentEntity { public DateTime EventTime { get; set; } }

    private class OrderByAscFluentContext : DbContext
    {
        public DbSet<OrderByAscFluentEntity> Items => Set<OrderByAscFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByAscFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ob_asc"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressionOrderBy_Ascending()
    {
        using OrderByAscFluentContext context = new();
        IEntityType entityType = GetEntityType<OrderByAscFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "EventTime"),
            (HypertableAnnotations.CompressionOrderBy, "EventTime ASC"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithCompressionOrderBy), chain);

        MethodCallCodeFragment? current = hypertableCall;
        MethodCallCodeFragment? orderByCall = null;
        while (current != null)
        {
            if (current.Method == nameof(HypertableTypeBuilder.WithCompressionOrderBy))
            {
                orderByCall = current;
                break;
            }
            current = current.ChainedCall;
        }
        Assert.NotNull(orderByCall);
        NestedClosureCodeFragment closure = Assert.IsType<NestedClosureCodeFragment>(orderByCall.Arguments[0]);
        Assert.Equal(nameof(OrderBySelector<>.ByAscending), closure.MethodCalls[0].Method);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithCompressionOrderBy_Descending

    private class OrderByDescFluentEntity { public int DeviceId { get; set; } public DateTime Ts { get; set; } }

    private class OrderByDescFluentContext : DbContext
    {
        public DbSet<OrderByDescFluentEntity> Items => Set<OrderByDescFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByDescFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ob_desc"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressionOrderBy_Descending()
    {
        using OrderByDescFluentContext context = new();
        IEntityType entityType = GetEntityType<OrderByDescFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "DeviceId DESC"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        MethodCallCodeFragment? current = hypertableCall;
        MethodCallCodeFragment? orderByCall = null;
        while (current != null)
        {
            if (current.Method == nameof(HypertableTypeBuilder.WithCompressionOrderBy)) { orderByCall = current; break; }
            current = current.ChainedCall;
        }
        Assert.NotNull(orderByCall);
        NestedClosureCodeFragment closure = Assert.IsType<NestedClosureCodeFragment>(orderByCall.Arguments[0]);
        Assert.Equal(nameof(OrderBySelector<>.ByDescending), closure.MethodCalls[0].Method);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithCompressionOrderBy_NoDirection

    private class OrderByNoDirFluentEntity { public DateTime EventTime { get; set; } public DateTime Ts { get; set; } }

    private class OrderByNoDirFluentContext : DbContext
    {
        public DbSet<OrderByNoDirFluentEntity> Items => Set<OrderByNoDirFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByNoDirFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ob_nodir"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressionOrderBy_NoDirection()
    {
        using OrderByNoDirFluentContext context = new();
        IEntityType entityType = GetEntityType<OrderByNoDirFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "EventTime"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        MethodCallCodeFragment? current = hypertableCall;
        MethodCallCodeFragment? orderByCall = null;
        while (current != null)
        {
            if (current.Method == nameof(HypertableTypeBuilder.WithCompressionOrderBy)) { orderByCall = current; break; }
            current = current.ChainedCall;
        }
        Assert.NotNull(orderByCall);
        NestedClosureCodeFragment closure = Assert.IsType<NestedClosureCodeFragment>(orderByCall.Arguments[0]);
        Assert.Equal(nameof(OrderBySelector<>.By), closure.MethodCalls[0].Method);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithCompressionOrderBy_NullsFirst

    private class OrderByNullsFirstFluentEntity { public DateTime EventTime { get; set; } public DateTime Ts { get; set; } }

    private class OrderByNullsFirstFluentContext : DbContext
    {
        public DbSet<OrderByNullsFirstFluentEntity> Items => Set<OrderByNullsFirstFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByNullsFirstFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ob_nullsfirst"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressionOrderBy_NullsFirst()
    {
        using OrderByNullsFirstFluentContext context = new();
        IEntityType entityType = GetEntityType<OrderByNullsFirstFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "EventTime ASC NULLS FIRST"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        MethodCallCodeFragment? current = hypertableCall;
        MethodCallCodeFragment? orderByCall = null;
        while (current != null)
        {
            if (current.Method == nameof(HypertableTypeBuilder.WithCompressionOrderBy)) { orderByCall = current; break; }
            current = current.ChainedCall;
        }
        Assert.NotNull(orderByCall);
        NestedClosureCodeFragment closure = Assert.IsType<NestedClosureCodeFragment>(orderByCall.Arguments[0]);
        Assert.Equal(nameof(OrderBySelector<>.ByAscending), closure.MethodCalls[0].Method);
        Assert.Equal(2, closure.MethodCalls[0].Arguments.Count);
        Assert.Equal(true, closure.MethodCalls[0].Arguments[1]);
    }

    #endregion

    #region GenerateFluentApiCalls_SegmentBy_Suppresses_Standalone_EnableCompression

    private class SegmentByNoEnableCompressionEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class SegmentByNoEnableCompressionContext : DbContext
    {
        public DbSet<SegmentByNoEnableCompressionEntity> Items => Set<SegmentByNoEnableCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SegmentByNoEnableCompressionEntity>(e => { e.HasNoKey(); e.ToTable("fluent_segby_nosep"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_SegmentBy_Suppresses_Standalone_EnableCompression()
    {
        using SegmentByNoEnableCompressionContext context = new();
        IEntityType entityType = GetEntityType<SegmentByNoEnableCompressionEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSegmentBy, "DeviceId"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.EnableCompression), chain);
        Assert.Contains(nameof(HypertableTypeBuilder.WithCompressionSegmentBy), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_EmitsStandalone_EnableCompression_When_No_SegmentBy_Or_OrderBy

    private class StandaloneEnableCompressionEntity { public DateTime Ts { get; set; } }

    private class StandaloneEnableCompressionContext : DbContext
    {
        public DbSet<StandaloneEnableCompressionEntity> Items => Set<StandaloneEnableCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StandaloneEnableCompressionEntity>(e => { e.HasNoKey(); e.ToTable("fluent_standalone_ec"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_EmitsStandalone_EnableCompression_When_No_SegmentBy_Or_OrderBy()
    {
        using StandaloneEnableCompressionContext context = new();
        IEntityType entityType = GetEntityType<StandaloneEnableCompressionEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.EnableCompression), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithChunkSkipping

    private class ChunkSkippingFluentEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class ChunkSkippingFluentContext : DbContext
    {
        public DbSet<ChunkSkippingFluentEntity> Items => Set<ChunkSkippingFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ChunkSkippingFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_chunk_skip"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithChunkSkipping()
    {
        using ChunkSkippingFluentContext context = new();
        IEntityType entityType = GetEntityType<ChunkSkippingFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkSkipColumns, "DeviceId"));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithChunkSkipping), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithMigrateData

    private class MigrateDataFluentEntity { public DateTime Ts { get; set; } }

    private class MigrateDataFluentContext : DbContext
    {
        public DbSet<MigrateDataFluentEntity> Items => Set<MigrateDataFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MigrateDataFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_migrate"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithMigrateData()
    {
        using MigrateDataFluentContext context = new();
        IEntityType entityType = GetEntityType<MigrateDataFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.MigrateData, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.WithMigrateData), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_HasRangeDimension

    private class RangeDimFluentEntity { public DateTime Ts { get; set; } public string? Region { get; set; } }

    private class RangeDimFluentContext : DbContext
    {
        public DbSet<RangeDimFluentEntity> Items => Set<RangeDimFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RangeDimFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_range_dim"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_HasRangeDimension()
    {
        using RangeDimFluentContext context = new();
        IEntityType entityType = GetEntityType<RangeDimFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(Dimension.CreateRange("Region", "1 month"))));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.HasRangeDimension), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_HasHashDimension

    private class HashDimFluentEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class HashDimFluentContext : DbContext
    {
        public DbSet<HashDimFluentEntity> Items => Set<HashDimFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HashDimFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_hash_dim"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_HasHashDimension()
    {
        using HashDimFluentContext context = new();
        IEntityType entityType = GetEntityType<HashDimFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(Dimension.CreateHash("DeviceId", 4))));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.Contains(nameof(HypertableTypeBuilder.HasHashDimension), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Consumes_All_Annotations

    private class ConsumeAllFluentEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class ConsumeAllFluentContext : DbContext
    {
        public DbSet<ConsumeAllFluentEntity> Items => Set<ConsumeAllFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConsumeAllFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_consume_all"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Consumes_All_Annotations()
    {
        using ConsumeAllFluentContext context = new();
        IEntityType entityType = GetEntityType<ConsumeAllFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "1 day"),
            (HypertableAnnotations.EnableCompression, true),
            (HypertableAnnotations.CompressionSegmentBy, "DeviceId"),
            (HypertableAnnotations.CompressionOrderBy, "Ts DESC"),
            (HypertableAnnotations.ChunkSkipColumns, "DeviceId"),
            (HypertableAnnotations.MigrateData, true),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(Dimension.CreateHash("DeviceId", 4))));

        CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        Assert.DoesNotContain(HypertableAnnotations.IsHypertable, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.HypertableTimeColumn, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.ChunkTimeInterval, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.EnableCompression, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.CompressionSegmentBy, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.CompressionOrderBy, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.ChunkSkipColumns, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.MigrateData, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.AdditionalDimensions, annotations.Keys);
    }

    #endregion

    // ── GenerateDataAnnotationAttributes ───────────────────────────────────

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Not_Set

    private class DataAnnotNotHypertableEntity { public DateTime Ts { get; set; } }

    private class DataAnnotNotHypertableContext : DbContext
    {
        public DbSet<DataAnnotNotHypertableEntity> Items => Set<DataAnnotNotHypertableEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotNotHypertableEntity>(e => { e.HasNoKey(); e.ToTable("da_not_ht"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Not_Set()
    {
        using DataAnnotNotHypertableContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotNotHypertableEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations();

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        Assert.DoesNotContain(result, a => a.Type == typeof(HypertableAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Minimal_Returns_HypertableAttribute

    private class DataAnnotMinimalEntity { public DateTime Ts { get; set; } }

    private class DataAnnotMinimalContext : DbContext
    {
        public DbSet<DataAnnotMinimalEntity> Items => Set<DataAnnotMinimalEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotMinimalEntity>(e => { e.HasNoKey(); e.ToTable("da_minimal"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Minimal_Returns_HypertableAttribute()
    {
        using DataAnnotMinimalContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotMinimalEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        Assert.Contains(result, a => a.Type == typeof(HypertableAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Includes_EnableCompression_In_Named_Args

    private class DataAnnotEnableCompressionEntity { public DateTime Ts { get; set; } }

    private class DataAnnotEnableCompressionContext : DbContext
    {
        public DbSet<DataAnnotEnableCompressionEntity> Items => Set<DataAnnotEnableCompressionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotEnableCompressionEntity>(e => { e.HasNoKey(); e.ToTable("da_enable_comp"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Includes_EnableCompression_In_Named_Args()
    {
        using DataAnnotEnableCompressionContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotEnableCompressionEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.EnableCompression)));
        Assert.Equal(true, hypertableAttr.NamedArguments[nameof(HypertableAttribute.EnableCompression)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Includes_ChunkTimeInterval_When_NonDefault

    private class DataAnnotChunkIntervalNonDefaultEntity { public DateTime Ts { get; set; } }

    private class DataAnnotChunkIntervalNonDefaultContext : DbContext
    {
        public DbSet<DataAnnotChunkIntervalNonDefaultEntity> Items => Set<DataAnnotChunkIntervalNonDefaultEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotChunkIntervalNonDefaultEntity>(e => { e.HasNoKey(); e.ToTable("da_cti_nondefault"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Includes_ChunkTimeInterval_When_NonDefault()
    {
        using DataAnnotChunkIntervalNonDefaultContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotChunkIntervalNonDefaultEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "1 hour"));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.ChunkTimeInterval)));
        Assert.Equal("1 hour", hypertableAttr.NamedArguments[nameof(HypertableAttribute.ChunkTimeInterval)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Omits_ChunkTimeInterval_When_Equal_To_Default

    private class DataAnnotChunkIntervalDefaultEntity { public DateTime Ts { get; set; } }

    private class DataAnnotChunkIntervalDefaultContext : DbContext
    {
        public DbSet<DataAnnotChunkIntervalDefaultEntity> Items => Set<DataAnnotChunkIntervalDefaultEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotChunkIntervalDefaultEntity>(e => { e.HasNoKey(); e.ToTable("da_cti_default"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_ChunkTimeInterval_When_Equal_To_Default()
    {
        using DataAnnotChunkIntervalDefaultContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotChunkIntervalDefaultEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, DefaultValues.ChunkTimeInterval));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.False(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.ChunkTimeInterval)));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Includes_MigrateData_In_Named_Args

    private class DataAnnotMigrateDataEntity { public DateTime Ts { get; set; } }

    private class DataAnnotMigrateDataContext : DbContext
    {
        public DbSet<DataAnnotMigrateDataEntity> Items => Set<DataAnnotMigrateDataEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotMigrateDataEntity>(e => { e.HasNoKey(); e.ToTable("da_migrate"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Includes_MigrateData_In_Named_Args()
    {
        using DataAnnotMigrateDataContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotMigrateDataEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.MigrateData, true));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.MigrateData)));
        Assert.Equal(true, hypertableAttr.NamedArguments[nameof(HypertableAttribute.MigrateData)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_EmitsDimension_Hash_Attribute

    private class DataAnnotHashDimEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class DataAnnotHashDimContext : DbContext
    {
        public DbSet<DataAnnotHashDimEntity> Items => Set<DataAnnotHashDimEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotHashDimEntity>(e => { e.HasNoKey(); e.ToTable("da_hash_dim"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_EmitsDimension_Hash_Attribute()
    {
        using DataAnnotHashDimContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotHashDimEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(Dimension.CreateHash("DeviceId", 8))));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        Assert.Contains(result, a => a.Type == typeof(DimensionAttribute));
        AttributeCodeFragment dimAttr = result.First(a => a.Type == typeof(DimensionAttribute));
        Assert.Equal(EDimensionType.Hash, dimAttr.Arguments[1]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_EmitsDimension_Range_Attribute

    private class DataAnnotRangeDimEntity { public DateTime Ts { get; set; } public string? Region { get; set; } }

    private class DataAnnotRangeDimContext : DbContext
    {
        public DbSet<DataAnnotRangeDimEntity> Items => Set<DataAnnotRangeDimEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotRangeDimEntity>(e => { e.HasNoKey(); e.ToTable("da_range_dim"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_EmitsDimension_Range_Attribute()
    {
        using DataAnnotRangeDimContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotRangeDimEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(Dimension.CreateRange("Region", "1 month"))));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        Assert.Contains(result, a => a.Type == typeof(DimensionAttribute));
        AttributeCodeFragment dimAttr = result.First(a => a.Type == typeof(DimensionAttribute));
        Assert.Equal(EDimensionType.Range, dimAttr.Arguments[1]);
        Assert.Equal("1 month", dimAttr.Arguments[2]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Consumes_All_Annotations

    private class DataAnnotConsumeAllEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class DataAnnotConsumeAllContext : DbContext
    {
        public DbSet<DataAnnotConsumeAllEntity> Items => Set<DataAnnotConsumeAllEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DataAnnotConsumeAllEntity>(e => { e.HasNoKey(); e.ToTable("da_consume_all"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Consumes_All_Annotations()
    {
        using DataAnnotConsumeAllContext context = new();
        IEntityType entityType = GetEntityType<DataAnnotConsumeAllEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "1 hour"),
            (HypertableAnnotations.EnableCompression, true),
            (HypertableAnnotations.CompressionSegmentBy, "DeviceId"),
            (HypertableAnnotations.CompressionOrderBy, "Ts DESC"),
            (HypertableAnnotations.ChunkSkipColumns, "DeviceId"),
            (HypertableAnnotations.MigrateData, true),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(Dimension.CreateHash("DeviceId", 4))));

        CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        Assert.DoesNotContain(HypertableAnnotations.IsHypertable, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.HypertableTimeColumn, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.ChunkTimeInterval, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.EnableCompression, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.CompressionSegmentBy, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.CompressionOrderBy, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.ChunkSkipColumns, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.MigrateData, annotations.Keys);
        Assert.DoesNotContain(HypertableAnnotations.AdditionalDimensions, annotations.Keys);
    }

    #endregion

    #region GenerateFluentApiCalls_MalformedDimensions_Json_Does_Not_Throw

    private class MalformedDimEntity { public DateTime Ts { get; set; } }

    private class MalformedDimContext : DbContext
    {
        public DbSet<MalformedDimEntity> Items => Set<MalformedDimEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MalformedDimEntity>(e => { e.HasNoKey(); e.ToTable("fluent_malformed_dim"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_MalformedDimensions_Json_Does_Not_Throw()
    {
        using MalformedDimContext context = new();
        IEntityType entityType = GetEntityType<MalformedDimEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, "not-valid-json"));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.FirstOrDefault(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.NotNull(hypertableCall);
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.HasRangeDimension), chain);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.HasHashDimension), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_WithCompressionOrderBy_NullsLast

    private class OrderByNullsLastFluentEntity { public DateTime EventTime { get; set; } public DateTime Ts { get; set; } }

    private class OrderByNullsLastFluentContext : DbContext
    {
        public DbSet<OrderByNullsLastFluentEntity> Items => Set<OrderByNullsLastFluentEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByNullsLastFluentEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ob_nullslast"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithCompressionOrderBy_NullsLast()
    {
        using OrderByNullsLastFluentContext context = new();
        IEntityType entityType = GetEntityType<OrderByNullsLastFluentEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "EventTime DESC NULLS LAST"),
            (HypertableAnnotations.EnableCompression, true));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        MethodCallCodeFragment? current = hypertableCall;
        MethodCallCodeFragment? orderByCall = null;
        while (current != null)
        {
            if (current.Method == nameof(HypertableTypeBuilder.WithCompressionOrderBy)) { orderByCall = current; break; }
            current = current.ChainedCall;
        }
        Assert.NotNull(orderByCall);
        NestedClosureCodeFragment closure = Assert.IsType<NestedClosureCodeFragment>(orderByCall.Arguments[0]);
        Assert.Equal(nameof(OrderBySelector<>.ByDescending), closure.MethodCalls[0].Method);
        Assert.Equal(2, closure.MethodCalls[0].Arguments.Count);
        Assert.Equal(false, closure.MethodCalls[0].Arguments[1]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionSegmentBy_NamedArgument_Contains_NameOfCodeFragment

    private class SegmentByNofEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class SegmentByNofContext : DbContext
    {
        public DbSet<SegmentByNofEntity> Items => Set<SegmentByNofEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SegmentByNofEntity>(e => { e.HasNoKey(); e.ToTable("da_segby_nof"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionSegmentBy_NamedArgument_Contains_NameOfCodeFragment()
    {
        using SegmentByNofContext context = new();
        IEntityType entityType = GetEntityType<SegmentByNofEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSegmentBy, "DeviceId"));

        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.CompressionSegmentBy)));
        object?[] segmentByArray = Assert.IsType<object[]>(hypertableAttr.NamedArguments[nameof(HypertableAttribute.CompressionSegmentBy)]);
        Assert.All(segmentByArray, entry => Assert.IsType<NameOfCodeFragment>(entry));
    }

    #endregion

    #region GenerateFluentApiCalls_WhitespaceOnly_TimeColumn_Returns_Empty

    private class WhitespaceTimeColumnEntity { public DateTime Ts { get; set; } }

    private class WhitespaceTimeColumnContext : DbContext
    {
        public DbSet<WhitespaceTimeColumnEntity> Items => Set<WhitespaceTimeColumnEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<WhitespaceTimeColumnEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ws_tc"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_WhitespaceOnly_TimeColumn_Returns_Empty()
    {
        using WhitespaceTimeColumnContext context = new();
        IEntityType entityType = GetEntityType<WhitespaceTimeColumnEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "   "));

        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Consumes_All_Hypertable_Annotations_In_DaMode

    private class ConsumeDaEntity { public DateTime Ts { get; set; } public string Device { get; set; } = ""; }

    private class ConsumeDaContext : DbContext
    {
        public DbSet<ConsumeDaEntity> Items => Set<ConsumeDaEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConsumeDaEntity>(e => { e.HasNoKey(); e.ToTable("consume_da_ht"); });
    }

    [Fact]
    public void ConsumeFeatureAnnotations_Consumes_All_Hypertable_Annotations_In_DaMode()
    {
        using ConsumeDaContext context = new();
        IEntityType entityType = GetEntityType<ConsumeDaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "1 day"),
            (HypertableAnnotations.EnableCompression, true),
            (HypertableAnnotations.CompressionSegmentBy, "Device"),
            (HypertableAnnotations.CompressionOrderBy, "Ts DESC"),
            (HypertableAnnotations.ChunkSkipColumns, "Ts"),
            (HypertableAnnotations.MigrateData, true),
            (HypertableAnnotations.AdditionalDimensions, DimensionsJson(new Dimension
            {
                ColumnName = "Device",
                Type = EDimensionType.Hash,
                NumberOfPartitions = 4,
            })));

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:", StringComparison.Ordinal));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionOrderBy_Unmapped_Column_With_Suffix_Returns_RawString

    private class UnmappedOrderByEntity { public DateTime Ts { get; set; } }

    private class UnmappedOrderByContext : DbContext
    {
        public DbSet<UnmappedOrderByEntity> Items => Set<UnmappedOrderByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnmappedOrderByEntity>(e => { e.HasNoKey(); e.ToTable("da_unmapped_orderby"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionOrderBy_Unmapped_Column_With_Suffix_Returns_RawString()
    {
        // Arrange
        using UnmappedOrderByContext context = new();
        IEntityType entityType = GetEntityType<UnmappedOrderByEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "unmapped_col DESC"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.CompressionOrderBy)));
        string[] orderByArray = Assert.IsType<string[]>(hypertableAttr.NamedArguments[nameof(HypertableAttribute.CompressionOrderBy)]);
        Assert.Equal("unmapped_col DESC", Assert.Single(orderByArray));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionOrderBy_Mapped_Column_With_Suffix_Returns_NameOfCodeFragment

    private class MappedOrderByEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class MappedOrderByContext : DbContext
    {
        public DbSet<MappedOrderByEntity> Items => Set<MappedOrderByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MappedOrderByEntity>(e => { e.HasNoKey(); e.ToTable("da_mapped_orderby"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionOrderBy_Mapped_Column_With_Suffix_Returns_NameOfCodeFragment()
    {
        // Arrange
        using MappedOrderByContext context = new();
        IEntityType entityType = GetEntityType<MappedOrderByEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "DeviceId DESC"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.CompressionOrderBy)));
        object[] orderByArray = Assert.IsType<object[]>(hypertableAttr.NamedArguments[nameof(HypertableAttribute.CompressionOrderBy)]);
        Assert.IsType<NameOfCodeFragment>(Assert.Single(orderByArray));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_TimeColumn_IsWhitespace

    private class DaWhitespaceTimeEntity { public DateTime Ts { get; set; } }

    private class DaWhitespaceTimeContext : DbContext
    {
        public DbSet<DaWhitespaceTimeEntity> Items => Set<DaWhitespaceTimeEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaWhitespaceTimeEntity>(e => { e.HasNoKey(); e.ToTable("da_ws_time"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_TimeColumn_IsWhitespace()
    {
        // Arrange
        using DaWhitespaceTimeContext context = new();
        IEntityType entityType = GetEntityType<DaWhitespaceTimeEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "   "));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(HypertableAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_InvalidJsonDimensions_ReturnsEmptyDimensions

    private class DaInvalidDimensionsEntity { public DateTime Ts { get; set; } }

    private class DaInvalidDimensionsContext : DbContext
    {
        public DbSet<DaInvalidDimensionsEntity> Items => Set<DaInvalidDimensionsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaInvalidDimensionsEntity>(e => { e.HasNoKey(); e.ToTable("da_invalid_dims"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_InvalidJsonDimensions_ReturnsEmptyDimensions()
    {
        // Arrange
        using DaInvalidDimensionsContext context = new();
        IEntityType entityType = GetEntityType<DaInvalidDimensionsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, "not valid json!!"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.Contains(result, a => a.Type == typeof(HypertableAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(DimensionAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Omits_ChunkTimeInterval_When_Whitespace

    private class DaChunkIntervalWhitespaceEntity { public DateTime Ts { get; set; } }

    private class DaChunkIntervalWhitespaceContext : DbContext
    {
        public DbSet<DaChunkIntervalWhitespaceEntity> Items => Set<DaChunkIntervalWhitespaceEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaChunkIntervalWhitespaceEntity>(e => { e.HasNoKey(); e.ToTable("da_cti_ws"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_ChunkTimeInterval_When_Whitespace()
    {
        // Arrange
        using DaChunkIntervalWhitespaceContext context = new();
        IEntityType entityType = GetEntityType<DaChunkIntervalWhitespaceEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "   "));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.False(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.ChunkTimeInterval)));
    }

    #endregion

    #region GenerateFluentApiCalls_Skips_ChunkTimeInterval_When_Whitespace

    private class FluentWhitespaceIntervalEntity { public DateTime Ts { get; set; } }

    private class FluentWhitespaceIntervalContext : DbContext
    {
        public DbSet<FluentWhitespaceIntervalEntity> Items => Set<FluentWhitespaceIntervalEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<FluentWhitespaceIntervalEntity>(e => { e.HasNoKey(); e.ToTable("fluent_ws_interval"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Skips_ChunkTimeInterval_When_Whitespace()
    {
        // Arrange
        using FluentWhitespaceIntervalContext context = new();
        IEntityType entityType = GetEntityType<FluentWhitespaceIntervalEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.ChunkTimeInterval, "   "));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.WithChunkTimeInterval), CollectMethodChain(hypertableCall));
    }

    #endregion

    #region GenerateFluentApiCalls_Skips_MigrateData_When_Annotation_Value_Is_False

    private class FluentMigrateDataFalseEntity { public DateTime Ts { get; set; } }

    private class FluentMigrateDataFalseContext : DbContext
    {
        public DbSet<FluentMigrateDataFalseEntity> Items => Set<FluentMigrateDataFalseEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<FluentMigrateDataFalseEntity>(e => { e.HasNoKey(); e.ToTable("fluent_migrate_false"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Skips_MigrateData_When_Annotation_Value_Is_False()
    {
        // Arrange
        using FluentMigrateDataFalseContext context = new();
        IEntityType entityType = GetEntityType<FluentMigrateDataFalseEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.MigrateData, false));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.WithMigrateData), CollectMethodChain(hypertableCall));
    }

    #endregion

    #region GenerateFluentApiCalls_Skips_Dimensions_When_AdditionalDimensions_Is_Empty_Array

    private class FluentEmptyDimensionsEntity { public DateTime Ts { get; set; } }

    private class FluentEmptyDimensionsContext : DbContext
    {
        public DbSet<FluentEmptyDimensionsEntity> Items => Set<FluentEmptyDimensionsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<FluentEmptyDimensionsEntity>(e => { e.HasNoKey(); e.ToTable("fluent_empty_dims"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Skips_Dimensions_When_AdditionalDimensions_Is_Empty_Array()
    {
        // Arrange
        using FluentEmptyDimensionsContext context = new();
        IEntityType entityType = GetEntityType<FluentEmptyDimensionsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, "[]"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        List<string> chain = CollectMethodChain(hypertableCall);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.HasHashDimension), chain);
        Assert.DoesNotContain(nameof(HypertableTypeBuilder.HasRangeDimension), chain);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_HashDimension_With_Null_NumberOfPartitions

    private class FluentHashDimNullPartitionsEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class FluentHashDimNullPartitionsContext : DbContext
    {
        public DbSet<FluentHashDimNullPartitionsEntity> Items => Set<FluentHashDimNullPartitionsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<FluentHashDimNullPartitionsEntity>(e => { e.HasNoKey(); e.ToTable("fluent_hash_null_parts"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_HashDimension_With_Null_NumberOfPartitions()
    {
        // Arrange
        using FluentHashDimNullPartitionsContext context = new();
        IEntityType entityType = GetEntityType<FluentHashDimNullPartitionsEntity>(context);
        Dimension hashDimWithNullPartitions = new() { ColumnName = "DeviceId", Type = EDimensionType.Hash, NumberOfPartitions = null };
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, JsonSerializer.Serialize(new List<Dimension> { hashDimWithNullPartitions })));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.Contains(nameof(HypertableTypeBuilder.HasHashDimension), CollectMethodChain(hypertableCall));
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_RangeDimension_With_Null_Interval

    private class FluentRangeDimNullIntervalEntity { public DateTime Ts { get; set; } public string Region { get; set; } = ""; }

    private class FluentRangeDimNullIntervalContext : DbContext
    {
        public DbSet<FluentRangeDimNullIntervalEntity> Items => Set<FluentRangeDimNullIntervalEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<FluentRangeDimNullIntervalEntity>(e => { e.HasNoKey(); e.ToTable("fluent_range_null_interval"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Chains_RangeDimension_With_Null_Interval()
    {
        // Arrange
        using FluentRangeDimNullIntervalContext context = new();
        IEntityType entityType = GetEntityType<FluentRangeDimNullIntervalEntity>(context);
        Dimension rangeDimNullInterval = new() { ColumnName = "Region", Type = EDimensionType.Range, Interval = null };
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, JsonSerializer.Serialize(new List<Dimension> { rangeDimNullInterval })));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? hypertableCall = result.First(f => CollectMethodChain(f).Contains(nameof(HypertableTypeBuilder.IsHypertable)));
        Assert.Contains(nameof(HypertableTypeBuilder.HasRangeDimension), CollectMethodChain(hypertableCall));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Skips_MigrateData_When_Annotation_Value_Is_False

    private class DaMigrateDataFalseEntity { public DateTime Ts { get; set; } }

    private class DaMigrateDataFalseContext : DbContext
    {
        public DbSet<DaMigrateDataFalseEntity> Items => Set<DaMigrateDataFalseEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaMigrateDataFalseEntity>(e => { e.HasNoKey(); e.ToTable("da_migrate_false"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Skips_MigrateData_When_Annotation_Value_Is_False()
    {
        // Arrange
        using DaMigrateDataFalseContext context = new();
        IEntityType entityType = GetEntityType<DaMigrateDataFalseEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.MigrateData, false));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.False(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.MigrateData)));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionOrderBy_Unmapped_Column_No_Space

    private class DaOrderByNoSpaceEntity { public DateTime Ts { get; set; } }

    private class DaOrderByNoSpaceContext : DbContext
    {
        public DbSet<DaOrderByNoSpaceEntity> Items => Set<DaOrderByNoSpaceEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaOrderByNoSpaceEntity>(e => { e.HasNoKey(); e.ToTable("da_ob_no_space"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionOrderBy_Unmapped_Column_No_Space()
    {
        // Arrange
        using DaOrderByNoSpaceContext context = new();
        IEntityType entityType = GetEntityType<DaOrderByNoSpaceEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "unmapped_col"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment hypertableAttr = Assert.Single(result, a => a.Type == typeof(HypertableAttribute));
        Assert.True(hypertableAttr.NamedArguments.ContainsKey(nameof(HypertableAttribute.CompressionOrderBy)));
        string[] orderByArray = Assert.IsType<string[]>(hypertableAttr.NamedArguments[nameof(HypertableAttribute.CompressionOrderBy)]);
        Assert.Equal("unmapped_col", Assert.Single(orderByArray));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_SparseIndex_EmptyStringEntry_IsSkipped

    private class DaSparseIndexEmptyEntryEntity { public DateTime Ts { get; set; } public int DeviceId { get; set; } }

    private class DaSparseIndexEmptyEntryContext : DbContext
    {
        public DbSet<DaSparseIndexEmptyEntryEntity> Items => Set<DaSparseIndexEmptyEntryEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaSparseIndexEmptyEntryEntity>(e => { e.HasNoKey(); e.ToTable("da_sparse_empty_entry"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_SparseIndex_EmptyStringEntry_IsSkipped()
    {
        // Arrange
        using DaSparseIndexEmptyEntryContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexEmptyEntryEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(DeviceId),  ,minmax(Ts)"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        IEnumerable<AttributeCodeFragment> sparseIndexAttrs = result.Where(a => a.Type == typeof(SparseIndexAttribute));
        Assert.Equal(2, sparseIndexAttrs.Count());
    }

    #endregion

    #region GenerateDataAnnotationAttributes_SparseIndex_Columns_Render_As_NameOf

    private class DaSparseIndexNameOfEntity
    {
        public DateTime Ts { get; set; }
        public string Site { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class DaSparseIndexNameOfContext : DbContext
    {
        public DbSet<DaSparseIndexNameOfEntity> Items => Set<DaSparseIndexNameOfEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaSparseIndexNameOfEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("da_sparse_nameof");
                e.Property(x => x.Site).HasColumnName("site");
                e.Property(x => x.Value).HasColumnName("value");
            });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_SparseIndex_Columns_Render_As_NameOf()
    {
        // Arrange
        using DaSparseIndexNameOfContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexNameOfEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(site), minmax(value)"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        List<AttributeCodeFragment> sparseIndexAttrs = [.. result.Where(a => a.Type == typeof(SparseIndexAttribute))];
        Assert.Equal(2, sparseIndexAttrs.Count);
        NameOfCodeFragment bloomColumn = Assert.IsType<NameOfCodeFragment>(sparseIndexAttrs[0].Arguments[1]);
        Assert.Equal(nameof(DaSparseIndexNameOfEntity.Site), bloomColumn.PropertyName);
        NameOfCodeFragment minMaxColumn = Assert.IsType<NameOfCodeFragment>(sparseIndexAttrs[1].Arguments[1]);
        Assert.Equal(nameof(DaSparseIndexNameOfEntity.Value), minMaxColumn.PropertyName);
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_SparseIndex_Unmapped_Column_Falls_Back_To_String()
    {
        // Arrange
        using DaSparseIndexNameOfContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexNameOfEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(not_a_column)"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment sparseIndexAttr = Assert.Single(result, a => a.Type == typeof(SparseIndexAttribute));
        Assert.Equal("not_a_column", sparseIndexAttr.Arguments[1]);
    }

    #endregion

    #region GenerateFluentApiCalls_SparseIndex_Renders_As_Selector_Lambdas

    [Fact]
    public void GenerateFluentApiCalls_SparseIndex_Renders_As_Selector_Lambdas()
    {
        // Arrange
        using DaSparseIndexNameOfContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexNameOfEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "Ts DESC"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(site,value), minmax(value)"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? sparseCall = FindChainedCall(result, "WithSparseIndex");
        Assert.NotNull(sparseCall);
        Assert.Equal(2, sparseCall.Arguments.Count);
        SparseIndexSelectorCodeFragment bloom = Assert.IsType<SparseIndexSelectorCodeFragment>(sparseCall.Arguments[0]);
        Assert.Equal(ESparseIndexType.Bloom, bloom.Kind);
        Assert.Equal(["Site", "Value"], bloom.PropertyNames);
        SparseIndexSelectorCodeFragment minMax = Assert.IsType<SparseIndexSelectorCodeFragment>(sparseCall.Arguments[1]);
        Assert.Equal(ESparseIndexType.MinMax, minMax.Kind);
        Assert.Equal(["Value"], minMax.PropertyNames);
    }

    [Fact]
    public void GenerateFluentApiCalls_SparseIndex_Unmapped_Column_Falls_Back_To_Raw_String()
    {
        // Arrange
        using DaSparseIndexNameOfContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexNameOfEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionOrderBy, "Ts DESC"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom(site), minmax(not_a_column)"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? sparseCall = FindChainedCall(result, "WithSparseIndex");
        Assert.NotNull(sparseCall);
        object? argument = Assert.Single(sparseCall.Arguments);
        Assert.Equal("bloom(site), minmax(not_a_column)", argument);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_SparseIndex_MalformedEntry_NoParen_IsSkipped

    private class DaSparseIndexNoParenEntity { public DateTime Ts { get; set; } }

    private class DaSparseIndexNoParenContext : DbContext
    {
        public DbSet<DaSparseIndexNoParenEntity> Items => Set<DaSparseIndexNoParenEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaSparseIndexNoParenEntity>(e => { e.HasNoKey(); e.ToTable("da_sparse_no_paren"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_SparseIndex_MalformedEntry_NoParen_IsSkipped()
    {
        // Arrange
        using DaSparseIndexNoParenContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexNoParenEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "not_a_function_call"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(SparseIndexAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_SparseIndex_EmptyColumns_IsSkipped

    private class DaSparseIndexEmptyColsEntity { public DateTime Ts { get; set; } }

    private class DaSparseIndexEmptyColsContext : DbContext
    {
        public DbSet<DaSparseIndexEmptyColsEntity> Items => Set<DaSparseIndexEmptyColsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaSparseIndexEmptyColsEntity>(e => { e.HasNoKey(); e.ToTable("da_sparse_empty_cols"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_SparseIndex_EmptyColumns_IsSkipped()
    {
        // Arrange
        using DaSparseIndexEmptyColsContext context = new();
        IEntityType entityType = GetEntityType<DaSparseIndexEmptyColsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.CompressionSparseIndex, "bloom()"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(SparseIndexAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Dimension_Unmapped_Column_Returns_RawString_And_Range_With_Null_Interval

    private class DaDimUnmappedEntity { public DateTime Ts { get; set; } }

    private class DaDimUnmappedContext : DbContext
    {
        public DbSet<DaDimUnmappedEntity> Items => Set<DaDimUnmappedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DaDimUnmappedEntity>(e => { e.HasNoKey(); e.ToTable("da_dim_unmapped"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Dimension_Unmapped_Column_Returns_RawString_And_Range_With_Null_Interval()
    {
        // Arrange
        using DaDimUnmappedContext context = new();
        IEntityType entityType = GetEntityType<DaDimUnmappedEntity>(context);
        Dimension rangeDimNullInterval = new() { ColumnName = "unmapped_dim_col", Type = EDimensionType.Range, Interval = null };
        Dictionary<string, IAnnotation> annotations = Annotations(
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "Ts"),
            (HypertableAnnotations.AdditionalDimensions, JsonSerializer.Serialize(new List<Dimension> { rangeDimNullInterval })));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.Contains(result, a => a.Type == typeof(DimensionAttribute));
        AttributeCodeFragment dimAttr = result.First(a => a.Type == typeof(DimensionAttribute));
        Assert.Equal(EDimensionType.Range, dimAttr.Arguments[1]);
        Assert.Equal(string.Empty, dimAttr.Arguments[2]);
        Assert.IsType<string>(dimAttr.Arguments[0]);
        Assert.Equal("unmapped_dim_col", dimAttr.Arguments[0]);
    }

    #endregion
}

#pragma warning disable EF1001
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

public class TimescaleCSharpModelGeneratorTests
{
    private static IModelCodeGeneratorSelector CreateSelector()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        return services.BuildServiceProvider().GetRequiredService<IModelCodeGeneratorSelector>();
    }

    private static ModelCodeGenerationOptions DefaultOptions(bool useDataAnnotations = false) => new()
    {
        Language = "C#",
        UseDataAnnotations = useDataAnnotations,
        ProjectDir = ".",
        ModelNamespace = "TestModels",
        ContextName = "TestDbContext",
        ContextNamespace = "TestModels",
        ConnectionString = "Host=localhost;Database=test"
    };

    // ── Selector tests ─────────────────────────────────────────────────────

    #region Select_Returns_TimescaleCSharpModelGenerator_When_BaseReturns_CSharpModelGenerator

    [Fact]
    public void Select_Returns_TimescaleCSharpModelGenerator_When_BaseReturns_CSharpModelGenerator()
    {
        IModelCodeGeneratorSelector selector = CreateSelector();

        IModelCodeGenerator generator = selector.Select(DefaultOptions());

        Assert.IsType<TimescaleCSharpModelGenerator>(generator);
    }

    #endregion

    #region Select_Returns_CSharpModelGenerator_When_TimescaleCSharpModelGenerator_Not_In_Services

    [Fact]
    public void Select_Returns_CSharpModelGenerator_When_TimescaleCSharpModelGenerator_Not_In_Services()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        IModelCodeGeneratorSelector selector = services.BuildServiceProvider()
            .GetRequiredService<IModelCodeGeneratorSelector>();

        IModelCodeGenerator generator = selector.Select(DefaultOptions());

        Assert.NotNull(generator);
        Assert.IsType<CSharpModelGenerator>(generator);
    }

    #endregion

    // ── GenerateModel tests ────────────────────────────────────────────────

    #region GenerateModel_UseDataAnnotations_False_Does_Not_Inject_Timescale_Usings

    private class NoAnnotationsEntity { public DateTime Ts { get; set; } }

    [Hypertable(nameof(Ts))]
    private class HypertableAnnotatedEntityFalse { public DateTime Ts { get; set; } }

    private class GenerateModelFalseContext : DbContext
    {
        public DbSet<HypertableAnnotatedEntityFalse> Items => Set<HypertableAnnotatedEntityFalse>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HypertableAnnotatedEntityFalse>(e => { e.HasNoKey(); e.ToTable("gen_model_false"); });
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_False_Does_Not_Inject_Timescale_Usings()
    {
        using GenerateModelFalseContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: false);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);

        Assert.All(result.AdditionalFiles, file =>
            Assert.DoesNotContain("using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;", file.Code));
    }

    #endregion

    #region GenerateModel_UseDataAnnotations_True_Injects_Hypertable_Namespace

    [Hypertable(nameof(Ts))]
    private class HypertableAnnotatedEntityTrue { public DateTime Ts { get; set; } }

    private class GenerateModelTrueContext : DbContext
    {
        public DbSet<HypertableAnnotatedEntityTrue> Items => Set<HypertableAnnotatedEntityTrue>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HypertableAnnotatedEntityTrue>(e => { e.HasNoKey(); e.ToTable("gen_model_true"); });
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_True_Injects_Hypertable_Namespace()
    {
        using GenerateModelTrueContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);

        // The entity file should contain the Hypertable namespace using
        bool found = result.AdditionalFiles.Any(f =>
            f.Code.Contains("using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;"));
        Assert.True(found);
    }

    #endregion

    // ── DA mode: suppress hypertable fluent API in context file ───────────────

    #region GenerateModel_UseDataAnnotations_True_DoesNotEmitHypertableFluentApiCalls

    [Hypertable(nameof(Ts))]
    private class HypertableWithChunkEntity { public DateTime Ts { get; set; } }

    private class HtChunkContext : DbContext
    {
        public DbSet<HypertableWithChunkEntity> Items => Set<HypertableWithChunkEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HypertableWithChunkEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("ht_chunk_da_test");
                e.WithChunkTimeInterval("1 day");
            });
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_True_DoesNotEmitHypertableFluentApiCalls()
    {
        using HtChunkContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);
        Assert.DoesNotContain("WithChunkTimeInterval", result.ContextFile.Code);
    }

    #endregion

    // ── Property-level namespace injection for CA entities ────────────────────

    #region GenerateModel_UseDataAnnotations_True_Injects_Aggregate_Namespaces

    private class CaAggSourceEntity { public DateTime Time { get; set; } public double Price { get; set; } }

    private class CaAggViewEntity { public double MaxPrice { get; set; } }

    private class CaAggNamespaceContext : DbContext
    {
        public DbSet<CaAggSourceEntity> Sources => Set<CaAggSourceEntity>();
        public DbSet<CaAggViewEntity> Views => Set<CaAggViewEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaAggSourceEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_agg_ns_source");
                e.Property(x => x.Price).HasColumnName("price");
            });
            modelBuilder.Entity<CaAggViewEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_agg_ns_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "ca_agg_ns_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "ca_agg_ns_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "MaxPrice:Max:Price" });
                e.Property(x => x.MaxPrice).HasColumnName("max_price");
            });
        }
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_True_Injects_Aggregate_Namespaces()
    {
        using CaAggNamespaceContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);

        bool hasAbstractionsNs = result.AdditionalFiles.Any(f =>
            f.Code.Contains($"using {typeof(EAggregateFunction).Namespace};"));
        bool hasAggregateAttrNs = result.AdditionalFiles.Any(f =>
            f.Code.Contains($"using {typeof(AggregateAttribute).Namespace};"));

        Assert.True(hasAbstractionsNs, "Expected EAggregateFunction namespace in entity file");
        Assert.True(hasAggregateAttrNs, "Expected AggregateAttribute namespace in entity file");
    }

    #endregion
}
#pragma warning restore EF1001

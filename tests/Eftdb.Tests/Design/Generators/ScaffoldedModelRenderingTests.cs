#pragma warning disable EF1001
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

/// <summary>
/// Full-pipeline rendering tests: models carrying scaffolding annotations are run through the real
/// design-time service chain and assertions are made on the RENDERED C# output, not on code fragments.
/// These pin the chaining strategy of the annotation renderers (a chained root must render).
/// </summary>
public class ScaffoldedModelRenderingTests
{
    private static ServiceProvider CreateDesignServices()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        return services.BuildServiceProvider();
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

    private static ScaffoldedModel Generate(DbContext context, bool useDataAnnotations)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        using ServiceProvider sp = CreateDesignServices();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations);
        IModelCodeGenerator generator = sp.GetRequiredService<IModelCodeGeneratorSelector>().Select(options);
        return generator.GenerateModel(model, options);
    }

    #region Should_Render_IsHypertable_Root_And_Chained_Calls_In_Fluent_Scaffold

    private class RenderHtEntity { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RenderHtContext : DbContext
    {
        public DbSet<RenderHtEntity> Items => Set<RenderHtEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RenderHtEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_ht_entity");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(HypertableAnnotations.ChunkTimeInterval, "1 day");
            });
    }

    [Fact]
    public void Should_Render_IsHypertable_Root_And_Chained_Calls_In_Fluent_Scaffold()
    {
        using RenderHtContext context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".IsHypertable(x => x.Time)", code);
        Assert.Contains(".WithChunkTimeInterval(\"1 day\")", code);
        Assert.True(
            code.IndexOf(".IsHypertable(", StringComparison.Ordinal) <
            code.IndexOf(".WithChunkTimeInterval(", StringComparison.Ordinal),
            "IsHypertable must precede WithChunkTimeInterval in the rendered chain.");
        Assert.DoesNotContain("using CmdScale.EntityFrameworkCore.TimescaleDB.Design", code);
    }

    #endregion

    #region Should_Render_IsContinuousAggregate_Before_Builder_Calls_In_Fluent_Scaffold

    private class RenderCaSource { public DateTime Time { get; set; } public double Price { get; set; } public string Region { get; set; } = null!; }
    private class RenderCaView { public DateTime Bucket { get; set; } public double MaxPrice { get; set; } public string Region { get; set; } = null!; }

    private class RenderCaContext : DbContext
    {
        public DbSet<RenderCaSource> Sources => Set<RenderCaSource>();
        public DbSet<RenderCaView> Views => Set<RenderCaView>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RenderCaSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_ca_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Price).HasColumnName("price");
                e.Property(x => x.Region).HasColumnName("region");
            });
            modelBuilder.Entity<RenderCaView>(e =>
            {
                e.HasNoKey();
                e.ToView("render_ca_view");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.MaxPrice).HasColumnName("max_price");
                e.Property(x => x.Region).HasColumnName("region");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_ca_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_ca_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    " SELECT time_bucket('01:00:00'::interval, \"time\") AS bucket, region, max(price) AS max_price" +
                    " FROM render_ca_source WHERE (price > (0)::double precision)" +
                    " GROUP BY (time_bucket('01:00:00'::interval, \"time\")), region");
            });
        }
    }

    [Fact]
    public void Should_Render_IsContinuousAggregate_Before_Builder_Calls_In_Fluent_Scaffold()
    {
        using RenderCaContext context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".IsContinuousAggregate(", code);
        Assert.Contains(".AddAggregateFunction(", code);
        Assert.Contains(".AddGroupByColumn(", code);
        Assert.Contains(".Where(", code);

        int rootIndex = code.IndexOf(".IsContinuousAggregate(", StringComparison.Ordinal);
        Assert.True(rootIndex < code.IndexOf(".AddAggregateFunction(", StringComparison.Ordinal),
            "IsContinuousAggregate must precede AddAggregateFunction; the builder methods are only reachable from its return value.");
        Assert.True(rootIndex < code.IndexOf(".AddGroupByColumn(", StringComparison.Ordinal),
            "IsContinuousAggregate must precede AddGroupByColumn.");
    }

    #endregion

    #region Should_Render_Attributes_And_Usings_In_DataAnnotations_Scaffold

    private class RenderDaSource { public DateTime Time { get; set; } public double Price { get; set; } }
    private class RenderDaView { public DateTime Bucket { get; set; } public double MaxPrice { get; set; } }

    private class RenderDaContext : DbContext
    {
        public DbSet<RenderDaSource> Sources => Set<RenderDaSource>();
        public DbSet<RenderDaView> Views => Set<RenderDaView>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RenderDaSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_da_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Price).HasColumnName("price");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
            });
            modelBuilder.Entity<RenderDaView>(e =>
            {
                e.HasNoKey();
                e.ToView("render_da_view");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.MaxPrice).HasColumnName("max_price");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_da_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_da_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    " SELECT time_bucket('01:00:00'::interval, \"time\") AS bucket, max(price) AS max_price" +
                    " FROM render_da_source GROUP BY (time_bucket('01:00:00'::interval, \"time\"))");
            });
        }
    }

    [Fact]
    public void Should_Render_Attributes_And_Usings_In_DataAnnotations_Scaffold()
    {
        using RenderDaContext context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? htFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RenderDaSource)));
        ScaffoldedFile? caFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RenderDaView)));
        Assert.NotNull(htFile);
        Assert.NotNull(caFile);

        Assert.Contains("[Hypertable(", htFile.Code);
        Assert.Contains($"using {typeof(HypertableAttribute).Namespace};", htFile.Code);

        Assert.Contains("[ContinuousAggregate(", caFile.Code);
        Assert.Contains("[TimeBucket(", caFile.Code);
        Assert.Contains($"using {typeof(ContinuousAggregateAttribute).Namespace};", caFile.Code);
        Assert.DoesNotContain(".IsHypertable(", result.ContextFile.Code);
        Assert.DoesNotContain(".IsContinuousAggregate(", result.ContextFile.Code);
    }

    #endregion
}
#pragma warning restore EF1001

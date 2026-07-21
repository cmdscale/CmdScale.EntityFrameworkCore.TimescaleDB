#pragma warning disable EF1001
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
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

    // ── Continuous aggregate refresh policy rendering ─────────────────────────

    private const string PolicyStandardViewDef =
        "SELECT time_bucket('01:00:00'::interval, render_policy_source.\"time\") AS bucket," +
        " avg(render_policy_source.value) AS avg_value" +
        " FROM render_policy_source" +
        " GROUP BY time_bucket('01:00:00'::interval, render_policy_source.\"time\")";

    #region Should_Render_WithRefreshPolicy_Chained_After_IsContinuousAggregate_In_Fluent_Scaffold

    private class PolicySourceA { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewA { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextA : DbContext
    {
        public DbSet<PolicySourceA> Sources => Set<PolicySourceA>();
        public DbSet<PolicyCaViewA> Views => Set<PolicyCaViewA>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceA>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewA>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_a");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_a");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "7 days");
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRefreshPolicy_Chained_After_IsContinuousAggregate_In_Fluent_Scaffold()
    {
        using PolicyCaContextA context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".IsContinuousAggregate(", code);
        Assert.Contains(".WithRefreshPolicy(", code);
        Assert.True(
            code.IndexOf(".IsContinuousAggregate(", StringComparison.Ordinal) <
            code.IndexOf(".WithRefreshPolicy(", StringComparison.Ordinal),
            ".WithRefreshPolicy( must follow .IsContinuousAggregate( in the rendered output.");
    }

    #endregion

    #region Should_Render_WithRefreshPolicy_With_StartOffset_And_EndOffset_Args_In_Fluent_Scaffold

    private class PolicySourceB { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewB { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextB : DbContext
    {
        public DbSet<PolicySourceB> Sources => Set<PolicySourceB>();
        public DbSet<PolicyCaViewB> Views => Set<PolicyCaViewB>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceB>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewB>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_b");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_b");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "7 days");
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRefreshPolicy_With_StartOffset_And_EndOffset_Args_In_Fluent_Scaffold()
    {
        using PolicyCaContextB context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".WithRefreshPolicy(\"7 days\", \"1 hour\")", code);
    }

    #endregion

    #region Should_Render_WithRefreshPolicy_With_Three_Args_When_ScheduleInterval_Present

    private class PolicySourceC { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewC { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextC : DbContext
    {
        public DbSet<PolicySourceC> Sources => Set<PolicySourceC>();
        public DbSet<PolicyCaViewC> Views => Set<PolicyCaViewC>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceC>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewC>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_c");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_c");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.ScheduleInterval, "24 hours");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRefreshPolicy_With_Three_Args_When_ScheduleInterval_Present()
    {
        using PolicyCaContextC context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".WithRefreshPolicy(null, null, \"24 hours\")", code);
    }

    #endregion

    #region Should_Render_WithInitialStart_In_Fluent_Scaffold

    private class PolicySourceD { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewD { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextD : DbContext
    {
        public DbSet<PolicySourceD> Sources => Set<PolicySourceD>();
        public DbSet<PolicyCaViewD> Views => Set<PolicyCaViewD>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceD>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewD>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_d");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_d");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Render_WithInitialStart_In_Fluent_Scaffold()
    {
        using PolicyCaContextD context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".WithInitialStart(", code);
    }

    #endregion

    #region Should_Not_Render_WithRefreshPolicy_When_CA_Renderer_Did_Not_Consume_MaterializedViewName

    private class PolicySourceE { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewE { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextE : DbContext
    {
        public DbSet<PolicySourceE> Sources => Set<PolicySourceE>();
        public DbSet<PolicyCaViewE> Views => Set<PolicyCaViewE>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceE>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewE>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_e");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_e");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, "UNPARSEABLE SQL");
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Not_Render_WithRefreshPolicy_When_CA_Renderer_Did_Not_Consume_MaterializedViewName()
    {
        using PolicyCaContextE context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Policy renderer must not emit .WithRefreshPolicy() because the CA renderer failed.
        Assert.DoesNotContain(".WithRefreshPolicy(", code);
    }

    #endregion

    #region Should_Render_ContinuousAggregatePolicyAttribute_In_DataAnnotations_Scaffold

    private class PolicySourceF { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewF { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextF : DbContext
    {
        public DbSet<PolicySourceF> Sources => Set<PolicySourceF>();
        public DbSet<PolicyCaViewF> Views => Set<PolicyCaViewF>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceF>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewF>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_f");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_f");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "7 days");
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
            });
        }
    }

    [Fact]
    public void Should_Render_ContinuousAggregatePolicyAttribute_In_DataAnnotations_Scaffold()
    {
        using PolicyCaContextF context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? caFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(PolicyCaViewF)));
        Assert.NotNull(caFile);
        Assert.Contains("[ContinuousAggregatePolicy(", caFile.Code);
        Assert.DoesNotContain(".WithRefreshPolicy(", result.ContextFile.Code);
    }

    #endregion

    #region Should_Render_ContinuousAggregatePolicyAttribute_With_InitialStart_As_ISO8601_String

    private class PolicySourceG { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewG { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextG : DbContext
    {
        public DbSet<PolicySourceG> Sources => Set<PolicySourceG>();
        public DbSet<PolicyCaViewG> Views => Set<PolicyCaViewG>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceG>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewG>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_g");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_g");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Render_ContinuousAggregatePolicyAttribute_With_InitialStart_As_ISO8601_String()
    {
        using PolicyCaContextG context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? caFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(PolicyCaViewG)));
        Assert.NotNull(caFile);
        Assert.Contains("InitialStart = \"2025-06-01T00:00:00", caFile.Code);
    }

    #endregion

    #region Should_Render_Full_Policy_Chain_In_Fluent_Scaffold

    private class PolicySourceH { public DateTime Time { get; set; } public double Value { get; set; } }
    private class PolicyCaViewH { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class PolicyCaContextH : DbContext
    {
        public DbSet<PolicySourceH> Sources => Set<PolicySourceH>();
        public DbSet<PolicyCaViewH> Views => Set<PolicyCaViewH>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceH>(e =>
            {
                e.HasNoKey();
                e.ToTable("render_policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<PolicyCaViewH>(e =>
            {
                e.HasNoKey();
                e.ToView("render_policy_ca_view_h");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "render_policy_ca_view_h");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "render_policy_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, PolicyStandardViewDef);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.StartOffset, "7 days");
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour");
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.IncludeTieredData, false);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 3);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, 10);
                e.HasAnnotation(ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, false);
            });
        }
    }

    [Fact]
    public void Should_Render_Full_Policy_Chain_In_Fluent_Scaffold()
    {
        using PolicyCaContextH context = new();

        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        Assert.Contains(".WithRefreshPolicy(", code);
        Assert.Contains(".WithInitialStart(", code);
        Assert.Contains(".WithIncludeTieredData(", code);
        Assert.Contains(".WithBucketsPerBatch(", code);
        Assert.Contains(".WithMaxBatchesPerExecution(", code);
        Assert.Contains(".WithRefreshNewestFirst(", code);

        int withRefreshPolicyIdx = code.IndexOf(".WithRefreshPolicy(", StringComparison.Ordinal);
        Assert.True(withRefreshPolicyIdx < code.IndexOf(".WithInitialStart(", StringComparison.Ordinal),
            ".WithInitialStart( must follow .WithRefreshPolicy(.");
        Assert.True(withRefreshPolicyIdx < code.IndexOf(".WithIncludeTieredData(", StringComparison.Ordinal),
            ".WithIncludeTieredData( must follow .WithRefreshPolicy(.");
        Assert.True(withRefreshPolicyIdx < code.IndexOf(".WithBucketsPerBatch(", StringComparison.Ordinal),
            ".WithBucketsPerBatch( must follow .WithRefreshPolicy(.");
        Assert.True(withRefreshPolicyIdx < code.IndexOf(".WithMaxBatchesPerExecution(", StringComparison.Ordinal),
            ".WithMaxBatchesPerExecution( must follow .WithRefreshPolicy(.");
        Assert.True(withRefreshPolicyIdx < code.IndexOf(".WithRefreshNewestFirst(", StringComparison.Ordinal),
            ".WithRefreshNewestFirst( must follow .WithRefreshPolicy(.");
    }

    #endregion
}
#pragma warning restore EF1001

#pragma warning disable EF1001
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
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

    // ── Retention policy rendering ────────────────────────────────────────────

    #region Should_Render_WithRetentionPolicy_Chained_In_Fluent_Scaffold_For_Hypertable

    private class RpHtSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpHtContextA : DbContext
    {
        public DbSet<RpHtSource> Items => Set<RpHtSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpHtSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_ht_source_a");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRetentionPolicy_Chained_In_Fluent_Scaffold_For_Hypertable()
    {
        // Arrange & Act
        using RpHtContextA context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".IsHypertable(", code);
        Assert.Contains(".WithRetentionPolicy(", code);
    }

    #endregion

    #region Should_Render_WithRetentionPolicy_With_DropAfter_Only_In_Fluent_Scaffold

    private class RpSingleArgSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpSingleArgContext : DbContext
    {
        public DbSet<RpSingleArgSource> Items => Set<RpSingleArgSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpSingleArgSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_single_arg_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRetentionPolicy_With_DropAfter_Only_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpSingleArgContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithRetentionPolicy(\"7 days\", null, null, null, null, null)", code);
    }

    #endregion

    #region Should_Render_WithRetentionPolicy_With_ScheduleInterval_In_Fluent_Scaffold

    private class RpThreeArgsSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpThreeArgsContext : DbContext
    {
        public DbSet<RpThreeArgsSource> Items => Set<RpThreeArgsSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpThreeArgsSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_three_args_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                e.HasAnnotation(RetentionPolicyAnnotations.ScheduleInterval, "1 day");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRetentionPolicy_With_ScheduleInterval_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpThreeArgsContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithRetentionPolicy(\"7 days\", null, \"1 day\", null, null, null)", code);
    }

    #endregion

    #region Should_Render_WithRetentionPolicy_Six_Args_In_Fluent_Scaffold

    private class RpSixArgsSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpSixArgsContext : DbContext
    {
        public DbSet<RpSixArgsSource> Items => Set<RpSixArgsSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpSixArgsSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_six_args_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                e.HasAnnotation(RetentionPolicyAnnotations.ScheduleInterval, "1 day");
                e.HasAnnotation(RetentionPolicyAnnotations.MaxRuntime, "01:00:00");
                e.HasAnnotation(RetentionPolicyAnnotations.MaxRetries, 3);
                e.HasAnnotation(RetentionPolicyAnnotations.RetryPeriod, "00:10:00");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRetentionPolicy_Six_Args_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpSixArgsContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithRetentionPolicy(\"7 days\", null, \"1 day\", \"01:00:00\", 3, \"00:10:00\")", code);
    }

    #endregion

    #region Should_Render_WithRetentionPolicy_With_DropCreatedBefore_In_Fluent_Scaffold

    private class RpDropCreatedBeforeSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpDropCreatedBeforeContext : DbContext
    {
        public DbSet<RpDropCreatedBeforeSource> Items => Set<RpDropCreatedBeforeSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpDropCreatedBeforeSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_drop_created_before_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropCreatedBefore, "30 days");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRetentionPolicy_With_DropCreatedBefore_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpDropCreatedBeforeContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithRetentionPolicy(null, \"30 days\", null, null, null, null)", code);
    }

    #endregion

    #region Should_Render_WithInitialStart_Chained_After_WithRetentionPolicy_In_Fluent_Scaffold

    private class RpInitialStartSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpInitialStartContext : DbContext
    {
        public DbSet<RpInitialStartSource> Items => Set<RpInitialStartSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpInitialStartSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_initial_start_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                e.HasAnnotation(RetentionPolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Render_WithInitialStart_Chained_After_WithRetentionPolicy_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpInitialStartContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithRetentionPolicy(", code);
        Assert.Contains(".WithInitialStart(", code);
        Assert.True(
            code.IndexOf(".WithRetentionPolicy(", StringComparison.Ordinal) <
            code.IndexOf(".WithInitialStart(", StringComparison.Ordinal),
            ".WithInitialStart( must follow .WithRetentionPolicy(.");
    }

    #endregion

    #region Should_Render_WithRetentionPolicy_For_ContinuousAggregate_In_Fluent_Scaffold

    private class RpCaSourceEntity { public DateTime Time { get; set; } public double Value { get; set; } }
    private class RpCaViewEntity { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class RpCaContext : DbContext
    {
        public DbSet<RpCaSourceEntity> Sources => Set<RpCaSourceEntity>();
        public DbSet<RpCaViewEntity> Views => Set<RpCaViewEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpCaSourceEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_ca_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<RpCaViewEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("rp_ca_view");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "rp_ca_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "rp_ca_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('01:00:00'::interval, rp_ca_source.\"time\") AS bucket," +
                    " avg(rp_ca_source.value) AS avg_value" +
                    " FROM rp_ca_source" +
                    " GROUP BY time_bucket('01:00:00'::interval, rp_ca_source.\"time\")");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "30 days");
            });
        }
    }

    [Fact]
    public void Should_Render_WithRetentionPolicy_For_ContinuousAggregate_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpCaContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".IsContinuousAggregate(", code);
        Assert.Contains(".WithRetentionPolicy(", code);
    }

    #endregion

    #region Should_Not_Render_WithRetentionPolicy_When_IsHypertable_Still_Present_In_Fluent_Scaffold

    private class RpGuardHtSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpGuardHtContext : DbContext
    {
        public DbSet<RpGuardHtSource> Items => Set<RpGuardHtSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpGuardHtSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_guard_ht_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Not_Render_WithRetentionPolicy_When_IsHypertable_Still_Present_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpGuardHtContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.DoesNotContain(".WithRetentionPolicy(", code);
    }

    #endregion

    #region Should_Not_Render_WithRetentionPolicy_When_MaterializedViewName_Still_Present_In_Fluent_Scaffold

    private class RpGuardCaSource { public DateTime Time { get; set; } public double Value { get; set; } }
    private class RpGuardCaView { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class RpGuardCaContext : DbContext
    {
        public DbSet<RpGuardCaSource> Sources => Set<RpGuardCaSource>();
        public DbSet<RpGuardCaView> Views => Set<RpGuardCaView>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpGuardCaSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_guard_ca_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<RpGuardCaView>(e =>
            {
                e.HasNoKey();
                e.ToView("rp_guard_ca_view");
                e.Property(x => x.Bucket).HasColumnName("bucket");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "rp_guard_ca_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "rp_guard_ca_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, "UNPARSEABLE SQL");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Not_Render_WithRetentionPolicy_When_MaterializedViewName_Still_Present_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RpGuardCaContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.DoesNotContain(".WithRetentionPolicy(", code);
    }

    #endregion

    #region Should_Render_RetentionPolicyAttribute_In_DataAnnotations_Scaffold

    private class RpDaSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpDaContext : DbContext
    {
        public DbSet<RpDaSource> Items => Set<RpDaSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpDaSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_da_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Render_RetentionPolicyAttribute_In_DataAnnotations_Scaffold()
    {
        // Arrange & Act
        using RpDaContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? entityFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RpDaSource)));
        Assert.NotNull(entityFile);

        // Assert
        Assert.Contains("[RetentionPolicy(", entityFile.Code);
        Assert.DoesNotContain(".WithRetentionPolicy(", result.ContextFile.Code);
    }

    #endregion

    #region Should_Render_RetentionPolicyAttribute_With_InitialStart_As_ISO8601_In_DataAnnotations_Scaffold

    private class RpDaInitialStartSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpDaInitialStartContext : DbContext
    {
        public DbSet<RpDaInitialStartSource> Items => Set<RpDaInitialStartSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpDaInitialStartSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_da_initial_start_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                e.HasAnnotation(RetentionPolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Render_RetentionPolicyAttribute_With_InitialStart_As_ISO8601_In_DataAnnotations_Scaffold()
    {
        // Arrange & Act
        using RpDaInitialStartContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? entityFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RpDaInitialStartSource)));
        Assert.NotNull(entityFile);

        // Assert
        Assert.Contains("InitialStart = \"2025-06-01T00:00:00", entityFile.Code);
    }

    #endregion

    #region Should_Not_Render_RetentionPolicyAttribute_When_IsHypertable_Not_Consumed_In_DataAnnotations_Scaffold

    private class RpDaGuardSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RpDaGuardContext : DbContext
    {
        public DbSet<RpDaGuardSource> Items => Set<RpDaGuardSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RpDaGuardSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rp_da_guard_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Not_Render_RetentionPolicyAttribute_When_IsHypertable_Not_Consumed_In_DataAnnotations_Scaffold()
    {
        // Arrange & Act
        using RpDaGuardContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? entityFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RpDaGuardSource)));
        Assert.NotNull(entityFile);

        // Assert
        Assert.DoesNotContain("[RetentionPolicy(", entityFile.Code);
    }

    #endregion

    // ── Reorder policy rendering ──────────────────────────────────────────────

    #region Should_Render_WithReorderPolicy_Chained_In_Fluent_Scaffold_For_Hypertable

    private class RrpHtSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpHtContextA : DbContext
    {
        public DbSet<RrpHtSource> Items => Set<RrpHtSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpHtSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_ht_source_a");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_ht_source_a");
            });
        }
    }

    [Fact]
    public void Should_Render_WithReorderPolicy_Chained_In_Fluent_Scaffold_For_Hypertable()
    {
        // Arrange & Act
        using RrpHtContextA context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithReorderPolicy(", code);
    }

    #endregion

    #region Should_Render_WithReorderPolicy_With_IndexName_Only_Five_Nulls_In_Fluent_Scaffold

    private class RrpFiveNullsSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpFiveNullsContext : DbContext
    {
        public DbSet<RrpFiveNullsSource> Items => Set<RrpFiveNullsSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpFiveNullsSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_five_nulls_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_t");
            });
        }
    }

    [Fact]
    public void Should_Render_WithReorderPolicy_With_IndexName_Only_Five_Nulls_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RrpFiveNullsContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithReorderPolicy(\"ix_t\", null, null, null, null)", code);
    }

    #endregion

    #region Should_Render_WithReorderPolicy_With_ScheduleInterval_In_Fluent_Scaffold

    private class RrpScheduleIntervalSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpScheduleIntervalContext : DbContext
    {
        public DbSet<RrpScheduleIntervalSource> Items => Set<RrpScheduleIntervalSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpScheduleIntervalSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_schedule_interval_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_t");
                e.HasAnnotation(ReorderPolicyAnnotations.ScheduleInterval, "2 days");
            });
        }
    }

    [Fact]
    public void Should_Render_WithReorderPolicy_With_ScheduleInterval_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RrpScheduleIntervalContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithReorderPolicy(\"ix_t\", \"2 days\", null, null, null)", code);
    }

    #endregion

    #region Should_Render_WithReorderPolicy_Five_Args_In_Fluent_Scaffold

    private class RrpFiveArgsSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpFiveArgsContext : DbContext
    {
        public DbSet<RrpFiveArgsSource> Items => Set<RrpFiveArgsSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpFiveArgsSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_five_args_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_t");
                e.HasAnnotation(ReorderPolicyAnnotations.ScheduleInterval, "2 days");
                e.HasAnnotation(ReorderPolicyAnnotations.MaxRuntime, "01:00:00");
                e.HasAnnotation(ReorderPolicyAnnotations.MaxRetries, 3);
                e.HasAnnotation(ReorderPolicyAnnotations.RetryPeriod, "00:10:00");
            });
        }
    }

    [Fact]
    public void Should_Render_WithReorderPolicy_Five_Args_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RrpFiveArgsContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithReorderPolicy(\"ix_t\", \"2 days\", \"01:00:00\", 3, \"00:10:00\")", code);
    }

    #endregion

    #region Should_Render_WithInitialStart_Chained_After_WithReorderPolicy_In_Fluent_Scaffold

    private class RrpInitialStartSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpInitialStartContext : DbContext
    {
        public DbSet<RrpInitialStartSource> Items => Set<RrpInitialStartSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpInitialStartSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_initial_start_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_initial_start");
                e.HasAnnotation(ReorderPolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Render_WithInitialStart_Chained_After_WithReorderPolicy_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RrpInitialStartContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithReorderPolicy(", code);
        Assert.Contains(".WithInitialStart(", code);
        Assert.True(
            code.IndexOf(".WithReorderPolicy(", StringComparison.Ordinal) <
            code.IndexOf(".WithInitialStart(", StringComparison.Ordinal),
            ".WithInitialStart( must follow .WithReorderPolicy(.");
    }

    #endregion

    #region Should_Render_WithReorderPolicy_After_WithRetentionPolicy_In_Fluent_Scaffold

    private class RrpChainOrderSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpChainOrderContext : DbContext
    {
        public DbSet<RrpChainOrderSource> Items => Set<RrpChainOrderSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpChainOrderSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_chain_order_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(RetentionPolicyAnnotations.HasRetentionPolicy, true);
                e.HasAnnotation(RetentionPolicyAnnotations.DropAfter, "7 days");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_chain_order");
            });
        }
    }

    [Fact]
    public void Should_Render_WithReorderPolicy_After_WithRetentionPolicy_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RrpChainOrderContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.Contains(".WithRetentionPolicy(", code);
        Assert.Contains(".WithReorderPolicy(", code);
        Assert.True(
            code.IndexOf(".WithRetentionPolicy(", StringComparison.Ordinal) <
            code.IndexOf(".WithReorderPolicy(", StringComparison.Ordinal),
            ".WithRetentionPolicy( must precede .WithReorderPolicy( in the chain.");
    }

    #endregion

    #region Should_Not_Render_WithReorderPolicy_When_IsHypertable_Still_Present_In_Fluent_Scaffold

    private class RrpGuardHtSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpGuardHtContext : DbContext
    {
        public DbSet<RrpGuardHtSource> Items => Set<RrpGuardHtSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpGuardHtSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_guard_ht_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_guard_ht");
            });
        }
    }

    [Fact]
    public void Should_Not_Render_WithReorderPolicy_When_IsHypertable_Still_Present_In_Fluent_Scaffold()
    {
        // Arrange & Act
        using RrpGuardHtContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: false);
        string code = result.ContextFile.Code;

        // Assert
        Assert.DoesNotContain(".WithReorderPolicy(", code);
    }

    #endregion

    #region Should_Render_ReorderPolicyAttribute_In_DataAnnotations_Scaffold

    private class RrpDaSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpDaContext : DbContext
    {
        public DbSet<RrpDaSource> Items => Set<RrpDaSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpDaSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_da_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_da");
            });
        }
    }

    [Fact]
    public void Should_Render_ReorderPolicyAttribute_In_DataAnnotations_Scaffold()
    {
        // Arrange & Act
        using RrpDaContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? entityFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RrpDaSource)));
        Assert.NotNull(entityFile);

        // Assert
        Assert.Contains("[ReorderPolicy(", entityFile.Code);
        Assert.DoesNotContain(".WithReorderPolicy(", result.ContextFile.Code);
    }

    #endregion

    #region Should_Render_ReorderPolicyAttribute_With_InitialStart_As_ISO8601_In_DataAnnotations_Scaffold

    private class RrpDaInitialStartSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpDaInitialStartContext : DbContext
    {
        public DbSet<RrpDaInitialStartSource> Items => Set<RrpDaInitialStartSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpDaInitialStartSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_da_initial_start_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "time");
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_da_initial_start");
                e.HasAnnotation(ReorderPolicyAnnotations.InitialStart,
                    new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Render_ReorderPolicyAttribute_With_InitialStart_As_ISO8601_In_DataAnnotations_Scaffold()
    {
        // Arrange & Act
        using RrpDaInitialStartContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? entityFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RrpDaInitialStartSource)));
        Assert.NotNull(entityFile);

        // Assert
        Assert.Contains("InitialStart = \"2025-06-01T00:00:00", entityFile.Code);
    }

    #endregion

    #region Should_Not_Render_ReorderPolicyAttribute_When_IsHypertable_Not_Consumed_In_DataAnnotations_Scaffold

    private class RrpDaGuardSource { public DateTime Time { get; set; } public double Value { get; set; } }

    private class RrpDaGuardContext : DbContext
    {
        public DbSet<RrpDaGuardSource> Items => Set<RrpDaGuardSource>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrpDaGuardSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("rrp_da_guard_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                e.HasAnnotation(ReorderPolicyAnnotations.HasReorderPolicy, true);
                e.HasAnnotation(ReorderPolicyAnnotations.IndexName, "ix_rrp_da_guard");
            });
        }
    }

    [Fact]
    public void Should_Not_Render_ReorderPolicyAttribute_When_IsHypertable_Not_Consumed_In_DataAnnotations_Scaffold()
    {
        // Arrange & Act
        using RrpDaGuardContext context = new();
        ScaffoldedModel result = Generate(context, useDataAnnotations: true);

        ScaffoldedFile? entityFile = result.AdditionalFiles.FirstOrDefault(f => f.Path.Contains(nameof(RrpDaGuardSource)));
        Assert.NotNull(entityFile);

        // Assert
        Assert.DoesNotContain("[ReorderPolicy(", entityFile.Code);
    }

    #endregion
}
#pragma warning restore EF1001

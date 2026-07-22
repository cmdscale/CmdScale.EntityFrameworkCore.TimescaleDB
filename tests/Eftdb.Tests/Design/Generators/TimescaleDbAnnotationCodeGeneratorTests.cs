#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

/// <summary>
/// Tests for the guard conditions, attribute caching, and source-argument resolution of
/// <see cref="TimescaleDbAnnotationCodeGenerator"/>. Feature-specific rendering is tested in the
/// renderer test classes.
/// </summary>
public class TimescaleDbAnnotationCodeGeneratorTests
{
    private sealed record StubAnnotation(string Name, object? Value) : IAnnotation;

    private static Dictionary<string, IAnnotation> Annotations(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => (IAnnotation)new StubAnnotation(p.Key, p.Value));

    private static TimescaleDbAnnotationCodeGenerator CreateGenerator(bool scaffoldMode = false)
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
            .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();
        generator.ScaffoldMode = scaffoldMode;
        return generator;
    }

    private static IEntityType GetEntityType<T>(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!;


    #region GenerateFluentApiCalls_Entity_ScaffoldMode_False_Returns_Base_Calls

    private class ScaffoldFalseEntity1 { public int Id { get; set; } }

    private class ScaffoldFalseContext1 : DbContext
    {
        public DbSet<ScaffoldFalseEntity1> Items => Set<ScaffoldFalseEntity1>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<ScaffoldFalseEntity1>(e => { e.HasKey(x => x.Id); e.ToTable("scaffold_false_1"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Entity_ScaffoldMode_False_Returns_Base_Calls()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: false);
        using ScaffoldFalseContext1 context = new();
        IEntityType entityType = GetEntityType<ScaffoldFalseEntity1>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "scaffold_false_1"),
            (ContinuousAggregateAnnotations.ParentName, "some_parent"),
            (ContinuousAggregateAnnotations.ViewDefinition,
                "SELECT time_bucket('1 hour'::interval, t.time) AS bucket FROM some_parent t GROUP BY 1"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f =>
        {
            for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall)
                if (c.Method == "IsContinuousAggregate") return true;
            return false;
        });
    }

    #endregion


    #region GenerateDataAnnotationAttributes_Entity_ScaffoldMode_False_Returns_Base_Attributes

    private class ScaffoldFalseEntity2 { public int Id { get; set; } }

    private class ScaffoldFalseContext2 : DbContext
    {
        public DbSet<ScaffoldFalseEntity2> Items => Set<ScaffoldFalseEntity2>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<ScaffoldFalseEntity2>(e => { e.HasKey(x => x.Id); e.ToTable("scaffold_false_2"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Entity_ScaffoldMode_False_Returns_Base_Attributes()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: false);
        using ScaffoldFalseContext2 context = new();
        IEntityType entityType = GetEntityType<ScaffoldFalseEntity2>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "scaffold_false_2"),
            (ContinuousAggregateAnnotations.ParentName, "some_parent"),
            (ContinuousAggregateAnnotations.ViewDefinition,
                "SELECT time_bucket('1 hour'::interval, t.time) AS bucket FROM some_parent t GROUP BY 1"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ContinuousAggregateAttribute));
    }

    #endregion


    #region GenerateDataAnnotationAttributes_Property_ScaffoldMode_False_Returns_Base_Attributes

    private class PropScaffoldFalseSource { public DateTime Time { get; set; } }
    private class PropScaffoldFalseCa { public double AvgValue { get; set; } }

    private class PropScaffoldFalseContext : DbContext
    {
        public DbSet<PropScaffoldFalseSource> Sources => Set<PropScaffoldFalseSource>();
        public DbSet<PropScaffoldFalseCa> CaViews => Set<PropScaffoldFalseCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PropScaffoldFalseSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("prop_sf_source");
            });

            modelBuilder.Entity<PropScaffoldFalseCa>(e =>
            {
                e.HasNoKey();
                e.ToView("prop_sf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "prop_sf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "prop_sf_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "AvgValue:Avg:Time" });
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_ScaffoldMode_False_Returns_Base_Attributes()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: false);
        using PropScaffoldFalseContext context = new();
        IEntityType entityType = GetEntityType<PropScaffoldFalseCa>(context);
        IProperty property = entityType.FindProperty(nameof(PropScaffoldFalseCa.AvgValue))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_NonEntityDeclaringType_Returns_Empty

    private class ComplexOwnerForGen { public int Id { get; set; } public NestedAddress Address { get; set; } = null!; }
    private class NestedAddress { public string City { get; set; } = ""; }

    private class ComplexTypeGenContext : DbContext
    {
        public DbSet<ComplexOwnerForGen> Owners => Set<ComplexOwnerForGen>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ComplexOwnerForGen>(e =>
            {
                e.HasKey(x => x.Id);
                e.ToTable("complex_owner_gen");
                e.ComplexProperty(x => x.Address);
            });
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_NonEntityDeclaringType_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using ComplexTypeGenContext context = new();
        IEntityType entityType = GetEntityType<ComplexOwnerForGen>(context);
        IProperty cityProperty = entityType.GetComplexProperties().Single().ComplexType.GetProperties()
            .Single(p => p.Name == nameof(NestedAddress.City));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(cityProperty, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(GroupByColumnAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_EntityWithout_MaterializedViewName_Returns_Empty

    private class NoMvnEntity { public int Id { get; set; } public double Amount { get; set; } }

    private class NoMvnContext : DbContext
    {
        public DbSet<NoMvnEntity> Items => Set<NoMvnEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<NoMvnEntity>(e => { e.HasKey(x => x.Id); e.ToTable("no_mvn_gen"); });
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_EntityWithout_MaterializedViewName_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using NoMvnContext context = new();
        IEntityType entityType = GetEntityType<NoMvnEntity>(context);
        IProperty property = entityType.FindProperty(nameof(NoMvnEntity.Amount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(GroupByColumnAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_AggregateEntry_With_Too_Few_Parts_Returns_Empty

    private class TooFewPartsSource { public DateTime Time { get; set; } }
    private class TooFewPartsCa { public double AvgValue { get; set; } }

    private class TooFewPartsContext : DbContext
    {
        public DbSet<TooFewPartsSource> Sources => Set<TooFewPartsSource>();
        public DbSet<TooFewPartsCa> CaViews => Set<TooFewPartsCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TooFewPartsSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("too_few_parts_source");
            });

            modelBuilder.Entity<TooFewPartsCa>(e =>
            {
                e.HasNoKey();
                e.ToView("too_few_parts_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "too_few_parts_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "too_few_parts_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "AvgValue:Avg" });
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_AggregateEntry_With_Too_Few_Parts_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using TooFewPartsContext context = new();
        IEntityType entityType = GetEntityType<TooFewPartsCa>(context);
        IProperty property = entityType.FindProperty(nameof(TooFewPartsCa.AvgValue))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
    }

    #endregion


    #region GenerateDataAnnotationAttributes_Entity_Cache_Hit_Returns_Same_Result

    private class CacheHitEntitySource { public DateTime Time { get; set; } public double Value { get; set; } }
    private class CacheHitCaEntity { public DateTime Bucket { get; set; } public double AvgValue { get; set; } }

    private class CacheHitEntityContext : DbContext
    {
        public DbSet<CacheHitEntitySource> Sources => Set<CacheHitEntitySource>();
        public DbSet<CacheHitCaEntity> CaViews => Set<CacheHitCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CacheHitEntitySource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("cache_hit_source");
                e.Property(x => x.Value).HasColumnName("value");
            });

            modelBuilder.Entity<CacheHitCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("cache_hit_ca");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Entity_Cache_Hit_Returns_Same_Result()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using CacheHitEntityContext context = new();
        IEntityType entityType = GetEntityType<CacheHitCaEntity>(context);

        Dictionary<string, IAnnotation> annotations1 = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "cache_hit_ca"),
            (ContinuousAggregateAnnotations.ParentName, "cache_hit_source"),
            (ContinuousAggregateAnnotations.ViewDefinition,
                "SELECT time_bucket('01:00:00'::interval, s.time) AS bucket, avg(s.value) AS avg_value FROM cache_hit_source s GROUP BY 1"));

        // Act
        IReadOnlyList<AttributeCodeFragment> first = generator.GenerateDataAnnotationAttributes(entityType, annotations1);

        Dictionary<string, IAnnotation> annotations2 = Annotations();
        IReadOnlyList<AttributeCodeFragment> second = generator.GenerateDataAnnotationAttributes(entityType, annotations2);

        // Assert
        IEnumerable<Type> firstTypes = first.Select(a => a.Type).OrderBy(t => t.Name);
        IEnumerable<Type> secondTypes = second.Select(a => a.Type).OrderBy(t => t.Name);
        Assert.Equal(firstTypes, secondTypes);
    }

    #endregion


    #region GenerateDataAnnotationAttributes_Property_Cache_Hit_Returns_Same_Result

    private class PropCacheHitSource { public DateTime Time { get; set; } public double Price { get; set; } }
    private class PropCacheHitCa { public double MaxPrice { get; set; } }

    private class PropCacheHitContext : DbContext
    {
        public DbSet<PropCacheHitSource> Sources => Set<PropCacheHitSource>();
        public DbSet<PropCacheHitCa> CaViews => Set<PropCacheHitCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PropCacheHitSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("prop_cache_hit_src");
                e.Property(x => x.Price).HasColumnName("price");
            });

            modelBuilder.Entity<PropCacheHitCa>(e =>
            {
                e.HasNoKey();
                e.ToView("prop_cache_hit_ca", "public");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "prop_cache_hit_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "prop_cache_hit_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('01:00:00'::interval, s.time) AS bucket, max(s.price) AS max_price FROM prop_cache_hit_src s GROUP BY 1");
                e.Property(x => x.MaxPrice).HasColumnName("max_price");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_Cache_Hit_Returns_Same_Result()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using PropCacheHitContext context = new();
        IEntityType entityType = GetEntityType<PropCacheHitCa>(context);
        IProperty property = entityType.FindProperty(nameof(PropCacheHitCa.MaxPrice))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> first = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        IReadOnlyList<AttributeCodeFragment> second = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.Equal(
            first.Select(a => a.Type).OrderBy(t => t.Name),
            second.Select(a => a.Type).OrderBy(t => t.Name));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_CountStar_Returns_Wildcard_Source

    private class CountStarSource { public DateTime Time { get; set; } public double Value { get; set; } }
    private class CountStarCa { public long TradeCount { get; set; } }

    private class CountStarContext : DbContext
    {
        public DbSet<CountStarSource> Sources => Set<CountStarSource>();
        public DbSet<CountStarCa> CaViews => Set<CountStarCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CountStarSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("count_star_src");
                e.Property(x => x.Value).HasColumnName("value");
            });

            modelBuilder.Entity<CountStarCa>(e =>
            {
                e.HasNoKey();
                e.ToView("count_star_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "count_star_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "count_star_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "TradeCount:Count:*" });
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_CountStar_Returns_Wildcard_Source()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using CountStarContext context = new();
        IEntityType entityType = GetEntityType<CountStarCa>(context);
        IProperty property = entityType.FindProperty(nameof(CountStarCa.TradeCount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Count, attr.Arguments[0]);
        Assert.Equal("*", attr.Arguments[1]);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_NoParentEntityType_Returns_RawClrName

    private class NoParentCa { public double AvgValue { get; set; } }

    private class NoParentContext : DbContext
    {
        public DbSet<NoParentCa> CaViews => Set<NoParentCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoParentCa>(e =>
            {
                e.HasNoKey();
                e.ToView("no_parent_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "no_parent_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "AvgValue:Avg:SomeSourceColumn" });
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_NoParentEntityType_Returns_RawClrName()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        using NoParentContext context = new();
        IEntityType entityType = GetEntityType<NoParentCa>(context);
        IProperty property = entityType.FindProperty(nameof(NoParentCa.AvgValue))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Avg, attr.Arguments[0]);
        string? sourceArg = attr.Arguments[1] as string;
        Assert.NotNull(sourceArg);
        Assert.Equal("SomeSourceColumn", sourceArg);
    }

    #endregion


    #region GenerateFluentApiCalls_Entity_ConsumeAnnotations_When_DataAnnotationsMode_True

    private class DaModeSuppressSource { public DateTime Time { get; set; } public double Revenue { get; set; } }
    private class DaModeSuppressCa { public double SumRevenue { get; set; } }

    private class DaModeSuppressContext : DbContext
    {
        public DbSet<DaModeSuppressSource> Sources => Set<DaModeSuppressSource>();
        public DbSet<DaModeSuppressCa> CaViews => Set<DaModeSuppressCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DaModeSuppressSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("da_mode_suppress_src");
                e.Property(x => x.Revenue).HasColumnName("revenue");
            });

            modelBuilder.Entity<DaModeSuppressCa>(e =>
            {
                e.HasNoKey();
                e.ToView("da_mode_suppress_ca");
                e.Property(x => x.SumRevenue).HasColumnName("sum_revenue");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_Entity_ConsumeAnnotations_When_DataAnnotationsMode_True()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DaModeSuppressContext context = new();
        IEntityType entityType = GetEntityType<DaModeSuppressCa>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "da_mode_suppress_ca"),
            (ContinuousAggregateAnnotations.ParentName, "da_mode_suppress_src"),
            (ContinuousAggregateAnnotations.ViewDefinition,
                "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, sum(s.revenue) AS sum_revenue FROM da_mode_suppress_src s GROUP BY 1"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f =>
        {
            for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall)
                if (c.Method == "IsContinuousAggregate") return true;
            return false;
        });
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:", StringComparison.Ordinal));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_Aggregate_Match_Returns_AggregateAttribute

    private class DbFirstAggSource { public DateTime Time { get; set; } public double Val { get; set; } }
    private class DbFirstAggCa { public double AvgVal { get; set; } }

    private class DbFirstAggContext : DbContext
    {
        public DbSet<DbFirstAggSource> Sources => Set<DbFirstAggSource>();
        public DbSet<DbFirstAggCa> CaViews => Set<DbFirstAggCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstAggSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_agg_source");
                e.Property(x => x.Val).HasColumnName("val");
            });

            modelBuilder.Entity<DbFirstAggCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_agg_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_agg_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_agg_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.val) AS avg_val FROM dbf_agg_source s GROUP BY 1");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_Aggregate_Match_Returns_AggregateAttribute()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstAggContext context = new();
        IEntityType entityType = GetEntityType<DbFirstAggCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstAggCa.AvgVal))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Avg, attr.Arguments[0]);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_Match_Returns_GroupByAttribute

    private class DbFirstGbSource { public DateTime Time { get; set; } public string Region { get; set; } = ""; }
    private class DbFirstGbCa { public string Region { get; set; } = ""; }

    private class DbFirstGbContext : DbContext
    {
        public DbSet<DbFirstGbSource> Sources => Set<DbFirstGbSource>();
        public DbSet<DbFirstGbCa> CaViews => Set<DbFirstGbCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstGbSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_gb_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Region).HasColumnName("region");
            });

            modelBuilder.Entity<DbFirstGbCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_gb_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_gb_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_gb_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, s.region AS region" +
                    " FROM dbf_gb_source s GROUP BY time_bucket('1 hour'::interval, s.time), s.region");
                e.Property(x => x.Region).HasColumnName("region");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_Match_Returns_GroupByAttribute()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstGbContext context = new();
        IEntityType entityType = GetEntityType<DbFirstGbCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstGbCa.Region))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.Contains(result, a => a.Type == typeof(GroupByColumnAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_NoMatch_Returns_Empty

    private class DbFirstNoMatchSource { public DateTime Time { get; set; } public double Score { get; set; } }
    private class DbFirstNoMatchCa { public string ExtraCol { get; set; } = ""; }

    private class DbFirstNoMatchContext : DbContext
    {
        public DbSet<DbFirstNoMatchSource> Sources => Set<DbFirstNoMatchSource>();
        public DbSet<DbFirstNoMatchCa> CaViews => Set<DbFirstNoMatchCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstNoMatchSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_nomatch_source");
                e.Property(x => x.Score).HasColumnName("score");
            });

            modelBuilder.Entity<DbFirstNoMatchCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_nomatch_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_nomatch_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_nomatch_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.score) AS avg_score FROM dbf_nomatch_source s GROUP BY 1");
                e.Property(x => x.ExtraCol).HasColumnName("extra_col");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_NoMatch_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstNoMatchContext context = new();
        IEntityType entityType = GetEntityType<DbFirstNoMatchCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstNoMatchCa.ExtraCol))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(GroupByColumnAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_CountStar_Aggregate_Returns_Wildcard

    private class DbFirstCsSource { public DateTime Time { get; set; } public double Amount { get; set; } }
    private class DbFirstCsCa { public long EventCount { get; set; } }

    private class DbFirstCsContext : DbContext
    {
        public DbSet<DbFirstCsSource> Sources => Set<DbFirstCsSource>();
        public DbSet<DbFirstCsCa> CaViews => Set<DbFirstCsCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstCsSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_cs_source");
                e.Property(x => x.Amount).HasColumnName("amount");
            });

            modelBuilder.Entity<DbFirstCsCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_cs_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_cs_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_cs_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, count(*) AS event_count FROM dbf_cs_source s GROUP BY 1");
                e.Property(x => x.EventCount).HasColumnName("event_count");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_CountStar_Aggregate_Returns_Wildcard()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstCsContext context = new();
        IEntityType entityType = GetEntityType<DbFirstCsCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstCsCa.EventCount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Count, attr.Arguments[0]);
        Assert.Equal("*", attr.Arguments[1]);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_Default_NoArg_When_NameMatches

    private class DbFirstGbMatchSource { public DateTime Time { get; set; } public string Region { get; set; } = ""; }
    private class DbFirstGbMatchCa { public string Region { get; set; } = ""; }

    private class DbFirstGbMatchContext : DbContext
    {
        public DbSet<DbFirstGbMatchSource> Sources => Set<DbFirstGbMatchSource>();
        public DbSet<DbFirstGbMatchCa> CaViews => Set<DbFirstGbMatchCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstGbMatchSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_gbmatch_src");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Region).HasColumnName("region");
            });

            modelBuilder.Entity<DbFirstGbMatchCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_gbmatch_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_gbmatch_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_gbmatch_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, s.region AS region" +
                    " FROM dbf_gbmatch_src s GROUP BY time_bucket('1 hour'::interval, s.time), s.region");
                e.Property(x => x.Region).HasColumnName("region");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_Default_NoArg_When_NameMatches()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstGbMatchContext context = new();
        IEntityType entityType = GetEntityType<DbFirstGbMatchCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstGbMatchCa.Region))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(GroupByColumnAttribute));
        Assert.NotNull(attr);
        Assert.Empty(attr.Arguments);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_NoViewDefinition_Returns_Empty

    private class DbFirstNvdSource { public DateTime Time { get; set; } public double Amount { get; set; } }
    private class DbFirstNvdCa { public double TotalAmount { get; set; } }

    private class DbFirstNvdContext : DbContext
    {
        public DbSet<DbFirstNvdSource> Sources => Set<DbFirstNvdSource>();
        public DbSet<DbFirstNvdCa> CaViews => Set<DbFirstNvdCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstNvdSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_nvd_source");
            });

            modelBuilder.Entity<DbFirstNvdCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_nvd_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_nvd_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_nvd_source");
                e.Property(x => x.TotalAmount).HasColumnName("total_amount");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_NoViewDefinition_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstNvdContext context = new();
        IEntityType entityType = GetEntityType<DbFirstNvdCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstNvdCa.TotalAmount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(GroupByColumnAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_NullParent_Returns_RawColumnName

    private class DbFirstNullParentCa { public double AvgScore { get; set; } }

    private class DbFirstNullParentContext : DbContext
    {
        public DbSet<DbFirstNullParentCa> CaViews => Set<DbFirstNullParentCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstNullParentCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_null_parent_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_null_parent_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.score) AS avg_score FROM some_source s GROUP BY 1");
                e.Property(x => x.AvgScore).HasColumnName("avg_score");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_NullParent_Returns_RawColumnName()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstNullParentContext context = new();
        IEntityType entityType = GetEntityType<DbFirstNullParentCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstNullParentCa.AvgScore))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Avg, attr.Arguments[0]);
        string? sourceArg = attr.Arguments[1] as string;
        Assert.NotNull(sourceArg);
        Assert.Equal("score", sourceArg);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_ParentPropNotFound_Returns_RawColumnName

    private class DbFirstPpnfSource { public DateTime Time { get; set; } public double Revenue { get; set; } }
    private class DbFirstPpnfCa { public double AvgObscureColumn { get; set; } }

    private class DbFirstPpnfContext : DbContext
    {
        public DbSet<DbFirstPpnfSource> Sources => Set<DbFirstPpnfSource>();
        public DbSet<DbFirstPpnfCa> CaViews => Set<DbFirstPpnfCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstPpnfSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_ppnf_source");
                e.Property(x => x.Revenue).HasColumnName("revenue");
            });

            modelBuilder.Entity<DbFirstPpnfCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_ppnf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_ppnf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_ppnf_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.obscure_column) AS avg_obscure_column FROM dbf_ppnf_source s GROUP BY 1");
                e.Property(x => x.AvgObscureColumn).HasColumnName("avg_obscure_column");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_ParentPropNotFound_Returns_RawColumnName()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstPpnfContext context = new();
        IEntityType entityType = GetEntityType<DbFirstPpnfCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstPpnfCa.AvgObscureColumn))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Avg, attr.Arguments[0]);
        string? sourceArg = attr.Arguments[1] as string;
        Assert.NotNull(sourceArg);
        Assert.Equal("obscure_column", sourceArg);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_CountStar_Wildcard_Via_ColumnName_Resolver

    private class DbFirstCsWcSource { public DateTime Time { get; set; } }
    private class DbFirstCsWcCa { public long TotalCount { get; set; } }

    private class DbFirstCsWcContext : DbContext
    {
        public DbSet<DbFirstCsWcSource> Sources => Set<DbFirstCsWcSource>();
        public DbSet<DbFirstCsWcCa> CaViews => Set<DbFirstCsWcCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstCsWcSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_cs_wc_source");
            });

            modelBuilder.Entity<DbFirstCsWcCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_cs_wc_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_cs_wc_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_cs_wc_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, count(*) AS total_count FROM dbf_cs_wc_source s GROUP BY 1");
                e.Property(x => x.TotalCount).HasColumnName("total_count");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_CountStar_Wildcard_Via_ColumnName_Resolver()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstCsWcContext context = new();
        IEntityType entityType = GetEntityType<DbFirstCsWcCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstCsWcCa.TotalCount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Count, attr.Arguments[0]);
        Assert.Equal("*", attr.Arguments[1]);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_WithArg_When_NameMismatch

    private class DbFirstGbMismatchSource { public DateTime Time { get; set; } public string ServiceName { get; set; } = ""; }
    private class DbFirstGbMismatchCa { public string Svc { get; set; } = ""; }

    private class DbFirstGbMismatchContext : DbContext
    {
        public DbSet<DbFirstGbMismatchSource> Sources => Set<DbFirstGbMismatchSource>();
        public DbSet<DbFirstGbMismatchCa> CaViews => Set<DbFirstGbMismatchCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstGbMismatchSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_gbmismatch_src");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });

            modelBuilder.Entity<DbFirstGbMismatchCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_gbmismatch_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_gbmismatch_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_gbmismatch_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, s.service_name AS service_name" +
                    " FROM dbf_gbmismatch_src s GROUP BY time_bucket('1 hour'::interval, s.time), s.service_name");
                e.Property(x => x.Svc).HasColumnName("service_name");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_WithArg_When_NameMismatch()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstGbMismatchContext context = new();
        IEntityType entityType = GetEntityType<DbFirstGbMismatchCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstGbMismatchCa.Svc))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(GroupByColumnAttribute));
        Assert.NotNull(attr);
        Assert.NotEmpty(attr.Arguments);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_CodeFirst_Entry_Null_Returns_Empty

    private class CfEntryNullSource { public DateTime Time { get; set; } public double Val { get; set; } }
    private class CfEntryNullCa { public double AvgVal { get; set; } public string? UnlistedProp { get; set; } }

    private class CfEntryNullContext : DbContext
    {
        public DbSet<CfEntryNullSource> Sources => Set<CfEntryNullSource>();
        public DbSet<CfEntryNullCa> CaViews => Set<CfEntryNullCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CfEntryNullSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("cf_entry_null_src");
                e.Property(x => x.Val).HasColumnName("val");
            });

            modelBuilder.Entity<CfEntryNullCa>(e =>
            {
                e.HasNoKey();
                e.ToView("cf_entry_null_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "cf_entry_null_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "cf_entry_null_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "AvgVal:Avg:val" });
                e.Property(x => x.UnlistedProp).HasColumnName("unlisted_prop");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_CodeFirst_Entry_Null_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using CfEntryNullContext context = new();
        IEntityType entityType = GetEntityType<CfEntryNullCa>(context);
        IProperty property = entityType.FindProperty(nameof(CfEntryNullCa.UnlistedProp))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
        Assert.DoesNotContain(result, a => a.Type == typeof(GroupByColumnAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_NoViewName_UsesTableName

    private class DbFirstNoViewNameSource { public DateTime Time { get; set; } public double Amount { get; set; } }
    private class DbFirstNoViewNameCa { public double SumAmount { get; set; } }

    private class DbFirstNoViewNameContext : DbContext
    {
        public DbSet<DbFirstNoViewNameSource> Sources => Set<DbFirstNoViewNameSource>();
        public DbSet<DbFirstNoViewNameCa> CaViews => Set<DbFirstNoViewNameCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstNoViewNameSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_no_view_src");
                e.Property(x => x.Amount).HasColumnName("amount");
            });

            modelBuilder.Entity<DbFirstNoViewNameCa>(e =>
            {
                e.HasNoKey();
                e.ToTable("dbf_no_view_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_no_view_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_no_view_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, sum(s.amount) AS sum_amount FROM dbf_no_view_src s GROUP BY 1");
                e.Property(x => x.SumAmount).HasColumnName("sum_amount");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_NoViewName_UsesTableName()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstNoViewNameContext context = new();
        IEntityType entityType = GetEntityType<DbFirstNoViewNameCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstNoViewNameCa.SumAmount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.NotNull(result);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_CodeFirst_WithParent_Returns_NameOf

    private class CfWithParentSource { public DateTime Time { get; set; } public double Revenue { get; set; } }
    private class CfWithParentCa { public double TotalRevenue { get; set; } }

    private class CfWithParentContext : DbContext
    {
        public DbSet<CfWithParentSource> Sources => Set<CfWithParentSource>();
        public DbSet<CfWithParentCa> CaViews => Set<CfWithParentCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CfWithParentSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("cf_with_parent_src");
                e.Property(x => x.Revenue).HasColumnName("revenue");
            });

            modelBuilder.Entity<CfWithParentCa>(e =>
            {
                e.HasNoKey();
                e.ToView("cf_with_parent_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "cf_with_parent_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "cf_with_parent_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "TotalRevenue:Sum:Revenue" });
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_CodeFirst_WithParent_Returns_NameOf()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using CfWithParentContext context = new();
        IEntityType entityType = GetEntityType<CfWithParentCa>(context);
        IProperty property = entityType.FindProperty(nameof(CfWithParentCa.TotalRevenue))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Sum, attr.Arguments[0]);
        Assert.IsNotType<string>(attr.Arguments[1]);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_CodeFirst_WithParent_PropNotFound_Returns_RawString

    private class CfPropNotFoundSource { public DateTime Time { get; set; } public double Amount { get; set; } }
    private class CfPropNotFoundCa { public double TotalAmount { get; set; } }

    private class CfPropNotFoundContext : DbContext
    {
        public DbSet<CfPropNotFoundSource> Sources => Set<CfPropNotFoundSource>();
        public DbSet<CfPropNotFoundCa> CaViews => Set<CfPropNotFoundCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CfPropNotFoundSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("cf_prop_nf_src");
            });

            modelBuilder.Entity<CfPropNotFoundCa>(e =>
            {
                e.HasNoKey();
                e.ToView("cf_prop_nf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "cf_prop_nf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "cf_prop_nf_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "TotalAmount:Sum:NonExistentProp" });
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_CodeFirst_WithParent_PropNotFound_Returns_RawString()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using CfPropNotFoundContext context = new();
        IEntityType entityType = GetEntityType<CfPropNotFoundCa>(context);
        IProperty property = entityType.FindProperty(nameof(CfPropNotFoundCa.TotalAmount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Sum, attr.Arguments[0]);
        string? sourceArg = attr.Arguments[1] as string;
        Assert.NotNull(sourceArg);
        Assert.Equal("NonExistentProp", sourceArg);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_CodeFirst_InvalidFunctionName_Returns_Empty

    private class CfInvalidFuncSource { public DateTime Time { get; set; } public double Val { get; set; } }
    private class CfInvalidFuncCa { public double BadAgg { get; set; } }

    private class CfInvalidFuncContext : DbContext
    {
        public DbSet<CfInvalidFuncSource> Sources => Set<CfInvalidFuncSource>();
        public DbSet<CfInvalidFuncCa> CaViews => Set<CfInvalidFuncCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CfInvalidFuncSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("cf_invalid_func_src");
            });

            modelBuilder.Entity<CfInvalidFuncCa>(e =>
            {
                e.HasNoKey();
                e.ToView("cf_invalid_func_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "cf_invalid_func_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "cf_invalid_func_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "BadAgg:INVALID_FUNCTION:val" });
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_CodeFirst_InvalidFunctionName_Returns_Empty()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using CfInvalidFuncContext context = new();
        IEntityType entityType = GetEntityType<CfInvalidFuncCa>(context);
        IProperty property = entityType.FindProperty(nameof(CfInvalidFuncCa.BadAgg))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_NullColumnName_FallsBack_To_PropertyName

    private class DbFirstNullColNameSource { public DateTime Time { get; set; } public double Price { get; set; } }

    private class DbFirstNullColNameCa { public double Price { get; set; } }

    private class DbFirstNullColNameContext : DbContext
    {
        public DbSet<DbFirstNullColNameSource> Sources => Set<DbFirstNullColNameSource>();
        public DbSet<DbFirstNullColNameCa> CaViews => Set<DbFirstNullColNameCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstNullColNameSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_null_col_src");
            });

            modelBuilder.Entity<DbFirstNullColNameCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_null_col_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_null_col_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_null_col_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.price) AS price FROM dbf_null_col_src s GROUP BY 1");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_NullColumnName_FallsBack_To_PropertyName()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstNullColNameContext context = new();
        IEntityType entityType = GetEntityType<DbFirstNullColNameCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstNullColNameCa.Price))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.NotNull(result);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_ParentColumnNotFound_EmitsWithArg

    private class DbFirstGbPcnfSource { public DateTime Time { get; set; } }
    private class DbFirstGbPcnfCa { public string ServiceRegion { get; set; } = ""; }

    private class DbFirstGbPcnfContext : DbContext
    {
        public DbSet<DbFirstGbPcnfSource> Sources => Set<DbFirstGbPcnfSource>();
        public DbSet<DbFirstGbPcnfCa> CaViews => Set<DbFirstGbPcnfCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstGbPcnfSource>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("dbf_gbpcnf_src");
                e.Property(x => x.Time).HasColumnName("time");
            });

            modelBuilder.Entity<DbFirstGbPcnfCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_gbpcnf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_gbpcnf_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_gbpcnf_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, s.service_region AS service_region" +
                    " FROM dbf_gbpcnf_src s GROUP BY time_bucket('1 hour'::interval, s.time), s.service_region");
                e.Property(x => x.ServiceRegion).HasColumnName("service_region");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_GroupByColumn_ParentColumnNotFound_EmitsWithArg()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        // Arrange
        using DbFirstGbPcnfContext context = new();
        IEntityType entityType = GetEntityType<DbFirstGbPcnfCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstGbPcnfCa.ServiceRegion))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(GroupByColumnAttribute));
        Assert.NotNull(attr);
        Assert.NotEmpty(attr.Arguments);
        Assert.IsType<string>(attr.Arguments[0]);
    }

    #endregion


    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_ParentViewMapped_GetTableName_Null

    private class DbFirstPvmSource { public DateTime Time { get; set; } public double Temperature { get; set; } }
    private class DbFirstPvmCa { public double AvgTemp { get; set; } }

    private class DbFirstPvmContext : DbContext
    {
        public DbSet<DbFirstPvmSource> Sources => Set<DbFirstPvmSource>();
        public DbSet<DbFirstPvmCa> CaViews => Set<DbFirstPvmCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstPvmSource>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_pvm_src_view");
                e.Property(x => x.Temperature).HasColumnName("temperature");
            });

            modelBuilder.Entity<DbFirstPvmCa>(e =>
            {
                e.HasNoKey();
                e.ToView("dbf_pvm_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_pvm_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_pvm_src_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.temperature) AS avg_temp FROM dbf_pvm_src_view s GROUP BY 1");
                e.Property(x => x.AvgTemp).HasColumnName("avg_temp");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_ParentViewMapped_GetTableName_Null()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        using DbFirstPvmContext context = new();
        IEntityType entityType = GetEntityType<DbFirstPvmCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstPvmCa.AvgTemp))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region GenerateContinuousAggregatePropertyAttributes_DbFirst_NoViewNoTable_FallsBackToEntityName

    private class DbFirstNvntSource { public DateTime Time { get; set; } public double Value { get; set; } }
    private class DbFirstNvntCa { public double AvgValue { get; set; } }

    private class DbFirstNvntContext : DbContext
    {
        public DbSet<DbFirstNvntSource> Sources => Set<DbFirstNvntSource>();
        public DbSet<DbFirstNvntCa> CaViews => Set<DbFirstNvntCa>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbFirstNvntSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("dbf_nvnt_src");
                e.Property(x => x.Value).HasColumnName("value");
            });

            modelBuilder.Entity<DbFirstNvntCa>(e =>
            {
                e.HasNoKey();
                e.ToSqlQuery("SELECT bucket, avg_value FROM dbf_nvnt_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "dbf_nvnt_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "dbf_nvnt_src");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour'::interval, s.time) AS bucket, avg(s.value) AS avg_value FROM dbf_nvnt_src s GROUP BY 1");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void GenerateContinuousAggregatePropertyAttributes_DbFirst_NoViewNoTable_FallsBackToEntityName()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = CreateGenerator(scaffoldMode: true);
        generator.ScaffoldDataAnnotationsMode = true;

        // Arrange
        using DbFirstNvntContext context = new();
        IEntityType entityType = GetEntityType<DbFirstNvntCa>(context);
        IProperty property = entityType.FindProperty(nameof(DbFirstNvntCa.AvgValue))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.NotNull(result);
    }

    #endregion
}
#pragma warning restore EF1001

#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators.AnnotationRenderers;

/// <summary>
/// Tests for <c>ContinuousAggregateAnnotationRenderer</c> exercised through the public
/// <see cref="TimescaleDbAnnotationCodeGenerator"/> surface.
/// </summary>
public class ContinuousAggregateAnnotationRendererTests
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

    private const string StandardViewDef =
        "SELECT time_bucket('01:00:00'::interval, api_log.\"time\") AS bucket," +
        " api_log.service_name AS service_name," +
        " avg(api_log.duration_ms) AS avg_duration_ms" +
        " FROM api_log" +
        " GROUP BY time_bucket('01:00:00'::interval, api_log.\"time\"), api_log.service_name";

    private class ApiLogEntity
    {
        public DateTime Time { get; set; }
        public double DurationMs { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class HourlyStatsEntity
    {
        public DateTime Bucket { get; set; }
        public double AvgDurationMs { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class CaTestContext : DbContext
    {
        public DbSet<ApiLogEntity> ApiLogs => Set<ApiLogEntity>();
        public DbSet<HourlyStatsEntity> HourlyStats => Set<HourlyStatsEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiLogEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("api_log");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.DurationMs).HasColumnName("duration_ms");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });

            modelBuilder.Entity<HourlyStatsEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("hourly_stats");
                e.Property(x => x.AvgDurationMs).HasColumnName("avg_duration_ms");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });
        }
    }

    // ── GenerateFluentApiCalls ─────────────────────────────────────────────

    #region GenerateFluentApiCalls_Returns_Empty_When_NoMaterializedViewName

    private class NoAnnotationEntity { public DateTime Ts { get; set; } }

    private class NoAnnotationContext : DbContext
    {
        public DbSet<NoAnnotationEntity> Items => Set<NoAnnotationEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<NoAnnotationEntity>(e => { e.HasNoKey(); e.ToTable("ca_no_ann"); });
    }

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_NoMaterializedViewName()
    {
        // Arrange
        using NoAnnotationContext context = new();
        IEntityType entityType = GetEntityType<NoAnnotationEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations();

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)));
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_ViewDefinitionUnparseable

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_ViewDefinitionUnparseable()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, "UNPARSEABLE SQL"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)));
    }

    #endregion

    #region GenerateFluentApiCalls_Minimal_Returns_IsContinuousAggregate

    [Fact]
    public void GenerateFluentApiCalls_Minimal_Returns_IsContinuousAggregate()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? call = result.FirstOrDefault(f =>
            CollectMethodChain(f).Contains(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)));
        Assert.NotNull(call);
        Assert.Equal(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate), call.Method);
    }

    #endregion

    #region GenerateFluentApiCalls_HumanizesInterval

    [Fact]
    public void GenerateFluentApiCalls_HumanizesInterval()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? call = result.First(f =>
            CollectMethodChain(f).Contains(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)));

        Assert.Equal("1 hour", call.Arguments[2]);
    }

    #endregion

    #region GenerateFluentApiCalls_Includes_MaterializedOnly

    [Fact]
    public void GenerateFluentApiCalls_Includes_MaterializedOnly()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("MaterializedOnly"));
    }

    #endregion

    #region GenerateFluentApiCalls_Includes_AddAggregateFunction

    [Fact]
    public void GenerateFluentApiCalls_Includes_AddAggregateFunction()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("AddAggregateFunction"));
    }

    #endregion

    #region GenerateFluentApiCalls_Includes_AddGroupByColumn

    [Fact]
    public void GenerateFluentApiCalls_Includes_AddGroupByColumn()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("AddGroupByColumn"));
    }

    #endregion

    #region GenerateFluentApiCalls_Includes_Where

    [Fact]
    public void GenerateFluentApiCalls_Includes_Where()
    {
        // Arrange
        const string viewDefWithWhere =
            "SELECT time_bucket('01:00:00'::interval, api_log.\"time\") AS bucket," +
            " avg(api_log.duration_ms) AS avg_duration_ms" +
            " FROM api_log" +
            " WHERE api_log.service_name = 'payments'" +
            " GROUP BY time_bucket('01:00:00'::interval, api_log.\"time\")";

        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDefWithWhere));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("Where"));
    }

    #endregion

    #region GenerateFluentApiCalls_ConsumesAllCaAnnotations

    [Fact]
    public void GenerateFluentApiCalls_ConsumesAllCaAnnotations()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ChunkInterval, "7 days"),
            (ContinuousAggregateAnnotations.WithNoData, false),
            (ContinuousAggregateAnnotations.CreateGroupIndexes, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:", StringComparison.Ordinal));
    }

    #endregion

    #region GenerateFluentApiCalls_LeavesAnnotationsUnconsumed_WhenViewDefUnparseable

    [Fact]
    public void GenerateFluentApiCalls_LeavesAnnotationsUnconsumed_WhenViewDefUnparseable()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, "UNPARSEABLE SQL"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("IsContinuousAggregate"));
        Assert.Contains(ContinuousAggregateAnnotations.MaterializedViewName, annotations.Keys);
        Assert.Contains(ContinuousAggregateAnnotations.ParentName, annotations.Keys);
        Assert.Contains(ContinuousAggregateAnnotations.ViewDefinition, annotations.Keys);
    }

    #endregion

    // ── GenerateDataAnnotationAttributes ──────────────────────────────────

    #region GenerateDataAnnotationAttributes_Returns_BasicAttribute

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_BasicAttribute()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(attr);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_HumanizesInterval

    [Fact]
    public void GenerateDataAnnotationAttributes_HumanizesInterval()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(TimeBucketAttribute));
        Assert.NotNull(attr);
        Assert.Equal("1 hour", attr.Arguments[0]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_TimeBucketAttribute

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_TimeBucketAttribute()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? timeBucketAttr = result.FirstOrDefault(a => a.Type == typeof(TimeBucketAttribute));
        Assert.NotNull(timeBucketAttr);
        Assert.DoesNotContain(result, a =>
            a.Type == typeof(ContinuousAggregateAttribute) &&
            a.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.TimeBucketWidth)));
    }

    #endregion

    #region GenerateFluentApiCalls_MaterializedOnly_HasExplicitTrueArg

    [Fact]
    public void GenerateFluentApiCalls_MaterializedOnly_HasExplicitTrueArg()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .Select(f =>
            {
                for (MethodCallCodeFragment? current = f; current != null; current = current.ChainedCall)
                {
                    if (current.Method == "MaterializedOnly") return current;
                }
                return null;
            })
            .FirstOrDefault(f => f != null);
        Assert.NotNull(fragment);
        Assert.Equal(true, fragment.Arguments.FirstOrDefault());
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Includes_WhereClause

    [Fact]
    public void GenerateDataAnnotationAttributes_Includes_WhereClause()
    {
        // Arrange
        const string viewDefWithWhere =
            "SELECT time_bucket('01:00:00'::interval, api_log.\"time\") AS bucket," +
            " avg(api_log.duration_ms) AS avg_duration_ms" +
            " FROM api_log" +
            " WHERE api_log.service_name = 'payments'" +
            " GROUP BY time_bucket('01:00:00'::interval, api_log.\"time\")";

        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDefWithWhere));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(attr);
        Assert.True(attr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.Where)));
        Assert.NotNull(attr.NamedArguments[nameof(ContinuousAggregateAttribute.Where)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_ConsumesAllCaAnnotations

    [Fact]
    public void GenerateDataAnnotationAttributes_ConsumesAllCaAnnotations()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ChunkInterval, "7 days"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:", StringComparison.Ordinal));
    }

    #endregion

    // ── DA mode suppression ───────────────────────────────────────────────────

    #region GenerateFluentApiCalls_InDataAnnotationsMode_DoesNotEmitCaFragments

    [Fact]
    public void GenerateFluentApiCalls_InDataAnnotationsMode_DoesNotEmitCaFragments()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        Assert.DoesNotContain(result, f => f.Method == "AddAggregateFunction");
        Assert.DoesNotContain(result, f => f.Method == "MaterializedOnly");
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:", StringComparison.Ordinal));
    }

    #endregion

    // ── Property-level [Aggregate] attribute ─────────────────────────────────

    private class AggSourceEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class AggCaEntity
    {
        public double AvgValue { get; set; }
        public long TradeCount { get; set; }
    }

    private class AggTestContext : DbContext
    {
        public DbSet<AggSourceEntity> Sources => Set<AggSourceEntity>();
        public DbSet<AggCaEntity> CaEntities => Set<AggCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AggSourceEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("agg_source");
                e.Property(x => x.Value).HasColumnName("value");
            });

            modelBuilder.Entity<AggCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("agg_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "agg_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "agg_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "AvgValue:Avg:Value", "TradeCount:Count:*" });
            });
        }
    }

    #region GenerateDataAnnotationAttributes_Property_Returns_AggregateAttribute

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_Returns_AggregateAttribute()
    {
        // Arrange
        using AggTestContext context = new();
        IEntityType entityType = GetEntityType<AggCaEntity>(context);
        IProperty property = entityType.FindProperty(nameof(AggCaEntity.AvgValue))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Avg, attr.Arguments[0]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Property_CountStar_UsesWildcardSourceColumn

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_CountStar_UsesWildcardSourceColumn()
    {
        // Arrange
        using AggTestContext context = new();
        IEntityType entityType = GetEntityType<AggCaEntity>(context);
        IProperty property = entityType.FindProperty(nameof(AggCaEntity.TradeCount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Count, attr.Arguments[0]);
        Assert.Equal("*", attr.Arguments[1]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Property_NoAggregateForNonCaProperty

    private class PlainEntity { public int Id { get; set; } }

    private class PlainContext : DbContext
    {
        public DbSet<PlainEntity> Items => Set<PlainEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder m)
            => m.Entity<PlainEntity>(e => { e.HasKey(x => x.Id); e.ToTable("plain_agg_test"); });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_NoAggregateForNonCaProperty()
    {
        // Arrange
        using PlainContext context = new();
        IEntityType entityType = GetEntityType<PlainEntity>(context);
        IProperty property = entityType.FindProperty(nameof(PlainEntity.Id))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(AggregateAttribute));
    }

    #endregion

    // ── Property-level [Aggregate] — scaffold path (no AggregateFunctions, uses ViewDefinition) ──

    private class ScaffoldSourceEntity
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
    }

    private class ScaffoldCaEntity
    {
        public double MaxPrice { get; set; }
        public long TradeCount { get; set; }
    }

    private class ScaffoldAggTestContext : DbContext
    {
        public DbSet<ScaffoldSourceEntity> Sources => Set<ScaffoldSourceEntity>();
        public DbSet<ScaffoldCaEntity> CaViews => Set<ScaffoldCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScaffoldSourceEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("scaffold_source");
                e.Property(x => x.Price).HasColumnName("price");
            });

            modelBuilder.Entity<ScaffoldCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("scaffold_ca", "public");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "scaffold_ca");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "scaffold_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
                    " max(s.price) AS max_price," +
                    " count(*) AS trade_count" +
                    " FROM scaffold_source s GROUP BY 1");
                e.Property(x => x.MaxPrice).HasColumnName("max_price");
                e.Property(x => x.TradeCount).HasColumnName("trade_count");
            });
        }
    }

    #region GenerateDataAnnotationAttributes_Property_ScaffoldPath_UsesViewDefinition

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_ScaffoldPath_UsesViewDefinition()
    {
        // Arrange
        using ScaffoldAggTestContext context = new();
        IEntityType entityType = GetEntityType<ScaffoldCaEntity>(context);
        IProperty property = entityType.FindProperty(nameof(ScaffoldCaEntity.MaxPrice))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Max, attr.Arguments[0]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Property_ScaffoldPath_CountStar

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_ScaffoldPath_CountStar()
    {
        // Arrange
        using ScaffoldAggTestContext context = new();
        IEntityType entityType = GetEntityType<ScaffoldCaEntity>(context);
        IProperty property = entityType.FindProperty(nameof(ScaffoldCaEntity.TradeCount))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(AggregateAttribute));
        Assert.NotNull(attr);
        Assert.Equal(EAggregateFunction.Count, attr.Arguments[0]);
        Assert.Equal("*", attr.Arguments[1]);
    }

    #endregion

    // ── Reporter warnings and graceful degradation ─────────────────────────

    private sealed class RecordingReporter : IOperationReporter
    {
        public List<string> Warnings { get; } = [];
        public void WriteError(string message) { }
        public void WriteWarning(string message) => Warnings.Add(message);
        public void WriteInformation(string message) { }
        public void WriteVerbose(string message) { }
    }

    private static (TimescaleDbAnnotationCodeGenerator Generator, RecordingReporter Reporter) CreateGeneratorWithReporter()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        ServiceProvider sp = services.BuildServiceProvider();
        RecordingReporter reporter = new();
        TimescaleDbAnnotationCodeGenerator generator = new(
            sp.GetRequiredService<AnnotationCodeGeneratorDependencies>(), reporter)
        {
            ScaffoldMode = true,
        };
        return (generator, reporter);
    }

    #region GenerateFluentApiCalls_ReportsWarning_WhenViewDefUnparseable

    [Fact]
    public void GenerateFluentApiCalls_ReportsWarning_WhenViewDefUnparseable()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, "UNPARSEABLE SQL"));

        (TimescaleDbAnnotationCodeGenerator generator, RecordingReporter reporter) = CreateGeneratorWithReporter();

        // Act
        generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        string warning = Assert.Single(reporter.Warnings);
        Assert.Contains("hourly_stats", warning);
    }

    #endregion

    #region ConsumeFeatureAnnotations_ReportsWarning_And_Keeps_Annotations_WhenUnparseable

    [Fact]
    public void ConsumeFeatureAnnotations_ReportsWarning_And_Keeps_Annotations_WhenUnparseable()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ViewDefinition, "UNPARSEABLE SQL"));

        (TimescaleDbAnnotationCodeGenerator generator, RecordingReporter reporter) = CreateGeneratorWithReporter();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Single(reporter.Warnings);
        Assert.Contains(ContinuousAggregateAnnotations.ViewDefinition, annotations.Keys);
    }

    #endregion

    #region ConsumeFeatureAnnotations_Consumes_All_WhenParseable

    [Fact]
    public void ConsumeFeatureAnnotations_Consumes_All_WhenParseable()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        (TimescaleDbAnnotationCodeGenerator generator, RecordingReporter reporter) = CreateGeneratorWithReporter();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("IsContinuousAggregate"));
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:", StringComparison.Ordinal));
        Assert.Empty(reporter.Warnings);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_ReportsWarning_ForUnrepresentableGroupBy

    private class RawGroupByCaEntity
    {
        public DateTime Bucket { get; set; }
        public double AvgDurationMs { get; set; }
    }

    private class RawGroupByContext : DbContext
    {
        public DbSet<ApiLogEntity> ApiLogs => Set<ApiLogEntity>();
        public DbSet<RawGroupByCaEntity> Stats => Set<RawGroupByCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiLogEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("api_log");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.DurationMs).HasColumnName("duration_ms");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });

            modelBuilder.Entity<RawGroupByCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("raw_group_by_stats");
                e.Property(x => x.AvgDurationMs).HasColumnName("avg_duration_ms");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_ReportsWarning_ForUnrepresentableGroupBy()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, api_log.\"time\") AS bucket," +
            " avg(api_log.duration_ms) AS avg_duration_ms" +
            " FROM api_log" +
            " GROUP BY time_bucket('01:00:00'::interval, api_log.\"time\"), EXTRACT(HOUR FROM api_log.\"time\")";

        using RawGroupByContext context = new();
        IEntityType entityType = GetEntityType<RawGroupByCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "raw_group_by_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        (TimescaleDbAnnotationCodeGenerator generator, RecordingReporter reporter) = CreateGeneratorWithReporter();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        generator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        string warning = Assert.Single(reporter.Warnings);
        Assert.Contains("EXTRACT", warning);
        Assert.Contains("raw_group_by_stats", warning);
    }

    #endregion

    // ── Chunk-interval 10x-parent elision ──────────────────────────────────

    #region ChunkInterval_Elided_WhenEqualTo10xParentDefault

    private static Dictionary<string, IAnnotation> ElisionAnnotations(string chunkInterval) => Annotations(
        (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
        (ContinuousAggregateAnnotations.ParentName, "api_log"),
        (ContinuousAggregateAnnotations.ChunkInterval, chunkInterval),
        (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

    [Fact]
    public void ChunkInterval_Elided_WhenEqualTo10xParentDefault()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, ElisionAnnotations("70 days"));

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithChunkInterval"));
    }

    #endregion

    #region ChunkInterval_Kept_WhenNot10xParentDefault

    [Fact]
    public void ChunkInterval_Kept_WhenNot10xParentDefault()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, ElisionAnnotations("30 days"));

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("WithChunkInterval"));
    }

    #endregion

    #region ChunkInterval_Kept_ForCalendarUnits

    [Fact]
    public void ChunkInterval_Kept_ForCalendarUnits()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, ElisionAnnotations("1 month"));

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("WithChunkInterval"));
    }

    #endregion

    #region ChunkInterval_Elided_InDataAnnotationsMode

    [Fact]
    public void ChunkInterval_Elided_InDataAnnotationsMode()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, ElisionAnnotations("70 days"));

        // Assert
        AttributeCodeFragment attr = Assert.Single(result, a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.DoesNotContain(nameof(ContinuousAggregateAttribute.ChunkInterval), attr.NamedArguments.Keys);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_MaterializedOnly_True_Sets_NamedArg

    [Fact]
    public void GenerateDataAnnotationAttributes_MaterializedOnly_True_Sets_NamedArg()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.MaterializedOnly, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(attr);
        Assert.True(attr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.MaterializedOnly)));
        Assert.Equal(true, attr.NamedArguments[nameof(ContinuousAggregateAttribute.MaterializedOnly)]);
    }

    #endregion

    // ── First/Last aggregates and parent resolution ────────────────────────

    #region GenerateFluentApiCalls_Includes_FirstLast_Aggregates

    private class FirstLastCaEntity
    {
        public DateTime Bucket { get; set; }
        public double FirstDurationMs { get; set; }
        public double LastDurationMs { get; set; }
    }

    private class FirstLastContext : DbContext
    {
        public DbSet<ApiLogEntity> ApiLogs => Set<ApiLogEntity>();
        public DbSet<FirstLastCaEntity> Stats => Set<FirstLastCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiLogEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("api_log");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.DurationMs).HasColumnName("duration_ms");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });

            modelBuilder.Entity<FirstLastCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("first_last_stats");
                e.Property(x => x.FirstDurationMs).HasColumnName("first_duration_ms");
                e.Property(x => x.LastDurationMs).HasColumnName("last_duration_ms");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_Includes_FirstLast_Aggregates()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, api_log.\"time\") AS bucket," +
            " first(api_log.duration_ms, api_log.\"time\") AS first_duration_ms," +
            " last(api_log.duration_ms, api_log.\"time\") AS last_duration_ms" +
            " FROM api_log" +
            " GROUP BY time_bucket('01:00:00'::interval, api_log.\"time\")";

        using FirstLastContext context = new();
        IEntityType entityType = GetEntityType<FirstLastCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "first_last_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => CollectMethodChain(f).Contains("IsContinuousAggregate"));
        List<EAggregateFunction> functions = [];
        for (MethodCallCodeFragment? current = root; current != null; current = current.ChainedCall)
        {
            if (current.Method == "AddAggregateFunction" && current.Arguments[2] is EAggregateFunction f)
            {
                functions.Add(f);
            }
        }

        Assert.Contains(EAggregateFunction.First, functions);
        Assert.Contains(EAggregateFunction.Last, functions);
    }

    #endregion

    #region ParentResolution_Matches_ClrName_WhenTableNameDiffers

    [Fact]
    public void ParentResolution_Matches_ClrName_WhenTableNameDiffers()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, nameof(ApiLogEntity)),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == "IsContinuousAggregate");

        // The parent resolved by CLR name renders as nameof(ApiLogEntity), not a raw string.
        Assert.IsType<CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.NameOfCodeFragment>(root.Arguments[1]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Property_ComplexType_DoesNotThrow

    private class ComplexOwnerEntity
    {
        public int Id { get; set; }
        public AddressComplex Address { get; set; } = null!;
    }

    private class AddressComplex
    {
        public string City { get; set; } = "";
    }

    private class ComplexTypeContext : DbContext
    {
        public DbSet<ComplexOwnerEntity> Owners => Set<ComplexOwnerEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ComplexOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.ToTable("complex_owner");
                e.ComplexProperty(x => x.Address);
            });
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_ComplexType_DoesNotThrow()
    {
        // Arrange
        using ComplexTypeContext context = new();
        IEntityType entityType = GetEntityType<ComplexOwnerEntity>(context);
        IProperty cityProperty = entityType.GetComplexProperties().Single().ComplexType.GetProperties()
            .Single(p => p.Name == nameof(AddressComplex.City));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(cityProperty, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region GenerateFluentApiCalls_WithNoData_True_Chains_WithNoData_Method

    [Fact]
    public void GenerateFluentApiCalls_WithNoData_True_Chains_WithNoData_Method()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.WithNoData, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .Select(f =>
            {
                for (MethodCallCodeFragment? current = f; current != null; current = current.ChainedCall)
                {
                    if (current.Method == "WithNoData") return current;
                }
                return null;
            })
            .FirstOrDefault(f => f != null);
        Assert.NotNull(fragment);
        Assert.Equal(true, fragment.Arguments.FirstOrDefault());
    }

    #endregion

    #region GenerateFluentApiCalls_CreateGroupIndexes_False_Chains_CreateGroupIndexes_Method

    [Fact]
    public void GenerateFluentApiCalls_CreateGroupIndexes_False_Chains_CreateGroupIndexes_Method()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.CreateGroupIndexes, false),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .Select(f =>
            {
                for (MethodCallCodeFragment? current = f; current != null; current = current.ChainedCall)
                {
                    if (current.Method == "CreateGroupIndexes") return current;
                }
                return null;
            })
            .FirstOrDefault(f => f != null);
        Assert.NotNull(fragment);
        Assert.Equal(false, fragment.Arguments.FirstOrDefault());
    }

    #endregion

    #region GenerateDataAnnotationAttributes_WithNoData_True_Sets_NamedArg

    [Fact]
    public void GenerateDataAnnotationAttributes_WithNoData_True_Sets_NamedArg()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.WithNoData, true),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(attr);
        Assert.True(attr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.WithNoData)));
        Assert.Equal(true, attr.NamedArguments[nameof(ContinuousAggregateAttribute.WithNoData)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CreateGroupIndexes_False_Sets_NamedArg

    [Fact]
    public void GenerateDataAnnotationAttributes_CreateGroupIndexes_False_Sets_NamedArg()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.CreateGroupIndexes, false),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(attr);
        Assert.True(attr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.CreateGroupIndexes)));
        Assert.Equal(false, attr.NamedArguments[nameof(ContinuousAggregateAttribute.CreateGroupIndexes)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Property_Emits_GroupByColumnAttribute

    private class GroupByPropCaEntity
    {
        public DateTime Bucket { get; set; }
        public double AvgDurationMs { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class GroupByPropContext : DbContext
    {
        public DbSet<ApiLogEntity> ApiLogs => Set<ApiLogEntity>();
        public DbSet<GroupByPropCaEntity> Stats => Set<GroupByPropCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApiLogEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("api_log");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.DurationMs).HasColumnName("duration_ms");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });

            modelBuilder.Entity<GroupByPropCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("group_by_prop_stats");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "group_by_prop_stats");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "api_log");
                e.HasAnnotation(ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef);
                e.Property(x => x.AvgDurationMs).HasColumnName("avg_duration_ms");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_Emits_GroupByColumnAttribute()
    {
        // Arrange
        using GroupByPropContext context = new();
        IEntityType entityType = GetEntityType<GroupByPropCaEntity>(context);
        IProperty property = entityType.FindProperty(nameof(GroupByPropCaEntity.ServiceName))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        AttributeCodeFragment attr = Assert.Single(result, a => a.Type == typeof(GroupByColumnAttribute));

        Assert.Empty(attr.Arguments);
    }

    #endregion

    // ── TimescaleDbAnnotationCodeGenerator scaffold-mode guard ─────────────

    #region GenerateFluentApiCalls_NonScaffoldMode_Returns_No_TimescaleFragments

    [Fact]
    public void GenerateFluentApiCalls_NonScaffoldMode_Returns_No_TimescaleFragments()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
            .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();

        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("IsContinuousAggregate"));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_NonScaffoldMode_Returns_No_TimescaleAttributes

    [Fact]
    public void GenerateDataAnnotationAttributes_NonScaffoldMode_Returns_No_TimescaleAttributes()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
            .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();

        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ContinuousAggregateAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Entity_CacheHit_Returns_Identical_Result

    [Fact]
    public void GenerateDataAnnotationAttributes_Entity_CacheHit_Returns_Identical_Result()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();

        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> first = generator.GenerateDataAnnotationAttributes(entityType, annotations);

        Dictionary<string, IAnnotation> annotations2 = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ParentName, "api_log"),
            (ContinuousAggregateAnnotations.ViewDefinition, StandardViewDef));

        IReadOnlyList<AttributeCodeFragment> second = generator.GenerateDataAnnotationAttributes(entityType, annotations2);

        // Assert
        AttributeCodeFragment? firstCa = first.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        AttributeCodeFragment? secondCa = second.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(firstCa);
        Assert.NotNull(secondCa);
        Assert.Equal(firstCa.Type, secondCa.Type);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Property_CacheHit_Returns_Identical_Result

    [Fact]
    public void GenerateDataAnnotationAttributes_Property_CacheHit_Returns_Identical_Result()
    {
        // Arrange
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();

        using ScaffoldAggTestContext context = new();
        IEntityType entityType = GetEntityType<ScaffoldCaEntity>(context);
        IProperty property = entityType.FindProperty(nameof(ScaffoldCaEntity.MaxPrice))!;

        // Act
        IReadOnlyList<AttributeCodeFragment> first = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());
        IReadOnlyList<AttributeCodeFragment> second = generator
            .GenerateDataAnnotationAttributes(property, new Dictionary<string, IAnnotation>());

        // Assert
        Assert.Equal(
            first.FirstOrDefault(a => a.Type == typeof(AggregateAttribute))?.Type,
            second.FirstOrDefault(a => a.Type == typeof(AggregateAttribute))?.Type);
    }

    #endregion
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregate;
#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.ContinuousAggregate;

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

        Assert.IsType<CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.NameOfCodeFragment>(root.Arguments[1]);
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

    // ── ViewDefinition parsing edge cases ─────────────────────────────────────

    #region Should_Return_Empty_When_ViewDefinition_Has_No_TimeBucket

    private class NoTimeBucketCaEntity
    {
        public double Value { get; set; }
    }

    private class NoTimeBucketContext : DbContext
    {
        public DbSet<NoTimeBucketCaEntity> Stats => Set<NoTimeBucketCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoTimeBucketCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("no_time_bucket_stats");
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_ViewDefinition_Has_No_TimeBucket()
    {
        // Arrange
        const string viewDefNoTimeBucket =
            "SELECT value FROM raw_data GROUP BY value";

        using NoTimeBucketContext context = new();
        IEntityType entityType = GetEntityType<NoTimeBucketCaEntity>(context);
        Dictionary<string, IAnnotation> fluentAnnotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "no_time_bucket_stats"),
            (ContinuousAggregateAnnotations.ParentName, "raw_data"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDefNoTimeBucket));

        Dictionary<string, IAnnotation> daAnnotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "no_time_bucket_stats"),
            (ContinuousAggregateAnnotations.ParentName, "raw_data"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDefNoTimeBucket));

        IAnnotationCodeGenerator generator = CreateAnnotationCodeGenerator();

        // Act
        IReadOnlyList<MethodCallCodeFragment> fluentResult = generator.GenerateFluentApiCalls(entityType, fluentAnnotations);
        IReadOnlyList<AttributeCodeFragment> daResult = generator.GenerateDataAnnotationAttributes(entityType, daAnnotations);

        // Assert
        Assert.DoesNotContain(fluentResult, f => CollectMethodChain(f).Contains(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)));
        Assert.DoesNotContain(daResult, a => a.Type == typeof(ContinuousAggregateAttribute));
    }

    #endregion

    #region Should_Return_Empty_When_TimeBucketSourceColumn_Is_Null

    private class NoSourceColumnCaEntity
    {
        public double Value { get; set; }
    }

    private class NoSourceColumnContext : DbContext
    {
        public DbSet<NoSourceColumnCaEntity> Stats => Set<NoSourceColumnCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoSourceColumnCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("no_source_col_stats");
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_TimeBucketSourceColumn_Is_Null()
    {
        // Arrange
        const string viewDefNoSourceCol =
            "SELECT time_bucket('1 hour') AS bucket, avg(value) AS avg_value FROM raw_data GROUP BY 1";

        using NoSourceColumnContext context = new();
        IEntityType entityType = GetEntityType<NoSourceColumnCaEntity>(context);
        Dictionary<string, IAnnotation> fluentAnnotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "no_source_col_stats"),
            (ContinuousAggregateAnnotations.ParentName, "raw_data"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDefNoSourceCol));

        Dictionary<string, IAnnotation> daAnnotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "no_source_col_stats"),
            (ContinuousAggregateAnnotations.ParentName, "raw_data"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDefNoSourceCol));

        IAnnotationCodeGenerator generator = CreateAnnotationCodeGenerator();

        // Act
        IReadOnlyList<MethodCallCodeFragment> fluentResult = generator.GenerateFluentApiCalls(entityType, fluentAnnotations);
        IReadOnlyList<AttributeCodeFragment> daResult = generator.GenerateDataAnnotationAttributes(entityType, daAnnotations);

        // Assert
        Assert.DoesNotContain(fluentResult, f => CollectMethodChain(f).Contains(nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate)));
        Assert.DoesNotContain(daResult, a => a.Type == typeof(ContinuousAggregateAttribute));
    }

    #endregion

    // ── ResolveParentColumnArg — unresolvable parent ───────────────────────────

    #region Should_Use_Raw_ColumnName_When_Parent_EntityType_Not_Resolved

    private class UnresolvableParentCaEntity
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class UnresolvableParentContext : DbContext
    {
        public DbSet<UnresolvableParentCaEntity> Stats => Set<UnresolvableParentCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnresolvableParentCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("unresolvable_parent_stats");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Use_Raw_ColumnName_When_Parent_EntityType_Not_Resolved()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, src.\"time\") AS bucket," +
            " avg(src.value) AS avg_value" +
            " FROM unknown_source src" +
            " GROUP BY time_bucket('01:00:00'::interval, src.\"time\")";

        using UnresolvableParentContext context = new();
        IEntityType entityType = GetEntityType<UnresolvableParentCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "unresolvable_parent_stats"),
            (ContinuousAggregateAnnotations.ParentName, "unknown_source"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));

        Assert.IsType<string>(root.Arguments[1]);
        Assert.IsType<string>(root.Arguments[3]);
        MethodCallCodeFragment? addAgg = null;
        for (MethodCallCodeFragment? current = root; current != null; current = current.ChainedCall)
        {
            if (current.Method == "AddAggregateFunction") { addAgg = current; break; }
        }
        Assert.NotNull(addAgg);
        Assert.IsType<string>(addAgg.Arguments[1]);
    }

    #endregion

    // ── COUNT(*) wildcard in fluent API ───────────────────────────────────────

    #region Should_Use_Wildcard_String_When_AggregateSourceColumn_Is_Star

    private class CountStarSourceEntity
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
    }

    private class CountStarCaEntity
    {
        public DateTime Bucket { get; set; }
        public long TradeCount { get; set; }
    }

    private class CountStarFluentContext : DbContext
    {
        public DbSet<CountStarSourceEntity> Sources => Set<CountStarSourceEntity>();
        public DbSet<CountStarCaEntity> Stats => Set<CountStarCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CountStarSourceEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("count_star_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Price).HasColumnName("price");
            });

            modelBuilder.Entity<CountStarCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("count_star_ca_stats");
                e.Property(x => x.TradeCount).HasColumnName("trade_count");
            });
        }
    }

    [Fact]
    public void Should_Use_Wildcard_String_When_AggregateSourceColumn_Is_Star()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " count(*) AS trade_count" +
            " FROM count_star_source s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using CountStarFluentContext context = new();
        IEntityType entityType = GetEntityType<CountStarCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "count_star_ca_stats"),
            (ContinuousAggregateAnnotations.ParentName, "count_star_source"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));

        MethodCallCodeFragment? addAgg = null;
        for (MethodCallCodeFragment? current = root; current != null; current = current.ChainedCall)
        {
            if (current.Method == "AddAggregateFunction") { addAgg = current; break; }
        }
        Assert.NotNull(addAgg);
        Assert.Equal("*", addAgg.Arguments[1]);
        Assert.IsType<string>(addAgg.Arguments[1]);
        Assert.Equal(EAggregateFunction.Count, addAgg.Arguments[2]);
    }

    #endregion

    // ── MaterializedOnly, WhereClause, ChunkInterval, GroupByColumn, raw alias ──

    #region Should_Chain_MaterializedOnly_When_MaterializedOnly_Is_True

    private class MaterializedOnlyParentEntity4
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class MaterializedOnlyCaEntity4
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnlyContext4 : DbContext
    {
        public DbSet<MaterializedOnlyParentEntity4> Sources => Set<MaterializedOnlyParentEntity4>();
        public DbSet<MaterializedOnlyCaEntity4> Stats => Set<MaterializedOnlyCaEntity4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaterializedOnlyParentEntity4>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("mat_only_src4");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<MaterializedOnlyCaEntity4>(e =>
            {
                e.HasNoKey();
                e.ToView("mat_only_stats4");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Chain_MaterializedOnly_When_MaterializedOnly_Is_True()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM mat_only_src4 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using MaterializedOnlyContext4 context = new();
        IEntityType entityType = GetEntityType<MaterializedOnlyCaEntity4>(context);
        Dictionary<string, IAnnotation> fluentAnnotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "mat_only_stats4"),
            (ContinuousAggregateAnnotations.ParentName, "mat_only_src4"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (ContinuousAggregateAnnotations.MaterializedOnly, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, fluentAnnotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.Contains("MaterializedOnly", chain);
    }

    #endregion

    #region Should_Chain_WhereClause_When_WhereClause_Present_In_FluentApi

    private class WhereClauseParentEntity5
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
        public string? Region { get; set; }
    }

    private class WhereClauseCaEntity5
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WhereClauseContext5 : DbContext
    {
        public DbSet<WhereClauseParentEntity5> Sources => Set<WhereClauseParentEntity5>();
        public DbSet<WhereClauseCaEntity5> Stats => Set<WhereClauseCaEntity5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WhereClauseParentEntity5>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("where_clause_src5");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<WhereClauseCaEntity5>(e =>
            {
                e.HasNoKey();
                e.ToView("where_clause_stats5");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Chain_WhereClause_When_WhereClause_Present_In_FluentApi()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM where_clause_src5 s" +
            " WHERE s.value > 0" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using WhereClauseContext5 context = new();
        IEntityType entityType = GetEntityType<WhereClauseCaEntity5>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "where_clause_stats5"),
            (ContinuousAggregateAnnotations.ParentName, "where_clause_src5"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.Contains("Where", chain);
    }

    #endregion

    #region Should_Chain_ChunkInterval_When_Not_Derived_Default

    private class ChunkIntervalParentEntity6
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ChunkIntervalCaEntity6
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ChunkIntervalContext6 : DbContext
    {
        public DbSet<ChunkIntervalParentEntity6> Sources => Set<ChunkIntervalParentEntity6>();
        public DbSet<ChunkIntervalCaEntity6> Stats => Set<ChunkIntervalCaEntity6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalParentEntity6>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("chunk_interval_src6");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<ChunkIntervalCaEntity6>(e =>
            {
                e.HasNoKey();
                e.ToView("chunk_interval_stats6");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Chain_ChunkInterval_When_Not_Derived_Default()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM chunk_interval_src6 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using ChunkIntervalContext6 context = new();
        IEntityType entityType = GetEntityType<ChunkIntervalCaEntity6>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "chunk_interval_stats6"),
            (ContinuousAggregateAnnotations.ParentName, "chunk_interval_src6"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (ContinuousAggregateAnnotations.ChunkInterval, "1 month"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.Contains("WithChunkInterval", chain);
    }

    #endregion

    #region Should_Not_Chain_ChunkInterval_When_IsDerivedDefault

    private class DerivedDefaultChunkIntervalParentEntity7
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class DerivedDefaultChunkIntervalCaEntity7
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DerivedDefaultChunkIntervalContext7 : DbContext
    {
        public DbSet<DerivedDefaultChunkIntervalParentEntity7> Sources => Set<DerivedDefaultChunkIntervalParentEntity7>();
        public DbSet<DerivedDefaultChunkIntervalCaEntity7> Stats => Set<DerivedDefaultChunkIntervalCaEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DerivedDefaultChunkIntervalParentEntity7>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("derived_default_src7");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<DerivedDefaultChunkIntervalCaEntity7>(e =>
            {
                e.HasNoKey();
                e.ToView("derived_default_stats7");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Not_Chain_ChunkInterval_When_IsDerivedDefault()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM derived_default_src7 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using DerivedDefaultChunkIntervalContext7 context = new();
        IEntityType entityType = GetEntityType<DerivedDefaultChunkIntervalCaEntity7>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "derived_default_stats7"),
            (ContinuousAggregateAnnotations.ParentName, "derived_default_src7"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (ContinuousAggregateAnnotations.ChunkInterval, "70 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.DoesNotContain("WithChunkInterval", chain);
    }

    #endregion

    #region Should_Chain_AddGroupByColumn_For_GroupBy_Columns

    private class GroupByColParentEntity8
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
        public string Region { get; set; } = "";
    }

    private class GroupByColCaEntity8
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
        public string Region { get; set; } = "";
    }

    private class GroupByColContext8 : DbContext
    {
        public DbSet<GroupByColParentEntity8> Sources => Set<GroupByColParentEntity8>();
        public DbSet<GroupByColCaEntity8> Stats => Set<GroupByColCaEntity8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GroupByColParentEntity8>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("group_by_col_src8");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
                e.Property(x => x.Region).HasColumnName("region");
            });
            modelBuilder.Entity<GroupByColCaEntity8>(e =>
            {
                e.HasNoKey();
                e.ToView("group_by_col_stats8");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
                e.Property(x => x.Region).HasColumnName("region");
            });
        }
    }

    [Fact]
    public void Should_Chain_AddGroupByColumn_For_GroupBy_Columns()
    {
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value," +
            " s.region AS region" +
            " FROM group_by_col_src8 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\"), s.region";

        using GroupByColContext8 context = new();
        IEntityType entityType = GetEntityType<GroupByColCaEntity8>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "group_by_col_stats8"),
            (ContinuousAggregateAnnotations.ParentName, "group_by_col_src8"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.Contains("AddGroupByColumn", chain);
    }

    #endregion

    #region Should_Use_Raw_Alias_When_AggregateAlias_Does_Not_Match_Any_Property

    private class RawAliasParentEntity9
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class RawAliasCaEntity9
    {
        public DateTime Bucket { get; set; }
    }

    private class RawAliasContext9 : DbContext
    {
        public DbSet<RawAliasParentEntity9> Sources => Set<RawAliasParentEntity9>();
        public DbSet<RawAliasCaEntity9> Stats => Set<RawAliasCaEntity9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RawAliasParentEntity9>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("raw_alias_src9");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<RawAliasCaEntity9>(e =>
            {
                e.HasNoKey();
                e.ToView("raw_alias_stats9");
            });
        }
    }

    [Fact]
    public void Should_Use_Raw_Alias_When_AggregateAlias_Does_Not_Match_Any_Property()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS unmapped_alias" +
            " FROM raw_alias_src9 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using RawAliasContext9 context = new();
        IEntityType entityType = GetEntityType<RawAliasCaEntity9>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "raw_alias_stats9"),
            (ContinuousAggregateAnnotations.ParentName, "raw_alias_src9"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        MethodCallCodeFragment? addAgg = null;
        for (MethodCallCodeFragment? cur = root; cur != null; cur = cur.ChainedCall)
        {
            if (cur.Method == "AddAggregateFunction") { addAgg = cur; break; }
        }
        Assert.NotNull(addAgg);
        Assert.IsType<string>(addAgg.Arguments[0]);
        Assert.Equal("unmapped_alias", addAgg.Arguments[0]);
    }

    #endregion

    #region Should_Set_MaterializedOnly_Named_Arg_In_DataAnnotations

    private class MaterializedOnlyDaParentEntity10
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class MaterializedOnlyDaCaEntity10
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MaterializedOnlyDaContext10 : DbContext
    {
        public DbSet<MaterializedOnlyDaParentEntity10> Sources => Set<MaterializedOnlyDaParentEntity10>();
        public DbSet<MaterializedOnlyDaCaEntity10> Stats => Set<MaterializedOnlyDaCaEntity10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MaterializedOnlyDaParentEntity10>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("mat_only_da_src10");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<MaterializedOnlyDaCaEntity10>(e =>
            {
                e.HasNoKey();
                e.ToView("mat_only_da_stats10");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Set_MaterializedOnly_Named_Arg_In_DataAnnotations()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM mat_only_da_src10 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using MaterializedOnlyDaContext10 context = new();
        IEntityType entityType = GetEntityType<MaterializedOnlyDaCaEntity10>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "mat_only_da_stats10"),
            (ContinuousAggregateAnnotations.ParentName, "mat_only_da_src10"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (ContinuousAggregateAnnotations.MaterializedOnly, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.MaterializedOnly)));
    }

    #endregion

    #region Should_Set_Where_Named_Arg_In_DataAnnotations

    private class WhereDaParentEntity11
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class WhereDaCaEntity11
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WhereDaContext11 : DbContext
    {
        public DbSet<WhereDaParentEntity11> Sources => Set<WhereDaParentEntity11>();
        public DbSet<WhereDaCaEntity11> Stats => Set<WhereDaCaEntity11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WhereDaParentEntity11>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("where_da_src11");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<WhereDaCaEntity11>(e =>
            {
                e.HasNoKey();
                e.ToView("where_da_stats11");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Set_Where_Named_Arg_In_DataAnnotations()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM where_da_src11 s" +
            " WHERE s.value > 0" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using WhereDaContext11 context = new();
        IEntityType entityType = GetEntityType<WhereDaCaEntity11>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "where_da_stats11"),
            (ContinuousAggregateAnnotations.ParentName, "where_da_src11"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.Where)));
    }

    #endregion

    #region Should_Set_ChunkInterval_Named_Arg_In_DataAnnotations_When_Not_Derived_Default

    private class ChunkIntervalDaParentEntity12
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ChunkIntervalDaCaEntity12
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ChunkIntervalDaContext12 : DbContext
    {
        public DbSet<ChunkIntervalDaParentEntity12> Sources => Set<ChunkIntervalDaParentEntity12>();
        public DbSet<ChunkIntervalDaCaEntity12> Stats => Set<ChunkIntervalDaCaEntity12>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalDaParentEntity12>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("chunk_da_src12");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<ChunkIntervalDaCaEntity12>(e =>
            {
                e.HasNoKey();
                e.ToView("chunk_da_stats12");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void Should_Set_ChunkInterval_Named_Arg_In_DataAnnotations_When_Not_Derived_Default()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM chunk_da_src12 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using ChunkIntervalDaContext12 context = new();
        IEntityType entityType = GetEntityType<ChunkIntervalDaCaEntity12>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "chunk_da_stats12"),
            (ContinuousAggregateAnnotations.ParentName, "chunk_da_src12"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (ContinuousAggregateAnnotations.ChunkInterval, "1 month"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.ChunkInterval)));
    }

    #endregion

    // ── Default fallbacks: createGroupIndexes and parentName ─────────────────

    #region CreateGroupIndexes_DefaultsToTrue_When_Annotation_Is_Null

    private class NullGiParentEntity13
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class NullGiCaEntity13
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullGiContext13 : DbContext
    {
        public DbSet<NullGiParentEntity13> Sources => Set<NullGiParentEntity13>();
        public DbSet<NullGiCaEntity13> Stats => Set<NullGiCaEntity13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullGiParentEntity13>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("null_gi_src13");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<NullGiCaEntity13>(e =>
            {
                e.HasNoKey();
                e.ToView("null_gi_stats13");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void CreateGroupIndexes_DefaultsToTrue_When_Annotation_Is_Null()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value" +
            " FROM null_gi_src13 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using NullGiContext13 context = new();
        IEntityType entityType = GetEntityType<NullGiCaEntity13>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "null_gi_stats13"),
            (ContinuousAggregateAnnotations.ParentName, "null_gi_src13"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.DoesNotContain("CreateGroupIndexes", chain);
    }

    #endregion

    #region GenerateFluentApiCalls_ParentName_Null_FallsBackTo_MaterializedViewName

    private class NullParentNameCaEntity14
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullParentNameContext14 : DbContext
    {
        public DbSet<NullParentNameCaEntity14> Stats => Set<NullParentNameCaEntity14>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullParentNameCaEntity14>(e =>
            {
                e.HasNoKey();
                e.ToView("null_parent_name_stats14");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_ParentName_Null_FallsBackTo_MaterializedViewName()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.avg_value) AS avg_value" +
            " FROM unknown_parent_src s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using NullParentNameContext14 context = new();
        IEntityType entityType = GetEntityType<NullParentNameCaEntity14>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "null_parent_name_stats14"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        string parentArg = Assert.IsType<string>(root.Arguments[1]);
        Assert.Equal(string.Empty, parentArg);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_ParentName_Null_FallsBackTo_EmptyString

    private class NullParentNameDaCaEntity15
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullParentNameDaContext15 : DbContext
    {
        public DbSet<NullParentNameDaCaEntity15> Stats => Set<NullParentNameDaCaEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullParentNameDaCaEntity15>(e =>
            {
                e.HasNoKey();
                e.ToView("null_parent_name_da_stats15");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_ParentName_Null_FallsBackTo_EmptyString()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.avg_value) AS avg_value" +
            " FROM unknown_parent_da_src s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using NullParentNameDaContext15 context = new();
        IEntityType entityType = GetEntityType<NullParentNameDaCaEntity15>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "null_parent_name_da_stats15"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.ParentName)));
        string parentNameArg = Assert.IsType<string>(caAttr.NamedArguments[nameof(ContinuousAggregateAttribute.ParentName)]);
        Assert.Equal(string.Empty, parentNameArg);
    }

    #endregion

    #region ConsumeFeatureAnnotations_ReportsWarning_WhenViewDefinitionUnparseable

    [Fact]
    public void ConsumeFeatureAnnotations_ReportsWarning_WhenViewDefinitionUnparseable()
    {
        // Arrange
        using CaTestContext context = new();
        IEntityType entityType = GetEntityType<HourlyStatsEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "hourly_stats"),
            (ContinuousAggregateAnnotations.ViewDefinition, "NOT VALID SQL AT ALL"));

        (TimescaleDbAnnotationCodeGenerator generator, RecordingReporter reporter) = CreateGeneratorWithReporter();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        string warning = Assert.Single(reporter.Warnings);
        Assert.Contains("hourly_stats", warning);
        Assert.Contains(ContinuousAggregateAnnotations.MaterializedViewName, annotations.Keys);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_NoGroupByAttribute_WhenGroupByEntryUnresolvable

    private class UnresolvableGroupByParentEntity16
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class UnresolvableGroupByCaEntity16
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class UnresolvableGroupByContext16 : DbContext
    {
        public DbSet<UnresolvableGroupByParentEntity16> Sources => Set<UnresolvableGroupByParentEntity16>();
        public DbSet<UnresolvableGroupByCaEntity16> Stats => Set<UnresolvableGroupByCaEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnresolvableGroupByParentEntity16>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("unresolvable_gb_src16");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
            modelBuilder.Entity<UnresolvableGroupByCaEntity16>(e =>
            {
                e.HasNoKey();
                e.ToView("unresolvable_gb_stats16");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_NoGroupByAttribute_WhenGroupByEntryUnresolvable()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.value) AS avg_value," +
            " s.region AS region" +
            " FROM unresolvable_gb_src16 s" +
            " GROUP BY time_bucket('01:00:00'::interval, s.\"time\"), s.region";

        using UnresolvableGroupByContext16 context = new();
        IEntityType entityType = GetEntityType<UnresolvableGroupByCaEntity16>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "unresolvable_gb_stats16"),
            (ContinuousAggregateAnnotations.ParentName, "unresolvable_gb_src16"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        (TimescaleDbAnnotationCodeGenerator generator, RecordingReporter reporter) = CreateGeneratorWithReporter();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.Single(reporter.Warnings);
        Assert.Contains(result, a => a.Type == typeof(ContinuousAggregateAttribute));
    }

    #endregion

    // ── Fluent API: parentEntityType null AND parentName null → uses materializedViewName ──

    #region GenerateFluentApiCalls_Uses_MaterializedViewName_When_ParentName_And_EntityType_Both_Null

    private class NullParentCaEntity17
    {
        public DateTime Bucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class NullParentContext17 : DbContext
    {
        public DbSet<NullParentCaEntity17> Stats => Set<NullParentCaEntity17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullParentCaEntity17>(e =>
            {
                e.HasNoKey();
                e.ToView("null_parent_ca17");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_Uses_MaterializedViewName_When_ParentName_And_EntityType_Both_Null()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.time) AS bucket, avg(s.avg_val) AS avg_val" +
            " FROM null_parent_src17 s GROUP BY 1";

        using NullParentContext17 context = new();
        IEntityType entityType = GetEntityType<NullParentCaEntity17>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "null_parent_ca17"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        (TimescaleDbAnnotationCodeGenerator generator, _) = CreateGeneratorWithReporter();

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        IEnumerable<string> methods = result.SelectMany(f => CollectMethodChain(f));
        Assert.Contains("IsContinuousAggregate", methods);
    }

    #endregion

    // ── DA mode: parentEntityType null AND parentName null → uses materializedViewName ──

    #region GenerateDataAnnotationAttributes_Uses_MaterializedViewName_When_ParentName_And_EntityType_Both_Null

    private class NullParentDaCaEntity18
    {
        public DateTime Bucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class NullParentDaContext18 : DbContext
    {
        public DbSet<NullParentDaCaEntity18> Stats => Set<NullParentDaCaEntity18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullParentDaCaEntity18>(e =>
            {
                e.HasNoKey();
                e.ToView("null_parent_da_ca18");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_Uses_MaterializedViewName_When_ParentName_And_EntityType_Both_Null()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.time) AS bucket, avg(s.avg_val) AS avg_val" +
            " FROM null_parent_da_src18 s GROUP BY 1";

        using NullParentDaContext18 context = new();
        IEntityType entityType = GetEntityType<NullParentDaCaEntity18>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "null_parent_da_ca18"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef));

        (TimescaleDbAnnotationCodeGenerator generator, _) = CreateGeneratorWithReporter();
        generator.ScaffoldDataAnnotationsMode = true;

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.Contains(result, a => a.Type == typeof(ContinuousAggregateAttribute));
    }

    #endregion

    // ── EnableCompression bare fallback (no segmentBy/orderBy) ─────────────────

    #region GenerateFluentApiCalls_EnableCompression_WithNoSegmentByOrOrderBy_ChainsWithCompression

    private class EnableCompressionFallbackCaEntity19
    {
        public DateTime Bucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class EnableCompressionFallbackContext19 : DbContext
    {
        public DbSet<EnableCompressionFallbackCaEntity19> Stats => Set<EnableCompressionFallbackCaEntity19>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EnableCompressionFallbackCaEntity19>(e =>
            {
                e.HasNoKey();
                e.ToView("enable_comp_fallback19");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_EnableCompression_WithNoSegmentByOrOrderBy_ChainsWithCompression()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket, avg(s.avg_val) AS avg_val" +
            " FROM enable_comp_src19 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using EnableCompressionFallbackContext19 context = new();
        IEntityType entityType = GetEntityType<EnableCompressionFallbackCaEntity19>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "enable_comp_fallback19"),
            (ContinuousAggregateAnnotations.ParentName, "enable_comp_src19"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.EnableCompression, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        List<string> chain = CollectMethodChain(root);
        Assert.Contains("WithCompression", chain);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_EnableCompression_WithNoSegmentByOrOrderBy_SetsEnabledNamedArg

    private class EnableCompressionDaFallbackCaEntity20
    {
        public DateTime Bucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class EnableCompressionDaFallbackContext20 : DbContext
    {
        public DbSet<EnableCompressionDaFallbackCaEntity20> Stats => Set<EnableCompressionDaFallbackCaEntity20>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EnableCompressionDaFallbackCaEntity20>(e =>
            {
                e.HasNoKey();
                e.ToView("enable_comp_da_fallback20");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_EnableCompression_WithNoSegmentByOrOrderBy_SetsEnabledNamedArg()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket, avg(s.avg_val) AS avg_val" +
            " FROM enable_comp_da_src20 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using EnableCompressionDaFallbackContext20 context = new();
        IEntityType entityType = GetEntityType<EnableCompressionDaFallbackCaEntity20>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "enable_comp_da_fallback20"),
            (ContinuousAggregateAnnotations.ParentName, "enable_comp_da_src20"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.EnableCompression, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.EnableCompression)));
        Assert.Equal(true, caAttr.NamedArguments[nameof(ContinuousAggregateAttribute.EnableCompression)]);
    }

    #endregion

    // ── Rename-safe scaffolding of compression settings ────────────────────────

    #region GenerateFluentApiCalls_CompressionSegmentBy_ResolvableColumn_YieldsColumnListCodeFragment

    private class CaCompSegBySourceEntity21
    {
        public DateTime Time { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class CaCompSegByCaEntity21
    {
        public DateTime Bucket { get; set; }
        public double AvgDuration { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class CaCompSegByContext21 : DbContext
    {
        public DbSet<CaCompSegBySourceEntity21> Sources => Set<CaCompSegBySourceEntity21>();
        public DbSet<CaCompSegByCaEntity21> Stats => Set<CaCompSegByCaEntity21>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompSegBySourceEntity21>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_comp_seg_src21");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });
            modelBuilder.Entity<CaCompSegByCaEntity21>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_seg_stats21");
                e.Property(x => x.AvgDuration).HasColumnName("avg_duration");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_CompressionSegmentBy_ResolvableColumn_YieldsColumnListCodeFragment()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " avg(s.duration) AS avg_duration, s.service_name AS service_name" +
            " FROM ca_comp_seg_src21 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\"), s.service_name";

        using CaCompSegByContext21 context = new();
        IEntityType entityType = GetEntityType<CaCompSegByCaEntity21>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_seg_stats21"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_seg_src21"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionSegmentBy, "service_name"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        MethodCallCodeFragment? segByCall = null;
        for (MethodCallCodeFragment? cur = root; cur != null; cur = cur.ChainedCall)
        {
            if (cur.Method == "WithCompressionSegmentBy") { segByCall = cur; break; }
        }
        Assert.NotNull(segByCall);
        ColumnListCodeFragment columnList = Assert.IsType<ColumnListCodeFragment>(segByCall.Arguments[0]);
        NameOfCodeFragment entry = Assert.IsType<NameOfCodeFragment>(Assert.Single(columnList.Entries));
        Assert.Equal("CaCompSegByCaEntity21.ServiceName", entry.PropertyName);
        Assert.Equal("", entry.Suffix);
    }

    #endregion

    #region GenerateFluentApiCalls_CompressionOrderBy_ResolvableColumn_YieldsColumnListCodeFragmentWithSuffix

    private class CaCompOrdBySourceEntity22
    {
        public DateTime Time { get; set; }
    }

    private class CaCompOrdByCaEntity22
    {
        public DateTime TimeBucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class CaCompOrdByContext22 : DbContext
    {
        public DbSet<CaCompOrdBySourceEntity22> Sources => Set<CaCompOrdBySourceEntity22>();
        public DbSet<CaCompOrdByCaEntity22> Stats => Set<CaCompOrdByCaEntity22>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompOrdBySourceEntity22>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_comp_ord_src22");
                e.Property(x => x.Time).HasColumnName("time");
            });
            modelBuilder.Entity<CaCompOrdByCaEntity22>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_ord_stats22");
                e.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_CompressionOrderBy_ResolvableColumn_YieldsColumnListCodeFragmentWithSuffix()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS time_bucket," +
            " avg(s.val) AS avg_val" +
            " FROM ca_comp_ord_src22 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using CaCompOrdByContext22 context = new();
        IEntityType entityType = GetEntityType<CaCompOrdByCaEntity22>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_ord_stats22"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_ord_src22"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionOrderBy, "time_bucket DESC"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        MethodCallCodeFragment? ordByCall = null;
        for (MethodCallCodeFragment? cur = root; cur != null; cur = cur.ChainedCall)
        {
            if (cur.Method == "WithCompressionOrderBy") { ordByCall = cur; break; }
        }
        Assert.NotNull(ordByCall);
        ColumnListCodeFragment columnList = Assert.IsType<ColumnListCodeFragment>(ordByCall.Arguments[0]);
        NameOfCodeFragment entry = Assert.IsType<NameOfCodeFragment>(Assert.Single(columnList.Entries));
        Assert.Equal("CaCompOrdByCaEntity22.TimeBucket", entry.PropertyName);
        Assert.Equal(" DESC", entry.Suffix);
    }

    #endregion

    #region GenerateFluentApiCalls_CompressionSegmentBy_UnresolvableColumn_FallsBackToRawString

    private class CaCompSegByUnresCaEntity23
    {
        public DateTime Bucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class CaCompSegByUnresContext23 : DbContext
    {
        public DbSet<CaCompSegByUnresCaEntity23> Stats => Set<CaCompSegByUnresCaEntity23>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompSegByUnresCaEntity23>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_seg_unres_stats23");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_CompressionSegmentBy_UnresolvableColumn_FallsBackToRawString()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket, avg(s.val) AS avg_val" +
            " FROM ca_comp_seg_unres_src23 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using CaCompSegByUnresContext23 context = new();
        IEntityType entityType = GetEntityType<CaCompSegByUnresCaEntity23>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_seg_unres_stats23"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_seg_unres_src23"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionSegmentBy, "unmapped_col"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        MethodCallCodeFragment? segByCall = null;
        for (MethodCallCodeFragment? cur = root; cur != null; cur = cur.ChainedCall)
        {
            if (cur.Method == "WithCompressionSegmentBy") { segByCall = cur; break; }
        }
        Assert.NotNull(segByCall);
        string rawArg = Assert.IsType<string>(segByCall.Arguments[0]);
        Assert.Equal("unmapped_col", rawArg);
    }

    #endregion

    #region GenerateFluentApiCalls_CompressionOrderBy_MixedResolvableAndUnresolvable_YieldsColumnListCodeFragmentWithBothEntries

    private class CaCompMixedSourceEntity24
    {
        public DateTime Time { get; set; }
    }

    private class CaCompMixedCaEntity24
    {
        public DateTime TimeBucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class CaCompMixedContext24 : DbContext
    {
        public DbSet<CaCompMixedSourceEntity24> Sources => Set<CaCompMixedSourceEntity24>();
        public DbSet<CaCompMixedCaEntity24> Stats => Set<CaCompMixedCaEntity24>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompMixedSourceEntity24>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_comp_mixed_src24");
                e.Property(x => x.Time).HasColumnName("time");
            });
            modelBuilder.Entity<CaCompMixedCaEntity24>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_mixed_stats24");
                e.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateFluentApiCalls_CompressionOrderBy_MixedResolvableAndUnresolvable_YieldsColumnListCodeFragmentWithBothEntries()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS time_bucket," +
            " avg(s.val) AS avg_val" +
            " FROM ca_comp_mixed_src24 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        using CaCompMixedContext24 context = new();
        IEntityType entityType = GetEntityType<CaCompMixedCaEntity24>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_mixed_stats24"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_mixed_src24"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionOrderBy, "time_bucket DESC, unmapped_col"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => f.Method == nameof(ContinuousAggregateTypeBuilder.IsContinuousAggregate));
        MethodCallCodeFragment? ordByCall = null;
        for (MethodCallCodeFragment? cur = root; cur != null; cur = cur.ChainedCall)
        {
            if (cur.Method == "WithCompressionOrderBy") { ordByCall = cur; break; }
        }
        Assert.NotNull(ordByCall);
        ColumnListCodeFragment columnList = Assert.IsType<ColumnListCodeFragment>(ordByCall.Arguments[0]);
        Assert.Equal(2, columnList.Entries.Count);
        NameOfCodeFragment nameOfEntry = Assert.IsType<NameOfCodeFragment>(columnList.Entries[0]);
        Assert.Equal("CaCompMixedCaEntity24.TimeBucket", nameOfEntry.PropertyName);
        Assert.Equal(" DESC", nameOfEntry.Suffix);
        string rawEntry = Assert.IsType<string>(columnList.Entries[1]);
        Assert.Equal("unmapped_col", rawEntry);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionSegmentBy_ResolvableColumn_YieldsBareNameOfInNamedArg

    private class CaCompDaSegBySourceEntity25
    {
        public DateTime Time { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class CaCompDaSegByCaEntity25
    {
        public DateTime Bucket { get; set; }
        public string ServiceName { get; set; } = "";
    }

    private class CaCompDaSegByContext25 : DbContext
    {
        public DbSet<CaCompDaSegBySourceEntity25> Sources => Set<CaCompDaSegBySourceEntity25>();
        public DbSet<CaCompDaSegByCaEntity25> Stats => Set<CaCompDaSegByCaEntity25>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompDaSegBySourceEntity25>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_comp_da_seg_src25");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });
            modelBuilder.Entity<CaCompDaSegByCaEntity25>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_da_seg_stats25");
                e.Property(x => x.ServiceName).HasColumnName("service_name");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionSegmentBy_ResolvableColumn_YieldsBareNameOfInNamedArg()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket," +
            " s.service_name AS service_name" +
            " FROM ca_comp_da_seg_src25 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\"), s.service_name";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using CaCompDaSegByContext25 context = new();
        IEntityType entityType = GetEntityType<CaCompDaSegByCaEntity25>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_da_seg_stats25"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_da_seg_src25"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionSegmentBy, "service_name"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.CompressionSegmentBy)));
        object?[] segByArg = Assert.IsType<object[]>(caAttr.NamedArguments[nameof(ContinuousAggregateAttribute.CompressionSegmentBy)]);
        NameOfCodeFragment nameOf = Assert.IsType<NameOfCodeFragment>(Assert.Single(segByArg));
        Assert.Equal("ServiceName", nameOf.PropertyName);
        Assert.Equal("", nameOf.Suffix);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionOrderBy_ResolvableColumn_YieldsSuffixedNameOfInNamedArg

    private class CaCompDaOrdBySourceEntity26
    {
        public DateTime Time { get; set; }
    }

    private class CaCompDaOrdByCaEntity26
    {
        public DateTime TimeBucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class CaCompDaOrdByContext26 : DbContext
    {
        public DbSet<CaCompDaOrdBySourceEntity26> Sources => Set<CaCompDaOrdBySourceEntity26>();
        public DbSet<CaCompDaOrdByCaEntity26> Stats => Set<CaCompDaOrdByCaEntity26>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompDaOrdBySourceEntity26>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_comp_da_ord_src26");
                e.Property(x => x.Time).HasColumnName("time");
            });
            modelBuilder.Entity<CaCompDaOrdByCaEntity26>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_da_ord_stats26");
                e.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionOrderBy_ResolvableColumn_YieldsSuffixedNameOfInNamedArg()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS time_bucket," +
            " avg(s.val) AS avg_val" +
            " FROM ca_comp_da_ord_src26 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using CaCompDaOrdByContext26 context = new();
        IEntityType entityType = GetEntityType<CaCompDaOrdByCaEntity26>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_da_ord_stats26"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_da_ord_src26"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionOrderBy, "time_bucket DESC"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.CompressionOrderBy)));
        object?[] ordByArg = Assert.IsType<object[]>(caAttr.NamedArguments[nameof(ContinuousAggregateAttribute.CompressionOrderBy)]);
        NameOfCodeFragment nameOf = Assert.IsType<NameOfCodeFragment>(Assert.Single(ordByArg));
        Assert.Equal("TimeBucket", nameOf.PropertyName);
        Assert.Equal(" DESC", nameOf.Suffix);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CompressionSegmentBy_AllUnresolvable_YieldsPlainStringArray

    private class CaCompDaUnresCaEntity27
    {
        public DateTime Bucket { get; set; }
        public double AvgVal { get; set; }
    }

    private class CaCompDaUnresContext27 : DbContext
    {
        public DbSet<CaCompDaUnresCaEntity27> Stats => Set<CaCompDaUnresCaEntity27>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaCompDaUnresCaEntity27>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_comp_da_unres_stats27");
                e.Property(x => x.AvgVal).HasColumnName("avg_val");
            });
        }
    }

    [Fact]
    public void GenerateDataAnnotationAttributes_CompressionSegmentBy_AllUnresolvable_YieldsPlainStringArray()
    {
        // Arrange
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, s.\"time\") AS bucket, avg(s.val) AS avg_val" +
            " FROM ca_comp_da_unres_src27 s GROUP BY time_bucket('01:00:00'::interval, s.\"time\")";

        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)CreateAnnotationCodeGenerator();
        generator.ScaffoldDataAnnotationsMode = true;

        using CaCompDaUnresContext27 context = new();
        IEntityType entityType = GetEntityType<CaCompDaUnresCaEntity27>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregateAnnotations.MaterializedViewName, "ca_comp_da_unres_stats27"),
            (ContinuousAggregateAnnotations.ParentName, "ca_comp_da_unres_src27"),
            (ContinuousAggregateAnnotations.ViewDefinition, viewDef),
            (HypertableAnnotations.CompressionSegmentBy, "unmapped_col_a, unmapped_col_b"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = generator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? caAttr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregateAttribute));
        Assert.NotNull(caAttr);
        Assert.True(caAttr.NamedArguments.ContainsKey(nameof(ContinuousAggregateAttribute.CompressionSegmentBy)));
        string[] segByArg = Assert.IsType<string[]>(caAttr.NamedArguments[nameof(ContinuousAggregateAttribute.CompressionSegmentBy)]);
        Assert.Equal(2, segByArg.Length);
        Assert.Equal("unmapped_col_a", segByArg[0]);
        Assert.Equal("unmapped_col_b", segByArg[1]);
    }

    #endregion
}

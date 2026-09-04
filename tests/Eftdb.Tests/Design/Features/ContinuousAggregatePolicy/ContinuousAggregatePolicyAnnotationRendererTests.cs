#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.ContinuousAggregatePolicy;

/// <summary>
/// Tests for <c>ContinuousAggregatePolicyAnnotationRenderer</c> exercised through the public
/// <see cref="TimescaleDbAnnotationCodeGenerator"/> surface.
/// The renderer runs AFTER <c>ContinuousAggregateAnnotationRenderer</c> and uses the presence or
/// absence of <c>MaterializedViewName</c> to decide whether to emit policy code.
/// </summary>
public class ContinuousAggregatePolicyAnnotationRendererTests
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
        "SELECT time_bucket('01:00:00'::interval, policy_source.\"time\") AS bucket," +
        " avg(policy_source.value) AS avg_value" +
        " FROM policy_source" +
        " GROUP BY time_bucket('01:00:00'::interval, policy_source.\"time\")";

    private class PolicySourceEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class PolicyCaEntity
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class PolicyTestContext : DbContext
    {
        public DbSet<PolicySourceEntity> Sources => Set<PolicySourceEntity>();
        public DbSet<PolicyCaEntity> CaViews => Set<PolicyCaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PolicySourceEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("policy_source");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });

            modelBuilder.Entity<PolicyCaEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("policy_ca_view");
                e.Property(x => x.AvgValue).HasColumnName("avg_value");
            });
        }
    }

    // ── Guard tests ───────────────────────────────────────────────────────────

    #region GenerateFluentApiCalls_Returns_Empty_When_HasRefreshPolicy_Not_Set

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_HasRefreshPolicy_Not_Set()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithRefreshPolicy"));
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_MaterializedViewName_Still_Present

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_MaterializedViewName_Still_Present()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregateAnnotations.MaterializedViewName, "policy_ca_view"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithRefreshPolicy"));
        Assert.True(annotations.ContainsKey(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy));
        Assert.True(annotations.ContainsKey(ContinuousAggregatePolicyAnnotations.StartOffset));
    }

    #endregion

    // ── Arg-trimming tests ────────────────────────────────────────────────────

    #region GenerateFluentApiCalls_WithRefreshPolicy_NoArgs_When_All_Offsets_Absent

    [Fact]
    public void GenerateFluentApiCalls_WithRefreshPolicy_NoArgs_When_All_Offsets_Absent()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRefreshPolicy = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithRefreshPolicy");

        Assert.NotNull(withRefreshPolicy);
        Assert.Empty(withRefreshPolicy.Arguments);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRefreshPolicy_OneArg_When_Only_StartOffset

    [Fact]
    public void GenerateFluentApiCalls_WithRefreshPolicy_OneArg_When_Only_StartOffset()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRefreshPolicy = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithRefreshPolicy");

        Assert.NotNull(withRefreshPolicy);
        Assert.Equal(["1 month"], withRefreshPolicy.Arguments);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRefreshPolicy_TwoArgs_When_Start_And_End

    [Fact]
    public void GenerateFluentApiCalls_WithRefreshPolicy_TwoArgs_When_Start_And_End()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRefreshPolicy = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithRefreshPolicy");

        Assert.NotNull(withRefreshPolicy);
        Assert.Equal(2, withRefreshPolicy.Arguments.Count);
        Assert.Equal("1 month", withRefreshPolicy.Arguments[0]);
        Assert.Equal("1 hour", withRefreshPolicy.Arguments[1]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRefreshPolicy_ThreeArgs_When_ScheduleInterval_Present

    [Fact]
    public void GenerateFluentApiCalls_WithRefreshPolicy_ThreeArgs_When_ScheduleInterval_Present()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.ScheduleInterval, "24 hours"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRefreshPolicy = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithRefreshPolicy");

        Assert.NotNull(withRefreshPolicy);
        Assert.Equal(3, withRefreshPolicy.Arguments.Count);
        Assert.Null(withRefreshPolicy.Arguments[0]);
        Assert.Null(withRefreshPolicy.Arguments[1]);
        Assert.Equal("24 hours", withRefreshPolicy.Arguments[2]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRefreshPolicy_ThreeArgs_When_All_Present

    [Fact]
    public void GenerateFluentApiCalls_WithRefreshPolicy_ThreeArgs_When_All_Present()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour"),
            (ContinuousAggregatePolicyAnnotations.ScheduleInterval, "2 hours"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRefreshPolicy = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithRefreshPolicy");

        Assert.NotNull(withRefreshPolicy);
        Assert.Equal(3, withRefreshPolicy.Arguments.Count);
        Assert.Equal("1 month", withRefreshPolicy.Arguments[0]);
        Assert.Equal("1 hour", withRefreshPolicy.Arguments[1]);
        Assert.Equal("2 hours", withRefreshPolicy.Arguments[2]);
    }

    #endregion

    // ── Chaining tests ────────────────────────────────────────────────────────

    #region GenerateFluentApiCalls_Chains_InitialStart_When_Present

    [Fact]
    public void GenerateFluentApiCalls_Chains_InitialStart_When_Present()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.InitialStart, initialStart));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        List<string> chain = [.. result.SelectMany(f => CollectMethodChain(f))];

        Assert.Contains("WithInitialStart", chain);
        MethodCallCodeFragment? initialStartFrag = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithInitialStart");

        Assert.NotNull(initialStartFrag);
        Assert.Equal(initialStart, initialStartFrag.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Chain_InitialStart_When_Absent

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_InitialStart_When_Absent()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithInitialStart"));
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_IncludeTieredData

    [Fact]
    public void GenerateFluentApiCalls_Chains_IncludeTieredData()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.IncludeTieredData, false));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithIncludeTieredData");

        Assert.NotNull(fragment);
        Assert.Equal(false, fragment.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_BucketsPerBatch

    [Fact]
    public void GenerateFluentApiCalls_Chains_BucketsPerBatch()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 5));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithBucketsPerBatch");

        Assert.NotNull(fragment);
        Assert.Equal(5, fragment.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_MaxBatchesPerExecution

    [Fact]
    public void GenerateFluentApiCalls_Chains_MaxBatchesPerExecution()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, 10));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithMaxBatchesPerExecution");

        Assert.NotNull(fragment);
        Assert.Equal(10, fragment.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Chains_RefreshNewestFirst

    [Fact]
    public void GenerateFluentApiCalls_Chains_RefreshNewestFirst()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, false));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? fragment = result
            .SelectMany(f => { List<MethodCallCodeFragment> all = []; for (MethodCallCodeFragment? c = f; c != null; c = c.ChainedCall) all.Add(c); return all; })
            .FirstOrDefault(f => f.Method == "WithRefreshNewestFirst");

        Assert.NotNull(fragment);
        Assert.Equal(false, fragment.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Consumes_All_Policy_Annotations_After_Render

    [Fact]
    public void GenerateFluentApiCalls_Consumes_All_Policy_Annotations_After_Render()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour"),
            (ContinuousAggregatePolicyAnnotations.ScheduleInterval, "2 hours"),
            (ContinuousAggregatePolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (ContinuousAggregatePolicyAnnotations.IfNotExists, true),
            (ContinuousAggregatePolicyAnnotations.IncludeTieredData, false),
            (ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 3),
            (ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, 10),
            (ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, false));

        // Act
        CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:ContinuousAggregatePolicy:", StringComparison.Ordinal));
    }

    #endregion

    #region GenerateFluentApiCalls_Chain_Order_Is_RefreshPolicy_Then_InitialStart_Then_Others

    [Fact]
    public void GenerateFluentApiCalls_Chain_Order_Is_RefreshPolicy_Then_InitialStart_Then_Others()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (ContinuousAggregatePolicyAnnotations.IncludeTieredData, false),
            (ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 3),
            (ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, 10),
            (ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, false));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => CollectMethodChain(f).Contains("WithRefreshPolicy"));
        List<string> chain = CollectMethodChain(root);

        int refreshPolicyIdx = chain.IndexOf("WithRefreshPolicy");
        int initialStartIdx = chain.IndexOf("WithInitialStart");
        int includeTieredIdx = chain.IndexOf("WithIncludeTieredData");
        int bucketsIdx = chain.IndexOf("WithBucketsPerBatch");
        int maxBatchesIdx = chain.IndexOf("WithMaxBatchesPerExecution");
        int newestFirstIdx = chain.IndexOf("WithRefreshNewestFirst");

        Assert.True(refreshPolicyIdx < initialStartIdx, "WithRefreshPolicy must precede WithInitialStart.");
        Assert.True(initialStartIdx < includeTieredIdx, "WithInitialStart must precede WithIncludeTieredData.");
        Assert.True(includeTieredIdx < bucketsIdx, "WithIncludeTieredData must precede WithBucketsPerBatch.");
        Assert.True(bucketsIdx < maxBatchesIdx, "WithBucketsPerBatch must precede WithMaxBatchesPerExecution.");
        Assert.True(maxBatchesIdx < newestFirstIdx, "WithMaxBatchesPerExecution must precede WithRefreshNewestFirst.");
    }

    #endregion

    // ── Data-annotation tests ─────────────────────────────────────────────────

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_HasRefreshPolicy_Not_Set

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_HasRefreshPolicy_Not_Set()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ContinuousAggregatePolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_MaterializedViewName_Still_Present

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_MaterializedViewName_Still_Present()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregateAnnotations.MaterializedViewName, "policy_ca_view"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ContinuousAggregatePolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Attribute_With_All_Named_Args

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_ContinuousAggregatePolicyAttribute_With_All_Named_Args()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour"),
            (ContinuousAggregatePolicyAnnotations.ScheduleInterval, "2 hours"),
            (ContinuousAggregatePolicyAnnotations.InitialStart, initialStart),
            (ContinuousAggregatePolicyAnnotations.IncludeTieredData, false),
            (ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 3),
            (ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, 10),
            (ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, false));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregatePolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("1 month", attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.StartOffset)]);
        Assert.Equal("1 hour", attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.EndOffset)]);
        Assert.Equal("2 hours", attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.ScheduleInterval)]);
        Assert.True(attr.NamedArguments.ContainsKey(nameof(ContinuousAggregatePolicyAttribute.InitialStart)));
        Assert.Equal(false, attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.IncludeTieredData)]);
        Assert.Equal(3, attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.BucketsPerBatch)]);
        Assert.Equal(10, attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.MaxBatchesPerExecution)]);
        Assert.Equal(false, attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.RefreshNewestFirst)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregatePolicyAttribute));
        Assert.NotNull(attr);
        Assert.Empty(attr.NamedArguments);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_InitialStart_Uses_ISO8601_UTC_String

    [Fact]
    public void GenerateDataAnnotationAttributes_InitialStart_Uses_ISO8601_UTC_String()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);

        DateTime localTime = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.InitialStart, localTime));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ContinuousAggregatePolicyAttribute));
        Assert.NotNull(attr);
        string? initialStartStr = attr.NamedArguments[nameof(ContinuousAggregatePolicyAttribute.InitialStart)] as string;
        Assert.NotNull(initialStartStr);
        Assert.EndsWith("Z", initialStartStr, StringComparison.Ordinal);
        DateTime parsed = DateTime.Parse(initialStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(localTime.ToUniversalTime(), parsed);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Consumes_All_Policy_Annotations_After_Render

    [Fact]
    public void GenerateDataAnnotationAttributes_Consumes_All_Policy_Annotations_After_Render()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.EndOffset, "1 hour"),
            (ContinuousAggregatePolicyAnnotations.ScheduleInterval, "2 hours"),
            (ContinuousAggregatePolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (ContinuousAggregatePolicyAnnotations.IfNotExists, true),
            (ContinuousAggregatePolicyAnnotations.IncludeTieredData, false),
            (ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 3),
            (ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution, 10),
            (ContinuousAggregatePolicyAnnotations.RefreshNewestFirst, false));

        // Act
        CreateAnnotationCodeGenerator().GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:ContinuousAggregatePolicy:", StringComparison.Ordinal));
    }

    #endregion

    // ── P2: ConsumeFeatureAnnotations ─────────────────────────────────────────

    #region ConsumeFeatureAnnotations_Removes_All_Policy_Keys_When_ShouldRender_True

    [Fact]
    public void ConsumeFeatureAnnotations_Removes_All_Policy_Keys_When_ShouldRender_True()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregatePolicyAnnotations.IfNotExists, false),
            (ContinuousAggregatePolicyAnnotations.BucketsPerBatch, 2));

        // Act
        CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(annotations.Keys, k => k.StartsWith("TimescaleDB:ContinuousAggregatePolicy:", StringComparison.Ordinal));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Leaves_Keys_When_ShouldRender_False

    [Fact]
    public void ConsumeFeatureAnnotations_Leaves_Keys_When_ShouldRender_False()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.StartOffset, "1 month"),
            (ContinuousAggregateAnnotations.MaterializedViewName, "policy_ca_view"));

        // Act
        CreateAnnotationCodeGenerator().GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(ContinuousAggregatePolicyAnnotations.HasRefreshPolicy));
        Assert.True(annotations.ContainsKey(ContinuousAggregatePolicyAnnotations.StartOffset));
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Chain_InitialStart_When_Value_Is_Not_DateTime

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_InitialStart_When_Value_Is_Not_DateTime()
    {
        // Arrange
        using PolicyTestContext context = new();
        IEntityType entityType = GetEntityType<PolicyCaEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ContinuousAggregatePolicyAnnotations.HasRefreshPolicy, true),
            (ContinuousAggregatePolicyAnnotations.InitialStart, "2025-06-01T00:00:00Z"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator()
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithInitialStart"));
    }

    #endregion
}
#pragma warning restore EF1001

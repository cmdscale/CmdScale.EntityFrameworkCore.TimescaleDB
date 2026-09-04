#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.RetentionPolicy;

/// <summary>
/// Tests for <c>RetentionPolicyAnnotationRenderer</c> exercised through the public
/// <see cref="TimescaleDbAnnotationCodeGenerator"/> surface.
/// The renderer runs AFTER <c>HypertableAnnotationRenderer</c> and
/// <c>ContinuousAggregateAnnotationRenderer</c> and uses the ABSENCE of <c>IsHypertable</c> and
/// <c>MaterializedViewName</c> to decide whether to emit retention policy code.
/// </summary>
public class RetentionPolicyAnnotationRendererTests
{
    private sealed record StubAnnotation(string Name, object? Value) : IAnnotation;

    private static Dictionary<string, IAnnotation> Annotations(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => (IAnnotation)new StubAnnotation(p.Key, p.Value));

    private static IAnnotationCodeGenerator CreateAnnotationCodeGenerator
    {
        get
        {
            ServiceCollection services = new();
            services.AddEntityFrameworkDesignTimeServices();
            new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
            TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
                .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();

            generator.ScaffoldMode = true;
            return generator;
        }
    }

    private static IEntityType GetEntityType<T>(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!;

    private static List<string> CollectMethodChain(MethodCallCodeFragment? fragment)
    {
        List<string> methods = [];
        while (fragment != null) { methods.Add(fragment.Method); fragment = fragment.ChainedCall; }
        return methods;
    }

    private static List<MethodCallCodeFragment> FlattenChain(MethodCallCodeFragment? fragment)
    {
        List<MethodCallCodeFragment> all = [];
        for (MethodCallCodeFragment? c = fragment; c != null; c = c.ChainedCall) all.Add(c);
        return all;
    }

    private class RetentionRendererEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class RetentionRendererContext : DbContext
    {
        public DbSet<RetentionRendererEntity> Items => Set<RetentionRendererEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RetentionRendererEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("retention_renderer_entity");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
    }

    // ── Guard tests ───────────────────────────────────────────────────────────

    #region GenerateFluentApiCalls_Returns_Empty_When_HasRetentionPolicy_Absent

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_HasRetentionPolicy_Absent()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithRetentionPolicy"));
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Still_Present

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Still_Present()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithRetentionPolicy"));
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy),
            "HasRetentionPolicy annotation must remain unconsumed when ShouldRender is false.");
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter),
            "DropAfter annotation must remain unconsumed when ShouldRender is false.");
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_MaterializedViewName_Still_Present

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_MaterializedViewName_Still_Present()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (ContinuousAggregateAnnotations.MaterializedViewName, "some_view"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithRetentionPolicy"));
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy),
            "HasRetentionPolicy annotation must remain unconsumed when ShouldRender is false.");
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter),
            "DropAfter annotation must remain unconsumed when ShouldRender is false.");
    }

    #endregion

    #region GenerateFluentApiCalls_Renders_When_IsHypertable_Consumed_And_MaterializedViewName_Absent

    [Fact]
    public void GenerateFluentApiCalls_Renders_When_IsHypertable_Consumed_And_MaterializedViewName_Absent()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("WithRetentionPolicy"));
    }

    #endregion

    #region GenerateFluentApiCalls_Renders_When_MaterializedViewName_Consumed_And_IsHypertable_Absent

    [Fact]
    public void GenerateFluentApiCalls_Renders_When_MaterializedViewName_Consumed_And_IsHypertable_Absent()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("WithRetentionPolicy"));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_HasRetentionPolicy_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_HasRetentionPolicy_Absent()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(RetentionPolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Still_Present

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Still_Present()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(RetentionPolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_MaterializedViewName_Still_Present

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_MaterializedViewName_Still_Present()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (ContinuousAggregateAnnotations.MaterializedViewName, "some_view"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(RetentionPolicyAttribute));
    }

    #endregion

    // ── Fluent positional argument tests (always all six args) ────────────────

    #region GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_Only_DropAfter

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_Only_DropAfter()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Equal("7 days", withRetentionPolicy.Arguments[0]);
        Assert.All(withRetentionPolicy.Arguments.Skip(1), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_Only_DropCreatedBefore

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_Only_DropCreatedBefore()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropCreatedBefore, "30 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Null(withRetentionPolicy.Arguments[0]);
        Assert.Equal("30 days", withRetentionPolicy.Arguments[1]);
        Assert.All(withRetentionPolicy.Arguments.Skip(2), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_DropAfter_And_ScheduleInterval

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_DropAfter_And_ScheduleInterval()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.ScheduleInterval, "1 day"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Equal("7 days", withRetentionPolicy.Arguments[0]);
        Assert.Null(withRetentionPolicy.Arguments[1]);
        Assert.Equal("1 day", withRetentionPolicy.Arguments[2]);
        Assert.All(withRetentionPolicy.Arguments.Skip(3), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_DropAfter_And_MaxRuntime

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_DropAfter_And_MaxRuntime()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.MaxRuntime, "01:00:00"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Equal("7 days", withRetentionPolicy.Arguments[0]);
        Assert.Null(withRetentionPolicy.Arguments[1]);
        Assert.Null(withRetentionPolicy.Arguments[2]);
        Assert.Equal("01:00:00", withRetentionPolicy.Arguments[3]);
        Assert.All(withRetentionPolicy.Arguments.Skip(4), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_DropAfter_And_MaxRetries

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_Emits_All_Args_When_DropAfter_And_MaxRetries()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.MaxRetries, 5));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Equal("7 days", withRetentionPolicy.Arguments[0]);
        Assert.Null(withRetentionPolicy.Arguments[1]);
        Assert.Null(withRetentionPolicy.Arguments[2]);
        Assert.Null(withRetentionPolicy.Arguments[3]);
        Assert.Equal(5, withRetentionPolicy.Arguments[4]);
        Assert.Null(withRetentionPolicy.Arguments[5]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRetentionPolicy_SixArgs_When_All_Six_Set

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_SixArgs_When_All_Six_Set()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.ScheduleInterval, "1 day"),
            (RetentionPolicyAnnotations.MaxRuntime, "01:00:00"),
            (RetentionPolicyAnnotations.MaxRetries, 3),
            (RetentionPolicyAnnotations.RetryPeriod, "00:10:00"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Equal("7 days", withRetentionPolicy.Arguments[0]);
        Assert.Null(withRetentionPolicy.Arguments[1]);
        Assert.Equal("1 day", withRetentionPolicy.Arguments[2]);
        Assert.Equal("01:00:00", withRetentionPolicy.Arguments[3]);
        Assert.Equal(3, withRetentionPolicy.Arguments[4]);
        Assert.Equal("00:10:00", withRetentionPolicy.Arguments[5]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithRetentionPolicy_SixArgs_When_DropCreatedBefore_And_RetryPeriod

    [Fact]
    public void GenerateFluentApiCalls_WithRetentionPolicy_SixArgs_When_DropCreatedBefore_And_RetryPeriod()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropCreatedBefore, "30 days"),
            (RetentionPolicyAnnotations.RetryPeriod, "00:10:00"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withRetentionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithRetentionPolicy");

        Assert.NotNull(withRetentionPolicy);
        Assert.Equal(6, withRetentionPolicy.Arguments.Count);
        Assert.Null(withRetentionPolicy.Arguments[0]);
        Assert.Equal("30 days", withRetentionPolicy.Arguments[1]);
        Assert.Null(withRetentionPolicy.Arguments[2]);
        Assert.Null(withRetentionPolicy.Arguments[3]);
        Assert.Null(withRetentionPolicy.Arguments[4]);
        Assert.Equal("00:10:00", withRetentionPolicy.Arguments[5]);
    }

    #endregion

    // ── InitialStart chaining tests ───────────────────────────────────────────

    #region GenerateFluentApiCalls_Chains_WithInitialStart_When_Present

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithInitialStart_When_Present()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.InitialStart, initialStart));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? initialStartFrag = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithInitialStart");

        Assert.NotNull(initialStartFrag);
        Assert.Equal(initialStart, initialStartFrag.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Chain_WithInitialStart_When_Absent

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_WithInitialStart_When_Absent()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithInitialStart"));
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Chain_WithInitialStart_When_Value_Is_Not_DateTime

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_WithInitialStart_When_Value_Is_Not_DateTime()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.InitialStart, "2025-06-01T00:00:00Z"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithInitialStart"));
    }

    #endregion

    #region GenerateFluentApiCalls_Chain_Order_Is_WithRetentionPolicy_Then_WithInitialStart

    [Fact]
    public void GenerateFluentApiCalls_Chain_Order_Is_WithRetentionPolicy_Then_WithInitialStart()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => CollectMethodChain(f).Contains("WithRetentionPolicy"));
        List<string> chain = CollectMethodChain(root);

        int retentionIdx = chain.IndexOf("WithRetentionPolicy");
        int initialStartIdx = chain.IndexOf("WithInitialStart");

        Assert.True(retentionIdx < initialStartIdx,
            "WithRetentionPolicy must precede WithInitialStart in the chain.");
    }

    #endregion

    // ── Annotation consumption tests ──────────────────────────────────────────

    #region GenerateFluentApiCalls_Consumes_All_Retention_Annotations_After_Render

    [Fact]
    public void GenerateFluentApiCalls_Consumes_All_Retention_Annotations_After_Render()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.ScheduleInterval, "1 day"),
            (RetentionPolicyAnnotations.MaxRuntime, "01:00:00"),
            (RetentionPolicyAnnotations.MaxRetries, 3),
            (RetentionPolicyAnnotations.RetryPeriod, "00:10:00"),
            (RetentionPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.ScheduleInterval));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.MaxRuntime));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.MaxRetries));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.RetryPeriod));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.InitialStart));
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Consume_When_ShouldRender_False

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Consume_When_ShouldRender_False()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy));
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Removes_All_Keys_When_ShouldRender_True

    [Fact]
    public void ConsumeFeatureAnnotations_Removes_All_Keys_When_ShouldRender_True()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.ScheduleInterval, "1 day"));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Leaves_Keys_When_ShouldRender_False

    [Fact]
    public void ConsumeFeatureAnnotations_Leaves_Keys_When_ShouldRender_False()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (ContinuousAggregateAnnotations.MaterializedViewName, "some_view"));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy));
        Assert.True(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter));
    }

    #endregion

    // ── Data-annotation attribute generation tests ────────────────────────────

    #region GenerateDataAnnotationAttributes_DropAfter_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_DropAfter_As_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("7 days", attr.NamedArguments[nameof(RetentionPolicyAttribute.DropAfter)]);
        Assert.Empty(attr.Arguments);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_DropCreatedBefore_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_DropCreatedBefore_As_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropCreatedBefore, "30 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("30 days", attr.NamedArguments[nameof(RetentionPolicyAttribute.DropCreatedBefore)]);
        Assert.False(attr.NamedArguments.ContainsKey(nameof(RetentionPolicyAttribute.DropAfter)));
        Assert.Empty(attr.Arguments);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_ScheduleInterval_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_ScheduleInterval_As_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.ScheduleInterval, "1 day"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("1 day", attr.NamedArguments[nameof(RetentionPolicyAttribute.ScheduleInterval)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_MaxRuntime_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_MaxRuntime_As_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.MaxRuntime, "01:00:00"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("01:00:00", attr.NamedArguments[nameof(RetentionPolicyAttribute.MaxRuntime)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_MaxRetries_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_MaxRetries_As_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.MaxRetries, 5));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal(5, attr.NamedArguments[nameof(RetentionPolicyAttribute.MaxRetries)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_RetryPeriod_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_RetryPeriod_As_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.RetryPeriod, "00:10:00"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("00:10:00", attr.NamedArguments[nameof(RetentionPolicyAttribute.RetryPeriod)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_InitialStart_As_ISO8601_UTC_String_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_InitialStart_As_ISO8601_UTC_String_Named_Arg()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.InitialStart, initialStart));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        string? initialStartStr = attr.NamedArguments[nameof(RetentionPolicyAttribute.InitialStart)] as string;
        Assert.NotNull(initialStartStr);
        Assert.EndsWith("Z", initialStartStr, StringComparison.Ordinal);
        DateTime parsed = DateTime.Parse(initialStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(initialStart, parsed);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Local_DateTime_Converted_To_UTC_For_InitialStart

    [Fact]
    public void GenerateDataAnnotationAttributes_Local_DateTime_Converted_To_UTC_For_InitialStart()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        DateTime localTime = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.InitialStart, localTime));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        string? initialStartStr = attr.NamedArguments[nameof(RetentionPolicyAttribute.InitialStart)] as string;
        Assert.NotNull(initialStartStr);
        Assert.EndsWith("Z", initialStartStr, StringComparison.Ordinal);
        DateTime parsed = DateTime.Parse(initialStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(localTime.ToUniversalTime(), parsed);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(RetentionPolicyAttribute));
        Assert.NotNull(attr);
        string namedArg = Assert.Single(attr.NamedArguments).Key;
        Assert.Equal(nameof(RetentionPolicyAttribute.DropAfter), namedArg);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Consumes_All_Retention_Annotations_After_Render

    [Fact]
    public void GenerateDataAnnotationAttributes_Consumes_All_Retention_Annotations_After_Render()
    {
        // Arrange
        using RetentionRendererContext context = new();
        IEntityType entityType = GetEntityType<RetentionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (RetentionPolicyAnnotations.HasRetentionPolicy, true),
            (RetentionPolicyAnnotations.DropAfter, "7 days"),
            (RetentionPolicyAnnotations.ScheduleInterval, "1 day"),
            (RetentionPolicyAnnotations.MaxRuntime, "01:00:00"),
            (RetentionPolicyAnnotations.MaxRetries, 3),
            (RetentionPolicyAnnotations.RetryPeriod, "00:10:00"),
            (RetentionPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        CreateAnnotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.HasRetentionPolicy));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.DropAfter));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.ScheduleInterval));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.MaxRuntime));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.MaxRetries));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.RetryPeriod));
        Assert.False(annotations.ContainsKey(RetentionPolicyAnnotations.InitialStart));
    }

    #endregion
}
#pragma warning restore EF1001

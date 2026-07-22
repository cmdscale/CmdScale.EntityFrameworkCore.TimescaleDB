#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators.AnnotationRenderers;

/// <summary>
/// Tests for <c>ReorderPolicyAnnotationRenderer</c> exercised through the public
/// <see cref="TimescaleDbAnnotationCodeGenerator"/> surface.
/// The renderer runs AFTER the hypertable renderer and uses the ABSENCE of <c>IsHypertable</c>
/// (i.e. it was consumed by the hypertable renderer) to decide whether to emit reorder policy code.
/// </summary>
public class ReorderPolicyAnnotationRendererTests
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

    private class ReorderRendererEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ReorderRendererContext : DbContext
    {
        public DbSet<ReorderRendererEntity> Items => Set<ReorderRendererEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReorderRendererEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("reorder_renderer_entity");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
    }

    // ── Guard tests (fluent) ──────────────────────────────────────────────────

    #region GenerateFluentApiCalls_Returns_Empty_When_HasReorderPolicy_Absent

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_HasReorderPolicy_Absent()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_guard_fluent"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithReorderPolicy"));
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Still_Present

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Still_Present()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_guard_ht"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithReorderPolicy"));
        Assert.True(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy),
            "HasReorderPolicy annotation must remain unconsumed when ShouldRender is false.");
        Assert.True(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName),
            "IndexName annotation must remain unconsumed when ShouldRender is false.");
    }

    #endregion

    #region GenerateFluentApiCalls_Renders_When_IsHypertable_Consumed

    [Fact]
    public void GenerateFluentApiCalls_Renders_When_IsHypertable_Consumed()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_renders_when_consumed"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("WithReorderPolicy"));
    }

    #endregion

    // ── Guard tests (attribute) ───────────────────────────────────────────────

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_HasReorderPolicy_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_HasReorderPolicy_Absent()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_da_guard_no_flag"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ReorderPolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Still_Present

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Still_Present()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_da_guard_ht"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ReorderPolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_And_Consumes_When_IndexName_Missing

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_And_Consumes_When_IndexName_Missing()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy),
            "HasReorderPolicy must be consumed even when IndexName is missing (blank-IndexName branch calls ConsumeAllReorderAnnotations).");
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Renders_When_IsHypertable_Consumed

    [Fact]
    public void GenerateDataAnnotationAttributes_Renders_When_IsHypertable_Consumed()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_da_renders"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.Contains(result, a => a.Type == typeof(ReorderPolicyAttribute));
    }

    #endregion

    // ── Fluent positional argument tests (always all 5 args) ─────────────────

    #region GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_IndexName_Only

    [Fact]
    public void GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_IndexName_Only()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_t"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withReorderPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithReorderPolicy");

        Assert.NotNull(withReorderPolicy);
        Assert.Equal(5, withReorderPolicy.Arguments.Count);
        Assert.Equal("ix_reorder_t", withReorderPolicy.Arguments[0]);
        Assert.All(withReorderPolicy.Arguments.Skip(1), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_ScheduleInterval_Provided

    [Fact]
    public void GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_ScheduleInterval_Provided()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_sched"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withReorderPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithReorderPolicy");

        Assert.NotNull(withReorderPolicy);
        Assert.Equal(5, withReorderPolicy.Arguments.Count);
        Assert.Equal("ix_reorder_sched", withReorderPolicy.Arguments[0]);
        Assert.Equal("2 days", withReorderPolicy.Arguments[1]);
        Assert.All(withReorderPolicy.Arguments.Skip(2), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_MaxRuntime_Provided

    [Fact]
    public void GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_MaxRuntime_Provided()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_maxrt"),
            (ReorderPolicyAnnotations.MaxRuntime, "01:00:00"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withReorderPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithReorderPolicy");

        Assert.NotNull(withReorderPolicy);
        Assert.Equal(5, withReorderPolicy.Arguments.Count);
        Assert.Equal("ix_reorder_maxrt", withReorderPolicy.Arguments[0]);
        Assert.Null(withReorderPolicy.Arguments[1]);
        Assert.Equal("01:00:00", withReorderPolicy.Arguments[2]);
        Assert.All(withReorderPolicy.Arguments.Skip(3), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_MaxRetries_Provided

    [Fact]
    public void GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_MaxRetries_Provided()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_maxret"),
            (ReorderPolicyAnnotations.MaxRetries, 5));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withReorderPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithReorderPolicy");

        Assert.NotNull(withReorderPolicy);
        Assert.Equal(5, withReorderPolicy.Arguments.Count);
        Assert.Equal("ix_reorder_maxret", withReorderPolicy.Arguments[0]);
        Assert.Null(withReorderPolicy.Arguments[1]);
        Assert.Null(withReorderPolicy.Arguments[2]);
        Assert.Equal(5, withReorderPolicy.Arguments[3]);
        Assert.Null(withReorderPolicy.Arguments[4]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_RetryPeriod_Provided

    [Fact]
    public void GenerateFluentApiCalls_WithReorderPolicy_Emits_All_Five_Args_When_RetryPeriod_Provided()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_retry"),
            (ReorderPolicyAnnotations.RetryPeriod, "00:10:00"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withReorderPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithReorderPolicy");

        Assert.NotNull(withReorderPolicy);
        Assert.Equal(5, withReorderPolicy.Arguments.Count);
        Assert.Equal("ix_reorder_retry", withReorderPolicy.Arguments[0]);
        Assert.Null(withReorderPolicy.Arguments[1]);
        Assert.Null(withReorderPolicy.Arguments[2]);
        Assert.Null(withReorderPolicy.Arguments[3]);
        Assert.Equal("00:10:00", withReorderPolicy.Arguments[4]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithReorderPolicy_FiveArgs_When_All_Five_Set

    [Fact]
    public void GenerateFluentApiCalls_WithReorderPolicy_FiveArgs_When_All_Five_Set()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_t"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"),
            (ReorderPolicyAnnotations.MaxRuntime, "01:00:00"),
            (ReorderPolicyAnnotations.MaxRetries, 3),
            (ReorderPolicyAnnotations.RetryPeriod, "00:10:00"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withReorderPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithReorderPolicy");

        Assert.NotNull(withReorderPolicy);
        Assert.Equal(5, withReorderPolicy.Arguments.Count);
        Assert.Equal("ix_t", withReorderPolicy.Arguments[0]);
        Assert.Equal("2 days", withReorderPolicy.Arguments[1]);
        Assert.Equal("01:00:00", withReorderPolicy.Arguments[2]);
        Assert.Equal(3, withReorderPolicy.Arguments[3]);
        Assert.Equal("00:10:00", withReorderPolicy.Arguments[4]);
    }

    #endregion

    // ── InitialStart chaining tests ───────────────────────────────────────────

    #region GenerateFluentApiCalls_Chains_WithInitialStart_When_Present

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithInitialStart_When_Present()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_chain_start"),
            (ReorderPolicyAnnotations.InitialStart, initialStart));

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
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_no_start"));

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
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_wrong_type"),
            (ReorderPolicyAnnotations.InitialStart, "2025-06-01T00:00:00Z"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithInitialStart"));
    }

    #endregion

    #region GenerateFluentApiCalls_Chain_Order_Is_WithReorderPolicy_Then_WithInitialStart

    [Fact]
    public void GenerateFluentApiCalls_Chain_Order_Is_WithReorderPolicy_Then_WithInitialStart()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_order"),
            (ReorderPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => CollectMethodChain(f).Contains("WithReorderPolicy"));
        List<string> chain = CollectMethodChain(root);

        int reorderIdx = chain.IndexOf("WithReorderPolicy");
        int initialStartIdx = chain.IndexOf("WithInitialStart");

        Assert.True(reorderIdx < initialStartIdx,
            "WithReorderPolicy must precede WithInitialStart in the chain.");
    }

    #endregion

    // ── Annotation consumption tests ──────────────────────────────────────────

    #region GenerateFluentApiCalls_Consumes_All_Reorder_Annotations_After_Render

    [Fact]
    public void GenerateFluentApiCalls_Consumes_All_Reorder_Annotations_After_Render()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_consume"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"),
            (ReorderPolicyAnnotations.MaxRuntime, "01:00:00"),
            (ReorderPolicyAnnotations.MaxRetries, 3),
            (ReorderPolicyAnnotations.RetryPeriod, "00:10:00"),
            (ReorderPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.InitialStart));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.ScheduleInterval));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.MaxRuntime));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.MaxRetries));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.RetryPeriod));
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Consume_When_ShouldRender_False

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Consume_When_ShouldRender_False()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_no_consume"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.True(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Removes_All_Keys_When_ShouldRender_True

    [Fact]
    public void ConsumeFeatureAnnotations_Removes_All_Keys_When_ShouldRender_True()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
            .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();
        generator.ScaffoldMode = true;
        generator.ScaffoldDataAnnotationsMode = true;

        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_consume_da_keys"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"));

        // Act
        generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Removes_All_Keys_Via_DataAnnotations_Mode

    [Fact]
    public void ConsumeFeatureAnnotations_Removes_All_Keys_Via_DataAnnotations_Mode()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        TimescaleDbAnnotationCodeGenerator generator = (TimescaleDbAnnotationCodeGenerator)services
            .BuildServiceProvider().GetRequiredService<IAnnotationCodeGenerator>();
        generator.ScaffoldMode = true;
        generator.ScaffoldDataAnnotationsMode = true;

        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_da_mode_consume"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"),
            (HypertableAnnotations.IsHypertable, true),
            (HypertableAnnotations.HypertableTimeColumn, "time"));

        // Act
        generator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.ScheduleInterval));
    }

    #endregion

    // ── Data-annotation attribute generation tests ────────────────────────────

    #region GenerateDataAnnotationAttributes_IndexName_As_Positional_Arg_0

    [Fact]
    public void GenerateDataAnnotationAttributes_IndexName_As_Positional_Arg_0()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_positional"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        object? positionalArg = Assert.Single(attr.Arguments);
        Assert.Equal("ix_reorder_positional", positionalArg);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_no_named"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Empty(attr.NamedArguments);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_ScheduleInterval_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_ScheduleInterval_As_Named_Arg()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_sched_named"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("2 days", attr.NamedArguments[nameof(ReorderPolicyAttribute.ScheduleInterval)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_MaxRuntime_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_MaxRuntime_As_Named_Arg()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_maxrt_named"),
            (ReorderPolicyAnnotations.MaxRuntime, "01:00:00"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("01:00:00", attr.NamedArguments[nameof(ReorderPolicyAttribute.MaxRuntime)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_MaxRetries_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_MaxRetries_As_Named_Arg()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_maxret_named"),
            (ReorderPolicyAnnotations.MaxRetries, 5));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal(5, attr.NamedArguments[nameof(ReorderPolicyAttribute.MaxRetries)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_RetryPeriod_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_RetryPeriod_As_Named_Arg()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_retry_named"),
            (ReorderPolicyAnnotations.RetryPeriod, "00:10:00"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("00:10:00", attr.NamedArguments[nameof(ReorderPolicyAttribute.RetryPeriod)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_InitialStart_As_ISO8601_UTC_String_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_InitialStart_As_ISO8601_UTC_String_Named_Arg()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_iso_start"),
            (ReorderPolicyAnnotations.InitialStart, initialStart));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        string? initialStartStr = attr.NamedArguments[nameof(ReorderPolicyAttribute.InitialStart)] as string;
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
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        DateTime localTime = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_local_start"),
            (ReorderPolicyAnnotations.InitialStart, localTime));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(ReorderPolicyAttribute));
        Assert.NotNull(attr);
        string? initialStartStr = attr.NamedArguments[nameof(ReorderPolicyAttribute.InitialStart)] as string;
        Assert.NotNull(initialStartStr);
        Assert.EndsWith("Z", initialStartStr, StringComparison.Ordinal);
        DateTime parsed = DateTime.Parse(initialStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(localTime.ToUniversalTime(), parsed);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Consumes_All_Reorder_Annotations_After_Render

    [Fact]
    public void GenerateDataAnnotationAttributes_Consumes_All_Reorder_Annotations_After_Render()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_consume_all"),
            (ReorderPolicyAnnotations.ScheduleInterval, "2 days"),
            (ReorderPolicyAnnotations.MaxRuntime, "01:00:00"),
            (ReorderPolicyAnnotations.MaxRetries, 3),
            (ReorderPolicyAnnotations.RetryPeriod, "00:10:00"),
            (ReorderPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        CreateAnnotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.InitialStart));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.ScheduleInterval));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.MaxRuntime));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.MaxRetries));
        Assert.False(annotations.ContainsKey(ReorderPolicyAnnotations.RetryPeriod));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Does_Not_Consume_When_ShouldRender_False

    [Fact]
    public void GenerateDataAnnotationAttributes_Does_Not_Consume_When_ShouldRender_False()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true),
            (ReorderPolicyAnnotations.IndexName, "ix_reorder_da_no_consume"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(ReorderPolicyAnnotations.HasReorderPolicy));
        Assert.True(annotations.ContainsKey(ReorderPolicyAnnotations.IndexName));
    }

    #endregion


    #region GenerateFluentApiCalls_Returns_Empty_When_IndexName_Missing

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_IndexName_Missing()
    {
        // Arrange
        using ReorderRendererContext context = new();
        IEntityType entityType = GetEntityType<ReorderRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (ReorderPolicyAnnotations.HasReorderPolicy, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithReorderPolicy"));
    }

    #endregion
}
#pragma warning restore EF1001

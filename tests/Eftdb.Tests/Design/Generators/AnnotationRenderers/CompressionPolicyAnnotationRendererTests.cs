using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy;
#pragma warning disable EF1001 // IOperationReporter and AnnotationCodeGeneratorDependencies are design-time internals.
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators.AnnotationRenderers;

/// <summary>
/// Tests for <c>CompressionPolicyAnnotationRenderer</c> exercised through the public
/// <see cref="TimescaleDbAnnotationCodeGenerator"/> surface.
/// The renderer requires <c>IsHypertable</c> to have already been consumed by the hypertable renderer.
/// It emits 5 positional args: (after, createdBefore, scheduleInterval, timezone, ifNotExists).
/// </summary>
public class CompressionPolicyAnnotationRendererTests
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

    private class CompressionRendererEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CompressionRendererContext : DbContext
    {
        public DbSet<CompressionRendererEntity> Items => Set<CompressionRendererEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressionRendererEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("compression_renderer_entity");
                e.Property(x => x.Time).HasColumnName("time");
                e.Property(x => x.Value).HasColumnName("value");
            });
    }

    // ── Guard tests ───────────────────────────────────────────────────────────

    #region GenerateFluentApiCalls_Returns_Empty_When_HasCompressionPolicy_Absent

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_HasCompressionPolicy_Absent()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.After, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithCompressionPolicy"));
    }

    #endregion

    #region GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Still_Present

    [Fact]
    public void GenerateFluentApiCalls_Returns_Empty_When_IsHypertable_Still_Present()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithCompressionPolicy"));
        Assert.True(annotations.ContainsKey(CompressionPolicyAnnotations.HasCompressionPolicy),
            "HasCompressionPolicy annotation must remain unconsumed when ShouldRender is false.");
        Assert.True(annotations.ContainsKey(CompressionPolicyAnnotations.After),
            "After annotation must remain unconsumed when ShouldRender is false.");
    }

    #endregion

    #region GenerateFluentApiCalls_Renders_When_IsHypertable_Consumed

    [Fact]
    public void GenerateFluentApiCalls_Renders_When_IsHypertable_Consumed()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.Contains(result, f => CollectMethodChain(f).Contains("WithCompressionPolicy"));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_HasCompressionPolicy_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_HasCompressionPolicy_Absent()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.After, "7 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(CompressionPolicyAttribute));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Still_Present

    [Fact]
    public void GenerateDataAnnotationAttributes_Returns_Empty_When_IsHypertable_Still_Present()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, a => a.Type == typeof(CompressionPolicyAttribute));
    }

    #endregion

    // ── Fluent positional argument tests (always all five args) ──────────────

    #region GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_Only_After

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_Only_After()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Equal("7 days", withCompressionPolicy.Arguments[0]);
        Assert.All(withCompressionPolicy.Arguments.Skip(1), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_Only_CreatedBefore

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_Only_CreatedBefore()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.CreatedBefore, "30 days"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Null(withCompressionPolicy.Arguments[0]);
        Assert.Equal("30 days", withCompressionPolicy.Arguments[1]);
        Assert.All(withCompressionPolicy.Arguments.Skip(2), Assert.Null);
    }

    #endregion

    #region GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_After_And_ScheduleInterval

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_After_And_ScheduleInterval()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.ScheduleInterval, "12 hours"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Equal("7 days", withCompressionPolicy.Arguments[0]);
        Assert.Null(withCompressionPolicy.Arguments[1]);
        Assert.Equal("12 hours", withCompressionPolicy.Arguments[2]);
        Assert.Null(withCompressionPolicy.Arguments[3]);
        Assert.Null(withCompressionPolicy.Arguments[4]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_After_And_Timezone

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_After_And_Timezone()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.Timezone, "Europe/Berlin"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Equal("7 days", withCompressionPolicy.Arguments[0]);
        Assert.Null(withCompressionPolicy.Arguments[1]);
        Assert.Null(withCompressionPolicy.Arguments[2]);
        Assert.Equal("Europe/Berlin", withCompressionPolicy.Arguments[3]);
        Assert.Null(withCompressionPolicy.Arguments[4]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_After_And_IfNotExists

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_Emits_FiveArgs_When_After_And_IfNotExists()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.IfNotExists, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Equal("7 days", withCompressionPolicy.Arguments[0]);
        Assert.Null(withCompressionPolicy.Arguments[1]);
        Assert.Null(withCompressionPolicy.Arguments[2]);
        Assert.Null(withCompressionPolicy.Arguments[3]);
        Assert.Equal(true, withCompressionPolicy.Arguments[4]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithCompressionPolicy_FiveArgs_When_All_Five_Set

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_FiveArgs_When_All_Five_Set()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "14 days"),
            (CompressionPolicyAnnotations.ScheduleInterval, "12 hours"),
            (CompressionPolicyAnnotations.Timezone, "UTC"),
            (CompressionPolicyAnnotations.IfNotExists, true));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Equal("14 days", withCompressionPolicy.Arguments[0]);
        Assert.Null(withCompressionPolicy.Arguments[1]);
        Assert.Equal("12 hours", withCompressionPolicy.Arguments[2]);
        Assert.Equal("UTC", withCompressionPolicy.Arguments[3]);
        Assert.Equal(true, withCompressionPolicy.Arguments[4]);
    }

    #endregion

    #region GenerateFluentApiCalls_WithCompressionPolicy_FiveArgs_When_CreatedBefore_And_Timezone

    [Fact]
    public void GenerateFluentApiCalls_WithCompressionPolicy_FiveArgs_When_CreatedBefore_And_Timezone()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.CreatedBefore, "30 days"),
            (CompressionPolicyAnnotations.Timezone, "America/New_York"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withCompressionPolicy = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithCompressionPolicy");

        Assert.NotNull(withCompressionPolicy);
        Assert.Equal(5, withCompressionPolicy.Arguments.Count);
        Assert.Null(withCompressionPolicy.Arguments[0]);
        Assert.Equal("30 days", withCompressionPolicy.Arguments[1]);
        Assert.Null(withCompressionPolicy.Arguments[2]);
        Assert.Equal("America/New_York", withCompressionPolicy.Arguments[3]);
        Assert.Null(withCompressionPolicy.Arguments[4]);
    }

    #endregion

    // ── InitialStart chaining tests ───────────────────────────────────────────

    #region GenerateFluentApiCalls_Chains_WithInitialStart_When_Present

    [Fact]
    public void GenerateFluentApiCalls_Chains_WithInitialStart_When_Present()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.InitialStart, initialStart));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment? withInitialStart = result
            .SelectMany(f => FlattenChain(f))
            .FirstOrDefault(f => f.Method == "WithInitialStart");

        Assert.NotNull(withInitialStart);
        Assert.Equal(initialStart, withInitialStart.Arguments[0]);
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Chain_WithInitialStart_When_Absent

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Chain_WithInitialStart_When_Absent()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"));

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
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.InitialStart, "2025-06-01T00:00:00Z"));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.DoesNotContain(result, f => CollectMethodChain(f).Contains("WithInitialStart"));
    }

    #endregion

    #region GenerateFluentApiCalls_Chain_Order_Is_WithCompressionPolicy_Then_WithInitialStart

    [Fact]
    public void GenerateFluentApiCalls_Chain_Order_Is_WithCompressionPolicy_Then_WithInitialStart()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Act
        IReadOnlyList<MethodCallCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateFluentApiCalls(entityType, annotations);

        // Assert
        MethodCallCodeFragment root = Assert.Single(result, f => CollectMethodChain(f).Contains("WithCompressionPolicy"));
        List<string> chain = CollectMethodChain(root);

        int compressionIdx = chain.IndexOf("WithCompressionPolicy");
        int initialStartIdx = chain.IndexOf("WithInitialStart");

        Assert.True(compressionIdx < initialStartIdx,
            "WithCompressionPolicy must precede WithInitialStart in the chain.");
    }

    #endregion

    // ── Annotation consumption tests ──────────────────────────────────────────

    #region GenerateFluentApiCalls_Consumes_All_Compression_Annotations_After_Render

    [Fact]
    public void GenerateFluentApiCalls_Consumes_All_Compression_Annotations_After_Render()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.ScheduleInterval, "12 hours"),
            (CompressionPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (CompressionPolicyAnnotations.Timezone, "UTC"),
            (CompressionPolicyAnnotations.IfNotExists, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.HasCompressionPolicy));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.After));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.ScheduleInterval));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.InitialStart));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.Timezone));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.IfNotExists));
    }

    #endregion

    #region GenerateFluentApiCalls_Does_Not_Consume_When_ShouldRender_False

    [Fact]
    public void GenerateFluentApiCalls_Does_Not_Consume_When_ShouldRender_False()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(CompressionPolicyAnnotations.HasCompressionPolicy));
        Assert.True(annotations.ContainsKey(CompressionPolicyAnnotations.After));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Removes_All_Keys_When_ShouldRender_True

    [Fact]
    public void ConsumeFeatureAnnotations_Removes_All_Keys_When_ShouldRender_True()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.ScheduleInterval, "12 hours"));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.HasCompressionPolicy));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.After));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.ScheduleInterval));
    }

    #endregion

    #region ConsumeFeatureAnnotations_Leaves_Keys_When_ShouldRender_False

    [Fact]
    public void ConsumeFeatureAnnotations_Leaves_Keys_When_ShouldRender_False()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (HypertableAnnotations.IsHypertable, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateFluentApiCalls(entityType, annotations);

        // Assert
        Assert.True(annotations.ContainsKey(CompressionPolicyAnnotations.HasCompressionPolicy));
        Assert.True(annotations.ContainsKey(CompressionPolicyAnnotations.After));
    }

    #endregion

    // ── Data-annotation attribute generation tests ────────────────────────────

    #region GenerateDataAnnotationAttributes_After_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_After_As_Named_Arg()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("7 days", attr.NamedArguments[nameof(CompressionPolicyAttribute.After)]);
        Assert.Empty(attr.Arguments);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_CreatedBefore_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_CreatedBefore_As_Named_Arg()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.CreatedBefore, "30 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("30 days", attr.NamedArguments[nameof(CompressionPolicyAttribute.CreatedBefore)]);
        Assert.False(attr.NamedArguments.ContainsKey(nameof(CompressionPolicyAttribute.After)));
        Assert.Empty(attr.Arguments);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_ScheduleInterval_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_ScheduleInterval_As_Named_Arg()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.ScheduleInterval, "12 hours"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("12 hours", attr.NamedArguments[nameof(CompressionPolicyAttribute.ScheduleInterval)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Timezone_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_Timezone_As_Named_Arg()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.Timezone, "Europe/Berlin"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal("Europe/Berlin", attr.NamedArguments[nameof(CompressionPolicyAttribute.Timezone)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_IfNotExists_As_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_IfNotExists_As_Named_Arg()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.IfNotExists, true));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.Equal(true, attr.NamedArguments[nameof(CompressionPolicyAttribute.IfNotExists)]);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_InitialStart_As_ISO8601_UTC_String_Named_Arg

    [Fact]
    public void GenerateDataAnnotationAttributes_InitialStart_As_ISO8601_UTC_String_Named_Arg()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.InitialStart, initialStart));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        string? initialStartStr = attr.NamedArguments[nameof(CompressionPolicyAttribute.InitialStart)] as string;
        Assert.NotNull(initialStartStr);
        Assert.EndsWith("Z", initialStartStr, StringComparison.Ordinal);
        DateTime parsed = DateTime.Parse(initialStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(initialStart, parsed);
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent

    [Fact]
    public void GenerateDataAnnotationAttributes_Omits_Named_Args_When_Optional_Annotations_Absent()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"));

        // Act
        IReadOnlyList<AttributeCodeFragment> result = CreateAnnotationCodeGenerator
            .GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        AttributeCodeFragment? attr = result.FirstOrDefault(a => a.Type == typeof(CompressionPolicyAttribute));
        Assert.NotNull(attr);
        Assert.False(attr.NamedArguments.ContainsKey(nameof(CompressionPolicyAttribute.ScheduleInterval)));
        Assert.False(attr.NamedArguments.ContainsKey(nameof(CompressionPolicyAttribute.InitialStart)));
        Assert.False(attr.NamedArguments.ContainsKey(nameof(CompressionPolicyAttribute.Timezone)));
        Assert.False(attr.NamedArguments.ContainsKey(nameof(CompressionPolicyAttribute.IfNotExists)));
    }

    #endregion

    #region GenerateDataAnnotationAttributes_Consumes_All_Compression_Annotations_After_Render

    [Fact]
    public void GenerateDataAnnotationAttributes_Consumes_All_Compression_Annotations_After_Render()
    {
        // Arrange
        using CompressionRendererContext context = new();
        IEntityType entityType = GetEntityType<CompressionRendererEntity>(context);
        Dictionary<string, IAnnotation> annotations = Annotations(
            (CompressionPolicyAnnotations.HasCompressionPolicy, true),
            (CompressionPolicyAnnotations.After, "7 days"),
            (CompressionPolicyAnnotations.ScheduleInterval, "12 hours"),
            (CompressionPolicyAnnotations.InitialStart, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (CompressionPolicyAnnotations.Timezone, "UTC"),
            (CompressionPolicyAnnotations.IfNotExists, true));

        // Act
        CreateAnnotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations);

        // Assert
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.HasCompressionPolicy));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.After));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.ScheduleInterval));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.InitialStart));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.Timezone));
        Assert.False(annotations.ContainsKey(CompressionPolicyAnnotations.IfNotExists));
    }

    #endregion
}
#pragma warning restore EF1001

#pragma warning disable EF1001
using System.Reflection;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

public class TimescaleCSharpModelGeneratorTests
{
    private static IModelCodeGeneratorSelector CreateSelector()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        return services.BuildServiceProvider().GetRequiredService<IModelCodeGeneratorSelector>();
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

    // ── Selector tests ─────────────────────────────────────────────────────

    #region Select_Returns_TimescaleCSharpModelGenerator_When_BaseReturns_CSharpModelGenerator

    [Fact]
    public void Select_Returns_TimescaleCSharpModelGenerator_When_BaseReturns_CSharpModelGenerator()
    {
        IModelCodeGeneratorSelector selector = CreateSelector();

        IModelCodeGenerator generator = selector.Select(DefaultOptions());

        Assert.IsType<TimescaleCSharpModelGenerator>(generator);
    }

    #endregion

    #region Select_Returns_CSharpModelGenerator_When_TimescaleCSharpModelGenerator_Not_In_Services

    [Fact]
    public void Select_Returns_CSharpModelGenerator_When_TimescaleCSharpModelGenerator_Not_In_Services()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        IModelCodeGeneratorSelector selector = services.BuildServiceProvider()
            .GetRequiredService<IModelCodeGeneratorSelector>();

        IModelCodeGenerator generator = selector.Select(DefaultOptions());

        Assert.NotNull(generator);
        Assert.IsType<CSharpModelGenerator>(generator);
    }

    #endregion

    #region Select_TimescaleSelector_Falls_Back_To_CSharpModelGenerator_When_Timescale_Not_In_Services

    [Fact]
    public void Select_TimescaleSelector_Falls_Back_To_CSharpModelGenerator_When_Timescale_Not_In_Services()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        IServiceProvider sp = services.BuildServiceProvider();

        IEnumerable<IModelCodeGenerator> generators = sp.GetServices<IModelCodeGenerator>();

        TimescaleModelCodeGeneratorSelector selector = new(generators);

        // Act
        IModelCodeGenerator result = selector.Select(DefaultOptions());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(typeof(CSharpModelGenerator), result.GetType());
    }

    #endregion

    // ── GenerateModel tests ────────────────────────────────────────────────

    #region GenerateModel_UseDataAnnotations_False_Does_Not_Inject_Timescale_Usings

    private class NoAnnotationsEntity { public DateTime Ts { get; set; } }

    [Hypertable(nameof(Ts))]
    private class HypertableAnnotatedEntityFalse { public DateTime Ts { get; set; } }

    private class GenerateModelFalseContext : DbContext
    {
        public DbSet<HypertableAnnotatedEntityFalse> Items => Set<HypertableAnnotatedEntityFalse>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HypertableAnnotatedEntityFalse>(e => { e.HasNoKey(); e.ToTable("gen_model_false"); });
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_False_Does_Not_Inject_Timescale_Usings()
    {
        using GenerateModelFalseContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: false);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);

        Assert.All(result.AdditionalFiles, file =>
            Assert.DoesNotContain("using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;", file.Code));
    }

    #endregion

    #region GenerateModel_UseDataAnnotations_True_Injects_Hypertable_Namespace

    [Hypertable(nameof(Ts))]
    private class HypertableAnnotatedEntityTrue { public DateTime Ts { get; set; } }

    private class GenerateModelTrueContext : DbContext
    {
        public DbSet<HypertableAnnotatedEntityTrue> Items => Set<HypertableAnnotatedEntityTrue>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HypertableAnnotatedEntityTrue>(e => { e.HasNoKey(); e.ToTable("gen_model_true"); });
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_True_Injects_Hypertable_Namespace()
    {
        using GenerateModelTrueContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);

        bool found = result.AdditionalFiles.Any(f =>
            f.Code.Contains("using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;"));
        Assert.True(found);
    }

    #endregion


    #region GenerateModel_UseDataAnnotations_True_DoesNotEmitHypertableFluentApiCalls

    [Hypertable(nameof(Ts))]
    private class HypertableWithChunkEntity { public DateTime Ts { get; set; } }

    private class HtChunkContext : DbContext
    {
        public DbSet<HypertableWithChunkEntity> Items => Set<HypertableWithChunkEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HypertableWithChunkEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("ht_chunk_da_test");
                e.WithChunkTimeInterval("1 day");
            });
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_True_DoesNotEmitHypertableFluentApiCalls()
    {
        using HtChunkContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);
        Assert.DoesNotContain("WithChunkTimeInterval", result.ContextFile.Code);
    }

    #endregion


    #region GenerateModel_UseDataAnnotations_True_Injects_Aggregate_Namespaces

    private class CaAggSourceEntity { public DateTime Time { get; set; } public double Price { get; set; } }

    private class CaAggViewEntity { public double MaxPrice { get; set; } }

    private class CaAggNamespaceContext : DbContext
    {
        public DbSet<CaAggSourceEntity> Sources => Set<CaAggSourceEntity>();
        public DbSet<CaAggViewEntity> Views => Set<CaAggViewEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaAggSourceEntity>(e =>
            {
                e.HasKey(x => x.Time);
                e.ToTable("ca_agg_ns_source");
                e.Property(x => x.Price).HasColumnName("price");
            });
            modelBuilder.Entity<CaAggViewEntity>(e =>
            {
                e.HasNoKey();
                e.ToView("ca_agg_ns_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.MaterializedViewName, "ca_agg_ns_view");
                e.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "ca_agg_ns_source");
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions,
                    new List<string> { "MaxPrice:Max:Price" });
                e.Property(x => x.MaxPrice).HasColumnName("max_price");
            });
        }
    }

    [Fact]
    public void GenerateModel_UseDataAnnotations_True_Injects_Aggregate_Namespaces()
    {
        using CaAggNamespaceContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        ScaffoldedModel result = generator.GenerateModel(model, options);

        bool hasAbstractionsNs = result.AdditionalFiles.Any(f =>
            f.Code.Contains($"using {typeof(EAggregateFunction).Namespace};"));
        bool hasAggregateAttrNs = result.AdditionalFiles.Any(f =>
            f.Code.Contains($"using {typeof(AggregateAttribute).Namespace};"));

        Assert.True(hasAbstractionsNs, "Expected EAggregateFunction namespace in entity file");
        Assert.True(hasAggregateAttrNs, "Expected AggregateAttribute namespace in entity file");
    }

    #endregion


    #region Select_Returns_NonCSharpModelGenerator_Unchanged

    private const string TestLanguage = "TestLang";

    private class TestLangModelGenerator : IModelCodeGenerator
    {
        public string Language => TestLanguage;

        public ScaffoldedModel GenerateModel(IModel model, ModelCodeGenerationOptions options) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void Select_Returns_NonCSharpModelGenerator_Unchanged()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        services.AddSingleton<IModelCodeGenerator, TestLangModelGenerator>();
        IServiceProvider sp = services.BuildServiceProvider();
        IModelCodeGeneratorSelector selector = sp.GetRequiredService<IModelCodeGeneratorSelector>();
        ModelCodeGenerationOptions options = new() { Language = TestLanguage };

        // Act
        IModelCodeGenerator generator = selector.Select(options);

        // Assert
        Assert.IsType<TestLangModelGenerator>(generator);
    }

    #endregion


    #region GenerateModel_NonTimescaleAnnotationGenerator_Does_Not_Throw

    private class NonTsAnnotationSimpleEntity { public DateTime Ts { get; set; } }

    private class NonTsAnnotationContext : DbContext
    {
        public DbSet<NonTsAnnotationSimpleEntity> Items => Set<NonTsAnnotationSimpleEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NonTsAnnotationSimpleEntity>(e => { e.HasNoKey(); e.ToTable("non_ts_annotation_gen"); });
    }

    [Fact]
    public void GenerateModel_NonTimescaleAnnotationGenerator_Does_Not_Throw()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        IServiceProvider sp = services.BuildServiceProvider();

        Mock<IAnnotationCodeGenerator> mockAnnotationGen = new();
        mockAnnotationGen
            .Setup(g => g.GenerateDataAnnotationAttributes(It.IsAny<IEntityType>(), It.IsAny<IDictionary<string, IAnnotation>>()))
            .Returns([]);
        mockAnnotationGen
            .Setup(g => g.GenerateDataAnnotationAttributes(It.IsAny<IProperty>(), It.IsAny<IDictionary<string, IAnnotation>>()))
            .Returns([]);

        ModelCodeGeneratorDependencies deps = sp.GetRequiredService<ModelCodeGeneratorDependencies>();
        IOperationReporter reporter = sp.GetRequiredService<IOperationReporter>();

        TimescaleCSharpModelGenerator generator = new(deps, reporter, sp, mockAnnotationGen.Object);

        using NonTsAnnotationContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: false);

        // Act
        ScaffoldedModel result = generator.GenerateModel(model, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ContextFile);
    }

    #endregion


    #region GenerateModel_Does_Not_Include_Design_Namespace_In_Output

    private class NoDesignNsEntity { public DateTime Ts { get; set; } }

    private class NoDesignNsContext : DbContext
    {
        public DbSet<NoDesignNsEntity> Items => Set<NoDesignNsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoDesignNsEntity>(e => { e.HasNoKey(); e.ToTable("no_design_ns_entity"); });
    }

    [Fact]
    public void GenerateModel_Does_Not_Include_Design_Namespace_In_Output()
    {
        // Arrange
        using NoDesignNsContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        // Act
        ScaffoldedModel result = generator.GenerateModel(model, options);

        // Assert
        const string designNamespacePrefix = "using CmdScale.EntityFrameworkCore.TimescaleDB.Design";
        Assert.DoesNotContain(designNamespacePrefix, result.ContextFile.Code);
        Assert.All(result.AdditionalFiles, file =>
            Assert.DoesNotContain(designNamespacePrefix, file.Code));
    }

    #endregion


    #region GenerateModel_DoesNotDuplicate_ContextUsings

    private class NoDuplicateUsingsEntity { public DateTime Ts { get; set; } }

    private class NoDuplicateUsingsContext : DbContext
    {
        public DbSet<NoDuplicateUsingsEntity> Items => Set<NoDuplicateUsingsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoDuplicateUsingsEntity>(e => { e.HasNoKey(); e.ToTable("no_dup_usings_entity"); });
    }

    [Fact]
    public void GenerateModel_DoesNotDuplicate_ContextUsings()
    {
        // Arrange
        using NoDuplicateUsingsContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IModelCodeGeneratorSelector selector = CreateSelector();
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);
        IModelCodeGenerator generator = selector.Select(options);

        // Act
        ScaffoldedModel result = generator.GenerateModel(model, options);

        // Assert
        string contextCode = result.ContextFile.Code;
        string newLine = contextCode.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        IEnumerable<string> usingLines = contextCode.Split(newLine)
            .Where(line => line.StartsWith("using ", StringComparison.Ordinal) && line.EndsWith(';'));
        IEnumerable<IGrouping<string, string>> duplicateGroups = usingLines
            .GroupBy(line => line)
            .Where(g => g.Count() > 1);
        Assert.Empty(duplicateGroups);
    }

    #endregion


    #region AddArgumentNamespace_NonEnum_NonType_Argument_Does_Not_Add_Namespace

    [Fact]
    public void AddArgumentNamespace_NonEnum_NonType_Argument_Does_Not_Add_Namespace()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("AddArgumentNamespace", BindingFlags.NonPublic | BindingFlags.Static)!;

        List<string> namespaces = ["System"];
        string argument = "plain_string_source";

        // Act
        method.Invoke(null, [namespaces, argument]);

        // Assert
        Assert.Equal(["System"], namespaces);
    }

    #endregion


    #region AddNamespace_Does_Not_Duplicate_Namespace_When_Already_Present

    [Fact]
    public void AddNamespace_Does_Not_Duplicate_Namespace_When_Already_Present()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("AddNamespace", BindingFlags.NonPublic | BindingFlags.Static)!;

        string ns = typeof(EAggregateFunction).Namespace!;
        List<string> namespaces = [ns];

        // Act
        method.Invoke(null, [namespaces, typeof(EAggregateFunction)]);

        // Assert
        Assert.Equal(ns, Assert.Single(namespaces));
    }

    #endregion


    #region AddArgumentNamespace_Type_Argument_Adds_Namespace

    [Fact]
    public void AddArgumentNamespace_Type_Argument_Adds_Namespace()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("AddArgumentNamespace", BindingFlags.NonPublic | BindingFlags.Static)!;

        List<string> namespaces = [];
        Type typeArgument = typeof(EAggregateFunction);

        // Act
        method.Invoke(null, [namespaces, typeArgument]);

        // Assert
        Assert.Contains(typeArgument.Namespace!, namespaces);
    }

    #endregion


    #region RemoveDesignUsings_Should_Remove_Design_Using_When_Code_Has_LF_Only_Line_Endings

    [Fact]
    public void RemoveDesignUsings_Should_Remove_Design_Using_When_Code_Has_LF_Only_Line_Endings()
    {
        // Arrange
        string code =
            "using CmdScale.EntityFrameworkCore.TimescaleDB.Design;\n" +
            "using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;\n" +
            "using System;\n" +
            "\n" +
            "namespace MyApp { }";

        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("RemoveDesignUsings", BindingFlags.NonPublic | BindingFlags.Static)!;

        // Act
        string result = (string)method.Invoke(null, [code])!;

        // Assert
        Assert.DoesNotContain("using CmdScale.EntityFrameworkCore.TimescaleDB.Design;", result);
        Assert.DoesNotContain("using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;", result);
        Assert.Contains("using System;", result);
        Assert.Contains("namespace MyApp { }", result);
    }

    #endregion


    #region AddMissingUsings_Should_Not_Duplicate_Usings_When_Already_Present

    [Fact]
    public void AddMissingUsings_Should_Not_Duplicate_Usings_When_Already_Present()
    {
        // Arrange
        string ns = typeof(TimescaleDbContextOptionsBuilderExtensions).Namespace!;
        string code =
            $"using {ns};\r\n" +
            "using System;\r\n" +
            "\r\n" +
            "namespace MyApp { }";

        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("AddMissingUsings", BindingFlags.NonPublic | BindingFlags.Static)!;

        // Act
        string result = (string)method.Invoke(null, [code, new List<string> { ns }])!;

        // Assert
        Assert.Equal(code, result);
    }

    #endregion


    #region AddMissingUsings_Should_Prepend_Usings_When_No_Using_Block_Exists

    [Fact]
    public void AddMissingUsings_Should_Prepend_Usings_When_No_Using_Block_Exists()
    {
        // Arrange
        string ns = typeof(TimescaleDbContextOptionsBuilderExtensions).Namespace!;
        string code =
            "namespace MyApp\n" +
            "{\n" +
            "    class Foo { }\n" +
            "}";

        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("AddMissingUsings", BindingFlags.NonPublic | BindingFlags.Static)!;

        // Act
        string result = (string)method.Invoke(null, [code, new List<string> { ns }])!;

        // Assert
        Assert.Contains($"using {ns};", result);
        int usingIndex = result.IndexOf($"using {ns};", StringComparison.Ordinal);
        int namespaceIndex = result.IndexOf("namespace MyApp", StringComparison.Ordinal);
        Assert.True(usingIndex < namespaceIndex, "using directive must appear before namespace declaration");
    }

    #endregion


    #region AddMissingUsings_Should_Insert_Usings_Into_LF_Only_Code

    [Fact]
    public void AddMissingUsings_Should_Insert_Usings_Into_LF_Only_Code()
    {
        // Arrange
        string ns = typeof(TimescaleDbContextOptionsBuilderExtensions).Namespace!;
        string code =
            "using System;\n" +
            "\n" +
            "namespace MyApp { }";

        MethodInfo method = typeof(TimescaleCSharpModelGenerator)
            .GetMethod("AddMissingUsings", BindingFlags.NonPublic | BindingFlags.Static)!;

        // Act
        string result = (string)method.Invoke(null, [code, new List<string> { ns }])!;

        // Assert
        Assert.Contains($"using {ns};", result);
        Assert.Contains("using System;", result);
        Assert.DoesNotContain("\r\n", result);
    }

    #endregion


    #region GenerateModel_NonTimescaleAnnotationGenerator_Returns_Valid_Context_File

    private class NonTsAnnotationFallbackEntity { public DateTime Ts { get; set; } }

    private class NonTsAnnotationFallbackContext : DbContext
    {
        public DbSet<NonTsAnnotationFallbackEntity> Items => Set<NonTsAnnotationFallbackEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NonTsAnnotationFallbackEntity>(e => { e.HasNoKey(); e.ToTable("non_ts_fallback_entity"); });
    }

    [Fact]
    public void GenerateModel_NonTimescaleAnnotationGenerator_Returns_Valid_Context_File()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        IServiceProvider sp = services.BuildServiceProvider();

        Mock<IAnnotationCodeGenerator> mockAnnotationGen = new();
        mockAnnotationGen
            .Setup(g => g.GenerateDataAnnotationAttributes(It.IsAny<IEntityType>(), It.IsAny<IDictionary<string, IAnnotation>>()))
            .Returns([]);
        mockAnnotationGen
            .Setup(g => g.GenerateDataAnnotationAttributes(It.IsAny<IProperty>(), It.IsAny<IDictionary<string, IAnnotation>>()))
            .Returns([]);

        ModelCodeGeneratorDependencies deps = sp.GetRequiredService<ModelCodeGeneratorDependencies>();
        IOperationReporter reporter = sp.GetRequiredService<IOperationReporter>();
        TimescaleCSharpModelGenerator generator = new(deps, reporter, sp, mockAnnotationGen.Object);

        using NonTsAnnotationFallbackContext context = new();
        IModel model = context.GetService<IDesignTimeModel>().Model;
        ModelCodeGenerationOptions options = DefaultOptions(useDataAnnotations: true);

        // Act
        ScaffoldedModel result = generator.GenerateModel(model, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ContextFile);
        Assert.NotEmpty(result.ContextFile.Code);
    }

    #endregion
}
#pragma warning restore EF1001

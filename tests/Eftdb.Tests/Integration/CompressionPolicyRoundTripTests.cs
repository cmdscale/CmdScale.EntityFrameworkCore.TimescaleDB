#pragma warning disable EF1001 // IScaffoldingModelFactory is an EF Core internal API, used here to drive the full scaffold-to-IModel pipeline.

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Reflection;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Validates that the compression policy scaffolding roundtrip is lossless.
/// A code-first reference database is created via migrations, scaffolded back to C# with
/// the design-time pipeline, and the generated code is compiled in-memory with Roslyn.
/// A phantom-migration check verifies the differ produces zero operations between
/// the reference model and the scaffolded model.
/// </summary>
public sealed class CompressionPolicyRoundTripTests : MigrationTestBase, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:latest-pg17")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Reference-model entity types

    private class CompressAfterEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressCreatedBeforeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressScheduleEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    #endregion

    private sealed class CompressionRoundTripReferenceContext(string connectionString) : DbContext
    {
        public DbSet<CompressAfterEntity> CompressAfterMetrics => Set<CompressAfterEntity>();
        public DbSet<CompressCreatedBeforeEntity> CompressCreatedBeforeMetrics => Set<CompressCreatedBeforeEntity>();
        public DbSet<CompressScheduleEntity> CompressScheduleMetrics => Set<CompressScheduleEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressAfterEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_cp_after");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(
                          after: "7 days",
                          scheduleInterval: "12 hours",
                          initialStart: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            });

            modelBuilder.Entity<CompressCreatedBeforeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_cp_created_before");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(createdBefore: "30 days");
            });

            modelBuilder.Entity<CompressScheduleEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_cp_schedule");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionOrderBy(s => s.By(x => x.Timestamp))
                      .WithCompressionPolicy(after: "14 days", scheduleInterval: "6 hours");
            });
        }
    }

    // ── Design-time helpers ───────────────────────────────────────────────────

    private static async Task<string> CreateIsolatedDatabaseAsync(string adminConnectionString)
    {
        string dbName = $"rrt_cp_{Guid.NewGuid():N}";

        await using NpgsqlConnection admin = new(adminConnectionString);
        await admin.OpenAsync();
        await using (NpgsqlCommand cmd = new($"CREATE DATABASE {dbName}", admin))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        string isolated = adminConnectionString.Replace("test_db", dbName, StringComparison.OrdinalIgnoreCase);
        await using NpgsqlConnection conn = new(isolated);
        await conn.OpenAsync();
        await using (NpgsqlCommand ext = new("CREATE EXTENSION IF NOT EXISTS timescaledb", conn))
        {
            await ext.ExecuteNonQueryAsync();
        }

        return isolated;
    }

    private static ServiceProvider CreateDesignServices()
    {
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        return services.BuildServiceProvider();
    }

    private static ModelCodeGenerationOptions MakeCodeGenOptions(
        string connectionString,
        bool useDataAnnotations,
        bool suppressOnConfiguring = false) => new()
        {
            Language = "C#",
            UseDataAnnotations = useDataAnnotations,
            ProjectDir = ".",
            ModelNamespace = "CpRoundTripScaffold",
            ContextName = "CpRoundTripDbContext",
            ContextNamespace = "CpRoundTripScaffold",
            ConnectionString = connectionString,
            SuppressOnConfiguring = suppressOnConfiguring
        };

    private static (IModel Model, ScaffoldedModel ScaffoldedCode) ScaffoldDatabase(
        string connectionString,
        bool useDataAnnotations,
        bool suppressOnConfiguring = false)
    {
        using ServiceProvider sp = CreateDesignServices();

        IDatabaseModelFactory dbFactory = sp.GetRequiredService<IDatabaseModelFactory>();
        using NpgsqlConnection conn = new(connectionString);
        DatabaseModel dbModel = dbFactory.Create(conn, new DatabaseModelFactoryOptions());

        IScaffoldingModelFactory scaffoldingModelFactory = sp.GetRequiredService<IScaffoldingModelFactory>();
        IModel model = scaffoldingModelFactory.Create(dbModel, new ModelReverseEngineerOptions());

        ModelCodeGenerationOptions codeGenOptions = MakeCodeGenOptions(connectionString, useDataAnnotations, suppressOnConfiguring);
        using ServiceProvider sp2 = CreateDesignServices();
        IModelCodeGenerator codeGenerator = sp2.GetRequiredService<IModelCodeGeneratorSelector>().Select(codeGenOptions);
        ScaffoldedModel scaffoldedCode = codeGenerator.GenerateModel(model, codeGenOptions);

        return (model, scaffoldedCode);
    }

    private static byte[] CompileAndAssertNoErrors(ScaffoldedModel scaffolded, string label)
    {
        IEnumerable<string> trustedPlatformAssemblies =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        IEnumerable<string> loadedAssemblyPaths =
            AppDomain.CurrentDomain
                     .GetAssemblies()
                     .Select(a => a.Location)
                     .Where(loc => !string.IsNullOrWhiteSpace(loc));

        IEnumerable<MetadataReference> references =
            trustedPlatformAssemblies
                .Concat(loadedAssemblyPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(File.Exists)
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));

        IEnumerable<string> sources =
            [scaffolded.ContextFile.Code, .. scaffolded.AdditionalFiles.Select(f => f.Code)];

        CSharpParseOptions parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        IEnumerable<SyntaxTree> trees = sources.Select(src => CSharpSyntaxTree.ParseText(src, parseOptions));

        CSharpCompilation compilation = CSharpCompilation.Create(
            $"CpRoundTripScaffold_{label}",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        using MemoryStream pe = new();
        Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(pe);

        IReadOnlyList<Diagnostic> errors =
            [.. emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];

        Assert.True(
            errors.Count == 0,
            $"[{label}] Scaffolded code has {errors.Count} compilation error(s):\n" +
            string.Join("\n", errors.Select(e => e.ToString())));

        return pe.ToArray();
    }

    private static void AssertZeroDiff(
        IRelationalModel source,
        IRelationalModel target,
        string direction)
    {
        IFeatureDiffer[] differs =
        [
            new HypertableDiffer(),
            new CompressionPolicyDiffer(),
        ];

        List<string> failures = [];
        foreach (IFeatureDiffer differ in differs)
        {
            IReadOnlyList<MigrationOperation> ops = differ.GetDifferences(source, target);
            if (ops.Count > 0)
            {
                string opList = string.Join(", ", ops.Select(op =>
                {
                    PropertyInfo? nameProp = op.GetType().GetProperty("TableName");
                    string name = nameProp?.GetValue(op)?.ToString() ?? "?";
                    return $"{op.GetType().Name}({name})";
                }));
                failures.Add($"  {differ.GetType().Name}: {ops.Count} op(s) — {opList}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Feature differs produced non-zero operations [{direction}]:\n" +
            string.Join("\n", failures));
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    #region Should_RoundTrip_CompressionPolicy_With_Zero_Diff_In_Fluent_Mode

    [Fact]
    public async Task Should_RoundTrip_CompressionPolicy_With_Zero_Diff_In_Fluent_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (IModel modelA, ScaffoldedModel codeA) = ScaffoldDatabase(isolated, useDataAnnotations: false);
        (IModel modelB, ScaffoldedModel _) = ScaffoldDatabase(isolated, useDataAnnotations: false);

        // Assert
        CompileAndAssertNoErrors(codeA, "fluent");

        IRelationalModel relA = modelA.GetRelationalModel();
        IRelationalModel relB = modelB.GetRelationalModel();

        AssertZeroDiff(relA, relB, "cp-fluent: pass-1 → pass-2");
        AssertZeroDiff(relB, relA, "cp-fluent: pass-2 → pass-1");

        IRelationalModel refRelModel = refCtx.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        AssertZeroDiff(refRelModel, relA, "cp-fluent: reference → scaffolded");
        AssertZeroDiff(relA, refRelModel, "cp-fluent: scaffolded → reference");
    }

    #endregion

    #region Should_RoundTrip_CompressionPolicy_With_Zero_Diff_In_DataAnnotations_Mode

    [Fact]
    public async Task Should_RoundTrip_CompressionPolicy_With_Zero_Diff_In_DataAnnotations_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (IModel modelA, ScaffoldedModel codeA) = ScaffoldDatabase(isolated, useDataAnnotations: true);
        (IModel modelB, ScaffoldedModel _) = ScaffoldDatabase(isolated, useDataAnnotations: true);

        // Assert
        CompileAndAssertNoErrors(codeA, "data-annotations");

        IRelationalModel relA = modelA.GetRelationalModel();
        IRelationalModel relB = modelB.GetRelationalModel();

        AssertZeroDiff(relA, relB, "cp-da: pass-1 → pass-2");
        AssertZeroDiff(relB, relA, "cp-da: pass-2 → pass-1");

        IRelationalModel refRelModel = refCtx.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        AssertZeroDiff(refRelModel, relA, "cp-da: reference → scaffolded");
        AssertZeroDiff(relA, refRelModel, "cp-da: scaffolded → reference");
    }

    #endregion

    #region Should_Scaffold_CompressionPolicy_Contains_WithCompressionPolicy_Call

    [Fact]
    public async Task Should_Scaffold_CompressionPolicy_Contains_WithCompressionPolicy_Call()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (_, ScaffoldedModel code) = ScaffoldDatabase(isolated, useDataAnnotations: false);

        // Assert
        string allGeneratedCode = code.ContextFile.Code +
            string.Join("\n", code.AdditionalFiles.Select(f => f.Code));

        Assert.Contains("WithCompressionPolicy", allGeneratedCode);
    }

    #endregion

    #region Should_Scaffold_CompressionPolicy_Contains_CompressionPolicyAttribute_In_DataAnnotations_Mode

    [Fact]
    public async Task Should_Scaffold_CompressionPolicy_Contains_CompressionPolicyAttribute_In_DataAnnotations_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (_, ScaffoldedModel code) = ScaffoldDatabase(isolated, useDataAnnotations: true);

        // Assert
        string allGeneratedCode = code.ContextFile.Code +
            string.Join("\n", code.AdditionalFiles.Select(f => f.Code));

        Assert.Contains("CompressionPolicy", allGeneratedCode);
    }

    #endregion
}

#pragma warning restore EF1001

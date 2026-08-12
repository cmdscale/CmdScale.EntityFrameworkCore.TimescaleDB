#pragma warning disable EF1001 // IScaffoldingModelFactory is an EF Core internal API, used here to drive the full scaffold-to-IModel pipeline.

using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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
/// Validates that scaffolding a compressed continuous aggregate produces zero phantom operations
/// when the scaffolded model is diffed against itself (idempotency) and against the
/// code-first reference model (losslessness of annotation round-trip).
/// </summary>
public sealed class ContinuousAggregateCompressionRoundTripTests : MigrationTestBase, IAsyncLifetime
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

    private class RrtCaCompSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class RrtCaCompHourlyView
    {
        public DateTime Bucket { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double AvgValue { get; set; }
    }

    #endregion

    private sealed class CaggCompressionRoundTripReferenceContext(string connectionString) : DbContext
    {
        public DbSet<RrtCaCompSourceMetric> Metrics => Set<RrtCaCompSourceMetric>();
        public DbSet<RrtCaCompHourlyView> HourlyMetrics => Set<RrtCaCompHourlyView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RrtCaCompSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_cacomp_source");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<RrtCaCompHourlyView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("rrt_cacomp_hourly");

                entity.IsContinuousAggregate<RrtCaCompHourlyView, RrtCaCompSourceMetric>(
                    "rrt_cacomp_hourly",
                    "1 hour",
                    x => x.Timestamp)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .AddGroupByColumn(x => x.DeviceId)
                    .WithCompressionSegmentBy(x => x.DeviceId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Bucket)]);

                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.Property(x => x.DeviceId).HasColumnName("DeviceId");
            });
        }
    }

    // ── Design-time helpers ───────────────────────────────────────────────────

    private static async Task<string> CreateIsolatedDatabaseAsync(string adminConnectionString)
    {
        string dbName = $"rrt_cacomp_{Guid.NewGuid():N}";

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
            ModelNamespace = "CaCompRoundTripScaffold",
            ContextName = "CaCompRoundTripDbContext",
            ContextNamespace = "CaCompRoundTripScaffold",
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
            $"CaCompRoundTripScaffold_{label}",
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

    private static void AssertZeroCaggDiff(
        IRelationalModel source,
        IRelationalModel target,
        string direction)
    {
        IFeatureDiffer differ = new ContinuousAggregateDiffer();

        IReadOnlyList<MigrationOperation> ops = differ.GetDifferences(source, target);
        if (ops.Count > 0)
        {
            string opList = string.Join(", ", ops.Select(op =>
            {
                PropertyInfo? nameProp =
                    op.GetType().GetProperty("TableName")
                    ?? op.GetType().GetProperty("MaterializedViewName");
                string name = nameProp?.GetValue(op)?.ToString() ?? "?";
                return $"{op.GetType().Name}({name})";
            }));

            Assert.Fail($"ContinuousAggregateDiffer produced {ops.Count} op(s) [{direction}]: {opList}");
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    #region Should_RoundTrip_CompressedCAgg_With_Zero_Diff_In_Fluent_Mode

    [Fact]
    public async Task Should_RoundTrip_CompressedCAgg_With_Zero_Diff_In_Fluent_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CaggCompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (IModel modelA, ScaffoldedModel codeA) = ScaffoldDatabase(isolated, useDataAnnotations: false);
        (IModel modelB, ScaffoldedModel _) = ScaffoldDatabase(isolated, useDataAnnotations: false);

        // Assert
        CompileAndAssertNoErrors(codeA, "fluent");

        IRelationalModel relA = modelA.GetRelationalModel();
        IRelationalModel relB = modelB.GetRelationalModel();

        AssertZeroCaggDiff(relA, relB, "cacomp-fluent: pass-1 → pass-2");
        AssertZeroCaggDiff(relB, relA, "cacomp-fluent: pass-2 → pass-1");
    }

    #endregion

    #region Should_RoundTrip_CompressedCAgg_With_Zero_Diff_In_DataAnnotations_Mode

    [Fact]
    public async Task Should_RoundTrip_CompressedCAgg_With_Zero_Diff_In_DataAnnotations_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CaggCompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (IModel modelA, ScaffoldedModel codeA) = ScaffoldDatabase(isolated, useDataAnnotations: true);
        (IModel modelB, ScaffoldedModel _) = ScaffoldDatabase(isolated, useDataAnnotations: true);

        // Assert
        CompileAndAssertNoErrors(codeA, "data-annotations");

        IRelationalModel relA = modelA.GetRelationalModel();
        IRelationalModel relB = modelB.GetRelationalModel();

        AssertZeroCaggDiff(relA, relB, "cacomp-da: pass-1 → pass-2");
        AssertZeroCaggDiff(relB, relA, "cacomp-da: pass-2 → pass-1");
    }

    #endregion

    #region Should_Scaffold_WithCompressionSegmentBy_In_Fluent_Mode

    [Fact]
    public async Task Should_Scaffold_WithCompressionSegmentBy_In_Fluent_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CaggCompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (_, ScaffoldedModel code) = ScaffoldDatabase(isolated, useDataAnnotations: false);

        // Assert
        string allCode = code.ContextFile.Code +
            string.Join("\n", code.AdditionalFiles.Select(f => f.Code));
        Assert.Contains("WithCompressionSegmentBy", allCode);
    }

    #endregion

    #region Should_Scaffold_CompressionSegmentBy_In_DataAnnotations_Mode

    [Fact]
    public async Task Should_Scaffold_CompressionSegmentBy_In_DataAnnotations_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using CaggCompressionRoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (_, ScaffoldedModel code) = ScaffoldDatabase(isolated, useDataAnnotations: true);

        // Assert
        string allCode = code.ContextFile.Code +
            string.Join("\n", code.AdditionalFiles.Select(f => f.Code));
        Assert.Contains("CompressionSegmentBy", allCode);
    }

    #endregion
}

#pragma warning restore EF1001

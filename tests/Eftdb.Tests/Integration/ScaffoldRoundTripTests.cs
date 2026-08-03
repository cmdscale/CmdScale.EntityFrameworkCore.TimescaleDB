#pragma warning disable EF1001 // IScaffoldingModelFactory is an EF Core internal API, used here to drive the full scaffold-to-IModel pipeline.

using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies;
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
/// Validates that the TimescaleDB scaffolding roundtrip is lossless for all renderable features.
/// A code-first reference database is created via migrations, scaffolded back to C# with the
/// design-time pipeline, and the generated code is compiled in-memory with Roslyn to catch
/// renderer bugs that produce un-compilable output.
///
/// Two complementary roundtrip assertions are performed:
///
/// Annotation-level (hypertable, retention, reorder): the code-first reference model and the
/// scaffolded model are compared via feature differs.
///
/// Database-level (continuous aggregate): the scaffolded model is applied to a second database
/// via the migration pipeline and the two databases are then queried directly.
///
/// Scope: only TimescaleDB feature differs are tested. Base EF Core relational differences
/// (column nullability, CLR type mapping, etc.) are intentionally out of scope.
/// </summary>
public sealed class ScaffoldRoundTripTests : MigrationTestBase, IAsyncLifetime
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

    private class CompressedMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
        public int Region { get; set; }
    }

    private class PolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DropCreatedBeforeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CaSourceMetric
    {
        public DateTime Timestamp { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Volume { get; set; }
    }

    private class CaHourlyView
    {
        public DateTime Bucket { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public double MaxPrice { get; set; }
        public double AvgPrice { get; set; }
        public long CountRows { get; set; }
    }

    #endregion

    private sealed class RoundTripReferenceContext(string connectionString) : DbContext
    {
        public DbSet<CompressedMetric> CompressedMetrics => Set<CompressedMetric>();
        public DbSet<PolicyMetric> PolicyMetrics => Set<PolicyMetric>();
        public DbSet<DropCreatedBeforeMetric> DropCreatedBeforeMetrics => Set<DropCreatedBeforeMetric>();
        public DbSet<CaSourceMetric> CaSourceMetrics => Set<CaSourceMetric>();
        public DbSet<CaHourlyView> CaHourlyViews => Set<CaHourlyView>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressedMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_compressed_metric");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("30 days")
                      .WithCompressionSegmentBy(x => x.DeviceId)
                      .WithCompressionOrderBy(s => [
                          s.ByDescending(x => x.Timestamp),
                          s.By(x => x.Value)
                      ])
                      .HasDimension(Dimension.CreateHash(nameof(CompressedMetric.Region), 4));
            });

            modelBuilder.Entity<PolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_policy_metric");
                entity.HasIndex(x => x.Timestamp, "rrt_policy_metric_time_idx");

                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(
                          dropAfter: "30 days",
                          scheduleInterval: "2 days",
                          maxRuntime: "1 hour",
                          maxRetries: 3,
                          retryPeriod: "5 minutes",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                      .WithReorderPolicy(
                          indexName: "rrt_policy_metric_time_idx",
                          scheduleInterval: "1 day",
                          maxRuntime: "30 minutes",
                          maxRetries: 2,
                          retryPeriod: "10 minutes",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });

            modelBuilder.Entity<DropCreatedBeforeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_dcb_metric");
                entity.IsHypertable(x => x.Timestamp)
                      .WithRetentionPolicy(dropCreatedBefore: "60 days");
            });

            modelBuilder.Entity<CaSourceMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("rrt_ca_source");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CaHourlyView>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("rrt_ca_hourly");

                entity.IsContinuousAggregate<CaHourlyView, CaSourceMetric>(
                        "rrt_ca_hourly",
                        "1 hour",
                        x => x.Timestamp,
                        chunkInterval: "30 days")
                    .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
                    .AddAggregateFunction(x => x.AvgPrice, x => x.Price, EAggregateFunction.Avg)
                    .AddAggregateFunction(x => x.CountRows, x => x.Ticker, EAggregateFunction.Count)
                    .AddGroupByColumn(x => x.Ticker)
                    .MaterializedOnly(true)
                    .WithRefreshPolicy(startOffset: "7 days", endOffset: "1 hour", scheduleInterval: "4 hours");

                entity.WithRetentionPolicy(dropAfter: "90 days");
            });
        }
    }

    // ── Design-time helpers ───────────────────────────────────────────────────

    private static async Task<string> CreateIsolatedDatabaseAsync(string adminConnectionString)
    {
        string dbName = $"rrt_{Guid.NewGuid():N}";

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
            ModelNamespace = "RoundTripScaffold",
            ContextName = "RoundTripDbContext",
            ContextNamespace = "RoundTripScaffold",
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

    private static async Task ApplyCompiledContextToDatabase(byte[] peBytes, string connectionString)
    {
        Assembly compiledAssembly = Assembly.Load(peBytes);
        Type contextType = compiledAssembly.GetType("RoundTripScaffold.RoundTripDbContext")
            ?? throw new InvalidOperationException("RoundTripScaffold.RoundTripDbContext not found in compiled assembly.");

        Type builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        DbContextOptionsBuilder builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        builder.UseNpgsql(connectionString).UseTimescaleDb();

        DbContext compiledCtx = (DbContext)Activator.CreateInstance(contextType, builder.Options)!;
        await using (compiledCtx)
        {
            await CreateDatabaseViaMigrationAsync(compiledCtx);
        }
    }

    private static async Task<(string ViewDefinition, bool MaterializedOnly, string? ChunkInterval)>
        QueryContinuousAggregateInfoAsync(string connectionString, string viewName)
    {
        const string sql = @"
            SELECT
                ca.view_definition,
                ca.materialized_only,
                dim.time_interval::text AS chunk_interval
            FROM timescaledb_information.continuous_aggregates ca
            LEFT JOIN _timescaledb_catalog.continuous_agg cagg
                ON ca.view_schema = cagg.user_view_schema
               AND ca.view_name   = cagg.user_view_name
            LEFT JOIN _timescaledb_catalog.hypertable mat_ht
                ON cagg.mat_hypertable_id = mat_ht.id
            LEFT JOIN timescaledb_information.dimensions dim
                ON dim.hypertable_schema = mat_ht.schema_name
               AND dim.hypertable_name   = mat_ht.table_name
               AND dim.dimension_number  = 1
            WHERE ca.view_name = @viewName;";

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("viewName", viewName);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), $"No row found in continuous_aggregates for view '{viewName}'.");

        string viewDefinition = reader.GetString(0);
        bool materializedOnly = reader.GetBoolean(1);
        string? chunkInterval = reader.IsDBNull(2) ? null : reader.GetString(2);

        return (viewDefinition, materializedOnly, chunkInterval);
    }

    private static async Task<(string? StartOffset, string? EndOffset, string ScheduleInterval)>
        QueryCaRefreshPolicyAsync(string connectionString, string viewName)
    {
        const string sql = @"
            SELECT
                j.config->>'start_offset'    AS start_offset,
                j.config->>'end_offset'      AS end_offset,
                j.schedule_interval::text    AS schedule_interval
            FROM timescaledb_information.jobs j
            INNER JOIN _timescaledb_catalog.continuous_agg ca
                ON (j.config->>'mat_hypertable_id')::integer = ca.mat_hypertable_id
            WHERE j.proc_name = 'policy_refresh_continuous_aggregate'
              AND ca.user_view_name = @viewName;";

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("viewName", viewName);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), $"No refresh policy job found for CA view '{viewName}'.");

        string? startOffset = reader.IsDBNull(0) ? null : reader.GetString(0);
        string? endOffset = reader.IsDBNull(1) ? null : reader.GetString(1);
        string scheduleInterval = reader.GetString(2);

        return (startOffset, endOffset, scheduleInterval);
    }

    private static async Task<string?> QueryCaRetentionDropAfterAsync(
        string connectionString, string viewName)
    {
        const string sql = @"
            SELECT j.config->>'drop_after' AS drop_after
            FROM timescaledb_information.jobs j
            INNER JOIN _timescaledb_catalog.continuous_agg ca
                ON (j.config->>'hypertable_id')::integer = ca.mat_hypertable_id
            WHERE j.proc_name = 'policy_retention'
              AND ca.user_view_name = @viewName;";

        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("viewName", viewName);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return reader.IsDBNull(0) ? null : reader.GetString(0);
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
            $"RoundTripScaffold_{label}",
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

    // ── Differ assertion helpers ──────────────────────────────────────────────

    private static void AssertZeroDiff(
        IRelationalModel source,
        IRelationalModel target,
        string direction)
    {
        IFeatureDiffer[] differs =
        [
            new HypertableDiffer(),
            new ContinuousAggregateDiffer(),
            new ContinuousAggregatePolicyDiffer(),
            new RetentionPolicyDiffer(),
            new ReorderPolicyDiffer(),
        ];

        List<string> failures = [];
        foreach (IFeatureDiffer differ in differs)
        {
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
                failures.Add($"  {differ.GetType().Name}: {ops.Count} op(s) — {opList}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Feature differs produced non-zero operations [{direction}]:\n" +
            string.Join("\n", failures));
    }

    /// <summary>
    /// Runs only the annotation-stable feature differs (hypertable, retention, reorder) between
    /// the code-first reference model and the scaffolded model. The CA differ is excluded because
    /// the code-first model uses structured annotations (TimeBucketWidth, AggregateFunctions, etc.)
    /// while the scaffolded model uses raw ViewDefinition — a known design trade-off described in
    /// the class XML doc.
    /// </summary>
    private static void AssertZeroDiffForNonCaFeatures(
        IRelationalModel source,
        IRelationalModel target,
        string direction)
    {
        IFeatureDiffer[] differs =
        [
            new HypertableDiffer(),
            new RetentionPolicyDiffer(),
            new ReorderPolicyDiffer(),
        ];

        List<string> failures = [];
        foreach (IFeatureDiffer differ in differs)
        {
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
                failures.Add($"  {differ.GetType().Name}: {ops.Count} op(s) — {opList}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Non-CA feature differs produced non-zero operations [{direction}]:\n" +
            string.Join("\n", failures));
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    #region Should_RoundTrip_With_Zero_Feature_Diff_Operations_In_Fluent_Mode

    [Fact]
    public async Task Should_RoundTrip_With_Zero_Feature_Diff_Operations_In_Fluent_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using RoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (IModel modelA, ScaffoldedModel codeA) = ScaffoldDatabase(isolated, useDataAnnotations: false);
        (IModel modelB, ScaffoldedModel _) = ScaffoldDatabase(isolated, useDataAnnotations: false);

        // Assert
        CompileAndAssertNoErrors(codeA, "fluent");

        IRelationalModel relA = modelA.GetRelationalModel();
        IRelationalModel relB = modelB.GetRelationalModel();

        // Assert
        AssertZeroDiff(relA, relB, "fluent: pass-1 → pass-2");
        AssertZeroDiff(relB, relA, "fluent: pass-2 → pass-1");

        // Assert
        IRelationalModel refRelModel = refCtx.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        AssertZeroDiffForNonCaFeatures(refRelModel, relA, "fluent: reference → scaffolded");
        AssertZeroDiffForNonCaFeatures(relA, refRelModel, "fluent: scaffolded → reference");
    }

    #endregion

    #region Should_RoundTrip_With_Zero_Feature_Diff_Operations_In_DataAnnotations_Mode

    [Fact]
    public async Task Should_RoundTrip_With_Zero_Feature_Diff_Operations_In_DataAnnotations_Mode()
    {
        // Arrange
        string isolated = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using RoundTripReferenceContext refCtx = new(isolated);
        await CreateDatabaseViaMigrationAsync(refCtx);

        // Act
        (IModel modelA, ScaffoldedModel codeA) = ScaffoldDatabase(isolated, useDataAnnotations: true);
        (IModel modelB, ScaffoldedModel _) = ScaffoldDatabase(isolated, useDataAnnotations: true);

        // Assert
        CompileAndAssertNoErrors(codeA, "data-annotations");

        IRelationalModel relA = modelA.GetRelationalModel();
        IRelationalModel relB = modelB.GetRelationalModel();

        // Assert
        AssertZeroDiff(relA, relB, "da: pass-1 → pass-2");
        AssertZeroDiff(relB, relA, "da: pass-2 → pass-1");

        // Assert
        IRelationalModel refRelModel = refCtx.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        AssertZeroDiffForNonCaFeatures(refRelModel, relA, "da: reference → scaffolded");
        AssertZeroDiffForNonCaFeatures(relA, refRelModel, "da: scaffolded → reference");
    }

    #endregion

    #region Should_Produce_Identical_Continuous_Aggregate_When_Migrating_From_Scaffolded_Model

    [Fact]
    public async Task Should_Produce_Identical_Continuous_Aggregate_When_Migrating_From_Scaffolded_Model()
    {
        // Arrange
        string db1 = await CreateIsolatedDatabaseAsync(_connectionString!);
        await using RoundTripReferenceContext refCtx = new(db1);
        await CreateDatabaseViaMigrationAsync(refCtx);

        string db2 = await CreateIsolatedDatabaseAsync(_connectionString!);

        // Act
        (IModel modelDb1, ScaffoldedModel codeDb1) = ScaffoldDatabase(db1, useDataAnnotations: false, suppressOnConfiguring: true);
        byte[] peBytes = CompileAndAssertNoErrors(codeDb1, "ca-db-level");

        await ApplyCompiledContextToDatabase(peBytes, db2);

        // Assert
        (string viewDefDb1, bool matOnlyDb1, string? chunkDb1) = await QueryContinuousAggregateInfoAsync(db1, "rrt_ca_hourly");
        (string viewDefDb2, bool matOnlyDb2, string? chunkDb2) = await QueryContinuousAggregateInfoAsync(db2, "rrt_ca_hourly");
        Assert.Equal(viewDefDb1, viewDefDb2);
        Assert.Equal(matOnlyDb1, matOnlyDb2);
        Assert.Equal(chunkDb1, chunkDb2);

        (string? startDb1, string? endDb1, string schedDb1) = await QueryCaRefreshPolicyAsync(db1, "rrt_ca_hourly");
        (string? startDb2, string? endDb2, string schedDb2) = await QueryCaRefreshPolicyAsync(db2, "rrt_ca_hourly");
        Assert.Equal(startDb1, startDb2);
        Assert.Equal(endDb1, endDb2);
        Assert.Equal(schedDb1, schedDb2);

        string? retDb1 = await QueryCaRetentionDropAfterAsync(db1, "rrt_ca_hourly");
        string? retDb2 = await QueryCaRetentionDropAfterAsync(db2, "rrt_ca_hourly");
        Assert.Equal(retDb1, retDb2);

        (IModel modelDb2, ScaffoldedModel _) = ScaffoldDatabase(db2, useDataAnnotations: false);

        IRelationalModel relDb1 = modelDb1.GetRelationalModel();
        IRelationalModel relDb2 = modelDb2.GetRelationalModel();

        AssertZeroDiff(relDb1, relDb2, "ca-db-level: scaffold(DB1) → scaffold(DB2)");
        AssertZeroDiff(relDb2, relDb1, "ca-db-level: scaffold(DB2) → scaffold(DB1)");
    }

    #endregion
}

#pragma warning restore EF1001

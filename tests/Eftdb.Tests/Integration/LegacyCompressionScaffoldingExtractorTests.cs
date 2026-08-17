using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Integration tests that verify scaffolding extractor fallback paths on TimescaleDB 2.17.x (pre-2.18).
/// On 2.17, the hypertable_columnstore_settings view does not exist, so extractors fall back to
/// timescaledb_information.compression_settings.
/// </summary>
public class LegacyCompressionScaffoldingExtractorTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:2.17.2-pg17")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new("CREATE EXTENSION IF NOT EXISTS timescaledb;", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using NpgsqlConnection conn = new(_connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // ── TryReadCompressionSettingsFromColumnstoreView early return ───────────

    #region Should_Return_False_From_TryReadColumnstoreView_When_View_Absent

    [Fact]
    public async Task Should_Return_False_From_TryReadColumnstoreView_When_View_Absent()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_view_check (ts TIMESTAMPTZ NOT NULL, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_view_check', 'ts');
            ALTER TABLE legacy_view_check SET (timescaledb.compress = true);");

        // Act
        await using NpgsqlConnection connection = new(_connectionString);
        bool result = false;
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        result = CompressionSettingsScaffoldingHelper.TryReadCompressionSettingsFromColumnstoreView(
            connection,
            rawKey => (rawKey, true),
            (_, _) => { },
            (_, _) => { });

        // Assert
        Assert.False(result);
    }

    #endregion

    // ── ReadCompressionSettings: segmentby + orderby via legacy view ─────────

    #region Should_Extract_SegmentBy_And_OrderBy_Via_Legacy_CompressionSettings_View

    [Fact]
    public async Task Should_Extract_SegmentBy_And_OrderBy_Via_Legacy_CompressionSettings_View()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_ht_segby (ts TIMESTAMPTZ NOT NULL, device_id INT, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_ht_segby', 'ts');
            ALTER TABLE legacy_ht_segby SET (
                timescaledb.compress = true,
                timescaledb.compress_segmentby = 'device_id',
                timescaledb.compress_orderby = 'ts DESC'
            );");

        List<string> segmentByCols = [];
        List<string> orderByCols = [];

        // Act
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        CompressionSettingsScaffoldingHelper.ReadCompressionSettings(
            connection,
            rawKey => (rawKey, rawKey == ("public", "legacy_ht_segby")),
            (key, columnName, isSegmentBy, isOrderBy, isAscending, isNullsFirst) =>
            {
                if (isSegmentBy)
                {
                    segmentByCols.Add(columnName);
                }

                if (isOrderBy)
                {
                    orderByCols.Add(CompressionSettingsScaffoldingHelper.BuildOrderByEntry(columnName, isAscending, isNullsFirst));
                }
            });

        // Assert
        Assert.Equal("device_id", Assert.Single(segmentByCols));

        Assert.Equal("ts DESC", Assert.Single(orderByCols));
    }

    #endregion

    // ── ReadCompressionSettings: NULL segmentby (only orderby configured) ────

    #region Should_Extract_OrderBy_Only_When_No_SegmentBy_Via_Legacy_View

    [Fact]
    public async Task Should_Extract_OrderBy_Only_When_No_SegmentBy_Via_Legacy_View()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_ht_no_segby (ts TIMESTAMPTZ NOT NULL, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_ht_no_segby', 'ts');
            ALTER TABLE legacy_ht_no_segby SET (
                timescaledb.compress = true,
                timescaledb.compress_orderby = 'ts ASC'
            );");

        List<string> segmentByCols = [];
        List<string> orderByCols = [];

        // Act
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        CompressionSettingsScaffoldingHelper.ReadCompressionSettings(
            connection,
            rawKey => (rawKey, rawKey == ("public", "legacy_ht_no_segby")),
            (key, columnName, isSegmentBy, isOrderBy, isAscending, isNullsFirst) =>
            {
                if (isSegmentBy)
                {
                    segmentByCols.Add(columnName);
                }

                if (isOrderBy)
                {
                    orderByCols.Add(CompressionSettingsScaffoldingHelper.BuildOrderByEntry(columnName, isAscending, isNullsFirst));
                }
            });

        // Assert
        Assert.Empty(segmentByCols);
        Assert.Equal("ts ASC", Assert.Single(orderByCols));
    }

    #endregion

    // ── HypertableScaffoldingExtractor: legacy fallback path ─────────────────

    #region Should_Extract_Hypertable_SegmentBy_Via_Legacy_Path

    [Fact]
    public async Task Should_Extract_Hypertable_SegmentBy_Via_Legacy_Path()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_ht_extract_segby (ts TIMESTAMPTZ NOT NULL, region TEXT, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_ht_extract_segby', 'ts');
            ALTER TABLE legacy_ht_extract_segby SET (
                timescaledb.compress = true,
                timescaledb.compress_segmentby = 'region',
                timescaledb.compress_orderby = 'ts DESC'
            );");

        // Act
        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "legacy_ht_extract_segby")));
        HypertableScaffoldingExtractor.HypertableInfo info =
            (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "legacy_ht_extract_segby")];

        Assert.True(info.CompressionEnabled);
        Assert.Equal("region", Assert.Single(info.CompressionSegmentBy));

        string orderByEntry = Assert.Single(info.CompressionOrderBy);
        Assert.Contains("ts", orderByEntry);
        Assert.Contains("DESC", orderByEntry);
    }

    #endregion

    #region Should_Extract_Hypertable_With_Compression_No_SegmentBy_Via_Legacy_Path

    [Fact]
    public async Task Should_Extract_Hypertable_With_Compression_No_SegmentBy_Via_Legacy_Path()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_ht_compress_only (ts TIMESTAMPTZ NOT NULL, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_ht_compress_only', 'ts');
            ALTER TABLE legacy_ht_compress_only SET (
                timescaledb.compress = true,
                timescaledb.compress_orderby = 'ts DESC'
            );");

        // Act
        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "legacy_ht_compress_only")));
        HypertableScaffoldingExtractor.HypertableInfo info =
            (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "legacy_ht_compress_only")];

        Assert.True(info.CompressionEnabled);
        Assert.Empty(info.CompressionSegmentBy);
        Assert.Single(info.CompressionOrderBy);
    }

    #endregion

    // ── ContinuousAggregateScaffoldingExtractor: legacy CAgg fallback path ───

    #region Should_Extract_CAgg_CompressionSettings_Via_Legacy_Path

    [Fact]
    public async Task Should_Extract_CAgg_CompressionSettings_Via_Legacy_Path()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_cagg_source (ts TIMESTAMPTZ NOT NULL, device_id INT, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_cagg_source', 'ts');

            CREATE MATERIALIZED VIEW legacy_cagg_hourly
            WITH (timescaledb.continuous) AS
            SELECT time_bucket('1 hour', ts) AS bucket,
                   device_id,
                   avg(val) AS avg_val
            FROM legacy_cagg_source
            GROUP BY 1, 2
            WITH NO DATA;

            ALTER MATERIALIZED VIEW legacy_cagg_hourly SET (
                timescaledb.compress = true,
                timescaledb.compress_segmentby = 'device_id',
                timescaledb.compress_orderby = 'bucket DESC'
            );");

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "legacy_cagg_hourly")));
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "legacy_cagg_hourly")];

        Assert.True(info.CompressionEnabled);
        Assert.NotNull(info.CompressionSegmentBy);
        Assert.Contains("device_id", info.CompressionSegmentBy);
        Assert.NotNull(info.CompressionOrderBy);
        Assert.NotEmpty(info.CompressionOrderBy);
        Assert.Contains("DESC", info.CompressionOrderBy[0]);
    }

    #endregion

    // ── ReadCompressionSettings: !accepted row-skip (lines 55-56) ────────────

    #region Should_Skip_CAgg_Materialization_Row_In_Legacy_ReadCompressionSettings

    [Fact]
    public async Task Should_Skip_CAgg_Materialization_Row_In_Legacy_ReadCompressionSettings()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_ht_with_cagg (ts TIMESTAMPTZ NOT NULL, region TEXT, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_ht_with_cagg', 'ts');
            ALTER TABLE legacy_ht_with_cagg SET (
                timescaledb.compress = true,
                timescaledb.compress_segmentby = 'region',
                timescaledb.compress_orderby = 'ts DESC'
            );

            CREATE MATERIALIZED VIEW legacy_cagg_for_skip_test
            WITH (timescaledb.continuous) AS
            SELECT time_bucket('1 hour', ts) AS bucket,
                   region,
                   avg(val) AS avg_val
            FROM legacy_ht_with_cagg
            GROUP BY 1, 2
            WITH NO DATA;

            ALTER MATERIALIZED VIEW legacy_cagg_for_skip_test SET (
                timescaledb.compress = true,
                timescaledb.compress_segmentby = 'region',
                timescaledb.compress_orderby = 'bucket DESC'
            );");

        // Act
        HypertableScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "legacy_ht_with_cagg")));
        HypertableScaffoldingExtractor.HypertableInfo info =
            (HypertableScaffoldingExtractor.HypertableInfo)result[("public", "legacy_ht_with_cagg")];

        Assert.True(info.CompressionEnabled);
        Assert.Equal("region", Assert.Single(info.CompressionSegmentBy));
    }

    #endregion

    #region Should_Extract_CAgg_Without_CompressionSettings_Via_Legacy_Path

    [Fact]
    public async Task Should_Extract_CAgg_Without_CompressionSettings_Via_Legacy_Path()
    {
        // Arrange
        await ExecuteSqlAsync(@"
            CREATE TABLE legacy_cagg_uncompressed_source (ts TIMESTAMPTZ NOT NULL, val DOUBLE PRECISION);
            SELECT create_hypertable('legacy_cagg_uncompressed_source', 'ts');

            CREATE MATERIALIZED VIEW legacy_cagg_uncompressed_hourly
            WITH (timescaledb.continuous) AS
            SELECT time_bucket('1 hour', ts) AS bucket,
                   avg(val) AS avg_val
            FROM legacy_cagg_uncompressed_source
            GROUP BY 1
            WITH NO DATA;");

        // Act
        ContinuousAggregateScaffoldingExtractor extractor = new();
        await using NpgsqlConnection connection = new(_connectionString);
        Dictionary<(string Schema, string TableName), object> result = extractor.Extract(connection);

        // Assert
        Assert.True(result.ContainsKey(("public", "legacy_cagg_uncompressed_hourly")));
        ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo info =
            (ContinuousAggregateScaffoldingExtractor.ContinuousAggregateInfo)result[("public", "legacy_cagg_uncompressed_hourly")];

        Assert.False(info.CompressionEnabled);
        Assert.NotNull(info.CompressionSegmentBy);
        Assert.Empty(info.CompressionSegmentBy);
    }

    #endregion
}

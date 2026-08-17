using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators;

public class ContinuousAggregateCompressionSqlGeneratorTests
{
    private static List<string> Generate(CreateContinuousAggregateOperation op)
        => ContinuousAggregateSqlGenerator.Generate(op);

    private static List<string> Generate(AlterContinuousAggregateOperation op)
        => ContinuousAggregateSqlGenerator.Generate(op);

    // ── Create with compression ───────────────────────────────────────────────

    #region Create_WithCompression_Only_Emits_License_Guard_Block

    [Fact]
    public void Create_WithCompression_Only_Emits_License_Guard_Block()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "hourly_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["avg_val:Avg:value"],
            EnableCompression = true,
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Equal(2, statements.Count);
        string compressionStmt = statements[1];
        Assert.Contains("DO $$", compressionStmt);
        Assert.Contains("timescaledb.enable_columnstore = true", compressionStmt);
        Assert.DoesNotContain("compress_segmentby", compressionStmt);
        Assert.DoesNotContain("compress_orderby", compressionStmt);
    }

    #endregion

    #region Create_WithCompression_And_SegmentBy_Emits_SegmentBy

    [Fact]
    public void Create_WithCompression_And_SegmentBy_Emits_SegmentBy()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "seg_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["cnt:Count:id"],
            EnableCompression = true,
            CompressionSegmentBy = ["region"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Equal(2, statements.Count);
        string compressionStmt = statements[1];
        Assert.Contains("timescaledb.enable_columnstore = true", compressionStmt);
        Assert.Contains("segmentby = ''\"region\"''", compressionStmt);
    }

    #endregion

    #region Create_WithCompression_And_OrderBy_Emits_OrderBy

    [Fact]
    public void Create_WithCompression_And_OrderBy_Emits_OrderBy()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "ord_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["avg_v:Avg:value"],
            EnableCompression = true,
            CompressionOrderBy = ["time DESC"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Equal(2, statements.Count);
        string compressionStmt = statements[1];
        Assert.Contains("timescaledb.enable_columnstore = true", compressionStmt);
        Assert.Contains("orderby = ''\"time\" DESC''", compressionStmt);
    }

    #endregion

    #region Create_WithSegmentByAndOrderBy_Emits_Both

    [Fact]
    public void Create_WithSegmentByAndOrderBy_Emits_Both()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "full_comp_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["avg_v:Avg:value"],
            EnableCompression = true,
            CompressionSegmentBy = ["device_id"],
            CompressionOrderBy = ["time DESC"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Equal(2, statements.Count);
        string compressionStmt = statements[1];
        Assert.Contains("timescaledb.enable_columnstore = true", compressionStmt);
        Assert.Contains("segmentby", compressionStmt);
        Assert.Contains("orderby", compressionStmt);
    }

    #endregion

    #region Create_WithNoCompression_Emits_SingleStatement

    [Fact]
    public void Create_WithNoCompression_Emits_SingleStatement()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "no_comp_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["avg_v:Avg:value"],
            EnableCompression = false,
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.DoesNotContain("compress", Assert.Single(statements));
    }

    #endregion

    #region Create_WithViewDefinition_And_Compression_Emits_CreateThenGuard

    [Fact]
    public void Create_WithViewDefinition_And_Compression_Emits_CreateThenGuard()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "raw_comp_cagg",
            Schema = "public",
            ParentName = "metrics",
            ViewDefinition = "SELECT time_bucket('1 hour', time) AS bucket FROM metrics GROUP BY bucket",
            EnableCompression = true,
            CompressionSegmentBy = ["region"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Contains("CREATE MATERIALIZED VIEW", statements[0]);
        Assert.Contains("DO $$", statements[1]);
        Assert.Contains("timescaledb.enable_columnstore = true", statements[1]);
    }

    #endregion

    #region Create_SegmentBy_QuotesColumnIdentifiers

    [Fact]
    public void Create_SegmentBy_QuotesColumnIdentifiers()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "quote_check_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["cnt:Count:id"],
            EnableCompression = true,
            CompressionSegmentBy = ["device_id", "region"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        string compressionStmt = statements[1];
        Assert.Contains("\"device_id\", \"region\"", compressionStmt);
    }

    #endregion

    // ── Alter with compression ────────────────────────────────────────────────

    #region Alter_EnableCompression_EmitsLicenseGuardWithTrue

    [Fact]
    public void Alter_EnableCompression_EmitsLicenseGuardWithTrue()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_comp_cagg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = false,
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        string stmt = Assert.Single(statements);
        Assert.Contains("DO $$", stmt);
        Assert.Contains("timescaledb.enable_columnstore = true", stmt);
    }

    #endregion

    #region Alter_DisableCompression_EmitsLicenseGuardWithFalse

    [Fact]
    public void Alter_DisableCompression_EmitsLicenseGuardWithFalse()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_disable_comp",
            Schema = "public",
            EnableCompression = false,
            OldEnableCompression = true,
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Contains("timescaledb.enable_columnstore = false", Assert.Single(statements));
    }

    #endregion

    #region Alter_ChangeSegmentBy_EmitsNewSegmentBy

    [Fact]
    public void Alter_ChangeSegmentBy_EmitsNewSegmentBy()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_seg_cagg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = true,
            CompressionSegmentBy = ["device_id"],
            OldCompressionSegmentBy = ["region"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        string stmt = Assert.Single(statements);
        Assert.Contains("segmentby = ''\"device_id\"''", stmt);
        Assert.DoesNotContain("\"region\"", stmt);
    }

    #endregion

    #region Alter_RemoveSegmentBy_EmitsEmptyString

    [Fact]
    public void Alter_RemoveSegmentBy_EmitsEmptyString()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_rem_seg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = true,
            CompressionSegmentBy = null,
            OldCompressionSegmentBy = ["region"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Contains("segmentby = ''", Assert.Single(statements));
    }

    #endregion

    #region Alter_ChangeOrderBy_EmitsNewOrderBy

    [Fact]
    public void Alter_ChangeOrderBy_EmitsNewOrderBy()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_ord_cagg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = true,
            CompressionOrderBy = ["time DESC"],
            OldCompressionOrderBy = ["time ASC"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Contains("orderby = ''\"time\" DESC''", Assert.Single(statements));
    }

    #endregion

    #region Alter_NoCompressionChange_EmitsNoCompressionStatement

    [Fact]
    public void Alter_NoCompressionChange_EmitsNoCompressionStatement()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_no_comp_change",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = true,
            CompressionSegmentBy = ["region"],
            OldCompressionSegmentBy = ["region"],
            CompressionOrderBy = ["time ASC"],
            OldCompressionOrderBy = ["time ASC"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.DoesNotContain(statements, s => s.Contains("timescaledb.enable_columnstore"));
    }

    #endregion

    #region Alter_CompressionWithOtherChanges_EmitsBothStatements

    [Fact]
    public void Alter_CompressionWithOtherChanges_EmitsBothStatements()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_mixed_cagg",
            Schema = "public",
            CreateGroupIndexes = true,
            OldCreateGroupIndexes = false,
            EnableCompression = true,
            OldEnableCompression = false,
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Contains(statements, s => s.Contains("create_group_indexes"));
        Assert.Contains(statements, s => s.Contains("timescaledb.enable_columnstore"));
    }

    #endregion

    #region Alter_WrapsCommunityFeaturesInLicenseGuard

    [Fact]
    public void Alter_WrapsCommunityFeaturesInLicenseGuard()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "guard_check_cagg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = false,
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        string stmt = Assert.Single(statements);
        Assert.Contains("DO $$", stmt);
        Assert.Contains("DECLARE", stmt);
        Assert.Contains("timescaledb.license", stmt);
        Assert.Contains("IF license IS NULL OR license != 'apache' THEN", stmt);
        Assert.Contains("RAISE WARNING", stmt);
    }

    #endregion

    #region Alter_OrderBy_QuotesColumnName_And_PreservesDirection

    [Fact]
    public void Alter_OrderBy_QuotesColumnName_And_PreservesDirection()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "ord_quote_cagg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = true,
            CompressionOrderBy = ["bucket_time DESC NULLS LAST"],
            OldCompressionOrderBy = ["bucket_time ASC"],
        };

        // Act
        List<string> statements = Generate(op);

        // Assert
        Assert.Contains("\"bucket_time\" DESC NULLS LAST", Assert.Single(statements));
    }

    #endregion

    // ── Legacy mode ───────────────────────────────────────────────────────────

    #region Legacy_Create_WithCompression_EmitsLegacyCompressionNames

    [Fact]
    public void Legacy_Create_WithCompression_EmitsLegacyCompressionNames()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "legacy_comp_cagg",
            Schema = "public",
            ParentName = "metrics",
            TimeBucketWidth = "1 hour",
            TimeBucketSourceColumn = "time",
            TimeBucketGroupBy = true,
            AggregateFunctions = ["avg_v:Avg:value"],
            EnableCompression = true,
            CompressionSegmentBy = ["region"],
            CompressionOrderBy = ["time DESC"],
        };

        // Act
        List<string> statements = ContinuousAggregateSqlGenerator.Generate(op, useLegacyCompressionNames: true);
        string compressionStmt = statements[1];

        // Assert
        Assert.Contains("timescaledb.compress = true", compressionStmt);
        Assert.Contains("timescaledb.compress_segmentby", compressionStmt);
        Assert.Contains("timescaledb.compress_orderby", compressionStmt);
        Assert.DoesNotContain("enable_columnstore", compressionStmt);
    }

    #endregion

    #region Legacy_Alter_EnableCompression_EmitsLegacyCompressName

    [Fact]
    public void Legacy_Alter_EnableCompression_EmitsLegacyCompressName()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "legacy_alter_cagg",
            Schema = "public",
            EnableCompression = true,
            OldEnableCompression = false,
        };

        // Act
        List<string> statements = ContinuousAggregateSqlGenerator.Generate(op, useLegacyCompressionNames: true);

        // Assert
        string statement = Assert.Single(statements);
        Assert.Contains("timescaledb.compress = true", statement);
        Assert.DoesNotContain("enable_columnstore", statement);
    }

    #endregion

    #region Legacy_Alter_DisableCompression_EmitsLegacyCompressFalse

    [Fact]
    public void Legacy_Alter_DisableCompression_EmitsLegacyCompressFalse()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "legacy_disable_cagg",
            Schema = "public",
            EnableCompression = false,
            OldEnableCompression = true,
        };

        // Act
        List<string> statements = ContinuousAggregateSqlGenerator.Generate(op, useLegacyCompressionNames: true);

        // Assert
        string statement = Assert.Single(statements);
        Assert.Contains("timescaledb.compress = false", statement);
        Assert.DoesNotContain("enable_columnstore", statement);
    }

    #endregion
}

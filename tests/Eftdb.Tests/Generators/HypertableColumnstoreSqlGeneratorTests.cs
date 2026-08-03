using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators;

public class HypertableColumnstoreSqlGeneratorTests
{
    private static string GetGeneratedSql(CreateHypertableOperation operation)
    {
        List<string> statements = HypertableSqlGenerator.Generate(operation);
        return string.Join("\n", statements);
    }

    private static string GetGeneratedSql(AlterHypertableOperation operation)
    {
        List<string> statements = HypertableSqlGenerator.Generate(operation);
        return string.Join("\n", statements);
    }

    // ── Create: SparseIndex only ──

    #region Should_Generate_Create_With_SparseIndex

    [Fact]
    public void Should_Generate_Create_With_SparseIndex()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            CompressionSparseIndex = "bloom(device_id)"
        };

        string expected = @"
            SELECT create_hypertable('public.""sensor_data""', 'ts');
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = ''bloom(device_id)'')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Create: CompressChunkTimeInterval only ──

    #region Should_Generate_Create_With_CompressChunkTimeInterval

    [Fact]
    public void Should_Generate_Create_With_CompressChunkTimeInterval()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            CompressChunkTimeInterval = "24 hours"
        };

        string expected = @"
            SELECT create_hypertable('public.""sensor_data""', 'ts');
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.compress_chunk_time_interval = ''24 hours'')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Create: both settings ──

    #region Should_Generate_Create_With_SparseIndex_And_CompressChunkTimeInterval

    [Fact]
    public void Should_Generate_Create_With_SparseIndex_And_CompressChunkTimeInterval()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            CompressionSparseIndex = "bloom(device_id)",
            CompressChunkTimeInterval = "7 days"
        };

        string expected = @"
            SELECT create_hypertable('public.""sensor_data""', 'ts');
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = ''bloom(device_id)'', timescaledb.compress_chunk_time_interval = ''7 days'')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Create: empty-string SparseIndex (disable auto-created sparse indexes) ──

    #region Should_Generate_Create_With_Empty_SparseIndex

    [Fact]
    public void Should_Generate_Create_With_Empty_SparseIndex()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            CompressionSparseIndex = string.Empty
        };

        string expected = @"
            SELECT create_hypertable('public.""sensor_data""', 'ts');
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = '''')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Create: null SparseIndex → no sparse_index in SET ──

    #region Should_Not_Include_SparseIndex_When_Null_On_Create

    [Fact]
    public void Should_Not_Include_SparseIndex_When_Null_On_Create()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            EnableCompression = true,
            CompressionSparseIndex = null
        };

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.DoesNotContain("sparse_index", result);
    }

    #endregion

    // ── Create: null CompressChunkTimeInterval → not included ──

    #region Should_Not_Include_CompressChunkTimeInterval_When_Null_On_Create

    [Fact]
    public void Should_Not_Include_CompressChunkTimeInterval_When_Null_On_Create()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            EnableCompression = true,
            CompressChunkTimeInterval = null
        };

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.DoesNotContain("compress_chunk_time_interval", result);
    }

    #endregion

    // ── Create: settings appear inside the community guard block ──

    #region Should_Include_SparseIndex_Inside_Community_Guard_On_Create

    [Fact]
    public void Should_Include_SparseIndex_Inside_Community_Guard_On_Create()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            TimeColumnName = "ts",
            CompressionSparseIndex = "bloom(device_id)"
        };

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Contains("DO $$", result);
        Assert.Contains("timescaledb.license", result);
        Assert.Contains("sparse_index", result);
        int guardStart = result.IndexOf("DO $$", StringComparison.Ordinal);
        int sparseIdx = result.IndexOf("sparse_index", StringComparison.Ordinal);
        Assert.True(sparseIdx > guardStart, "sparse_index must appear inside the community guard block");
    }

    #endregion

    // ── Alter: SparseIndex changed ──

    #region Should_Generate_Alter_Set_When_SparseIndex_Changed

    [Fact]
    public void Should_Generate_Alter_Set_When_SparseIndex_Changed()
    {
        // Arrange
        AlterHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            CompressionSparseIndex = "bloom(device_id), minmax(temperature)",
            OldCompressionSparseIndex = "bloom(device_id)"
        };

        string expected = @"
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = ''bloom(device_id), minmax(temperature)'')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Alter: SparseIndex removed → RESET ──

    #region Should_Generate_Alter_Reset_When_SparseIndex_Removed

    [Fact]
    public void Should_Generate_Alter_Reset_When_SparseIndex_Removed()
    {
        // Arrange
        AlterHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            CompressionSparseIndex = null,
            OldCompressionSparseIndex = "bloom(device_id)"
        };

        string expected = @"
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" RESET (timescaledb.sparse_index)';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Alter: CompressChunkTimeInterval changed ──

    #region Should_Generate_Alter_Set_When_CompressChunkTimeInterval_Changed

    [Fact]
    public void Should_Generate_Alter_Set_When_CompressChunkTimeInterval_Changed()
    {
        // Arrange
        AlterHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            CompressChunkTimeInterval = "7 days",
            OldCompressChunkTimeInterval = "24 hours"
        };

        string expected = @"
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.compress_chunk_time_interval = ''7 days'')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Alter: CompressChunkTimeInterval removed → SET '0' ──

    #region Should_Generate_Alter_Set_Zero_When_CompressChunkTimeInterval_Removed

    [Fact]
    public void Should_Generate_Alter_Set_Zero_When_CompressChunkTimeInterval_Removed()
    {
        // Arrange
        AlterHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            CompressChunkTimeInterval = null,
            OldCompressChunkTimeInterval = "24 hours"
        };

        string expected = @"
            DO $$
            DECLARE
                license TEXT;
            BEGIN
                license := current_setting('timescaledb.license', true);

                IF license IS NULL OR license != 'apache' THEN
                    EXECUTE 'ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.compress_chunk_time_interval = ''0'')';
                ELSE
                    RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                END IF;
            END $$;
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Alter: RESET statements appear inside community guard ──

    #region Should_Include_Reset_Statements_Inside_Community_Guard

    [Fact]
    public void Should_Include_Reset_Statements_Inside_Community_Guard()
    {
        // Arrange
        AlterHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            CompressionSparseIndex = null,
            OldCompressionSparseIndex = "bloom(device_id)",
            CompressChunkTimeInterval = null,
            OldCompressChunkTimeInterval = "24 hours"
        };

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Contains("DO $$", result);
        Assert.Contains("timescaledb.license", result);
        int guardStart = result.IndexOf("DO $$", StringComparison.Ordinal);
        int sparseReset = result.IndexOf("RESET (timescaledb.sparse_index)", StringComparison.Ordinal);
        int cctiClear = result.IndexOf("timescaledb.compress_chunk_time_interval = ''0''", StringComparison.Ordinal);
        Assert.True(sparseReset > guardStart, "RESET (timescaledb.sparse_index) must appear inside community guard");
        Assert.True(cctiClear > guardStart, "SET (timescaledb.compress_chunk_time_interval = '0') must appear inside community guard");
    }

    #endregion

    // ── Alter: both removed → RESET and SET '0' inside one guard ──

    #region Should_Generate_Both_Removal_Statements_When_Both_Removed

    [Fact]
    public void Should_Generate_Both_Removal_Statements_When_Both_Removed()
    {
        // Arrange
        AlterHypertableOperation operation = new()
        {
            TableName = "sensor_data",
            Schema = "public",
            CompressionSparseIndex = null,
            OldCompressionSparseIndex = "bloom(device_id)",
            CompressChunkTimeInterval = null,
            OldCompressChunkTimeInterval = "24 hours"
        };

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Contains("RESET (timescaledb.sparse_index)", result);
        Assert.Contains("timescaledb.compress_chunk_time_interval = ''0''", result);
        Assert.Equal(1, result.Split("DO $$").Length - 1);
    }

    #endregion
}

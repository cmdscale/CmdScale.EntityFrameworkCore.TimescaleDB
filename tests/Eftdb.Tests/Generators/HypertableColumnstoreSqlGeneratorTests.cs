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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = 'bloom(device_id)');
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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.compress_chunk_time_interval = '24 hours');
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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = 'bloom(device_id)', timescaledb.compress_chunk_time_interval = '7 days');
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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = '');
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

    // ── Create: sparse index emitted as a clean ALTER TABLE after create ──

    #region Should_Emit_SparseIndex_As_Clean_Alter_On_Create

    [Fact]
    public void Should_Emit_SparseIndex_As_Clean_Alter_On_Create()
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
        Assert.DoesNotContain("DO $$", result);
        Assert.DoesNotContain("timescaledb.license", result);
        Assert.Contains("ALTER TABLE \"public\".\"sensor_data\" SET (timescaledb.sparse_index = 'bloom(device_id)');", result);
        int createIdx = result.IndexOf("create_hypertable", StringComparison.Ordinal);
        int sparseIdx = result.IndexOf("sparse_index", StringComparison.Ordinal);
        Assert.True(sparseIdx > createIdx, "sparse_index must appear after create_hypertable");
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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.sparse_index = 'bloom(device_id), minmax(temperature)');
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
            ALTER TABLE ""public"".""sensor_data"" RESET (timescaledb.sparse_index);
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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.compress_chunk_time_interval = '7 days');
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
            ALTER TABLE ""public"".""sensor_data"" SET (timescaledb.compress_chunk_time_interval = '0');
        ";

        // Act
        string result = GetGeneratedSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    // ── Alter: RESET and clear statements emitted cleanly ──

    #region Should_Emit_Reset_Statements_Cleanly

    [Fact]
    public void Should_Emit_Reset_Statements_Cleanly()
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
        Assert.DoesNotContain("DO $$", result);
        Assert.DoesNotContain("timescaledb.license", result);
        Assert.Contains("ALTER TABLE \"public\".\"sensor_data\" RESET (timescaledb.sparse_index);", result);
        Assert.Contains("timescaledb.compress_chunk_time_interval = '0'", result);
    }

    #endregion

    // ── Alter: both removed → RESET and SET '0' emitted cleanly ──

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
        Assert.Contains("timescaledb.compress_chunk_time_interval = '0'", result);
        Assert.DoesNotContain("DO $$", result);
    }

    #endregion
}

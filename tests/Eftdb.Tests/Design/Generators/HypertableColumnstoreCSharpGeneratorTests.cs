using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

public class HypertableColumnstoreCSharpGeneratorTests
{
    private readonly ICSharpHelper _code = DesignTimeHelper.CreateRealCSharpHelper();

    private string Generate(CreateHypertableOperation operation)
    {
        IndentedStringBuilder builder = new();
        new HypertableCSharpGenerator(_code).Generate(operation, builder);
        return builder.ToString();
    }

    private string Generate(AlterHypertableOperation operation)
    {
        IndentedStringBuilder builder = new();
        new HypertableCSharpGenerator(_code).Generate(operation, builder);
        return builder.ToString();
    }

    // ── Create: SparseIndex with value ──

    #region Create_Emits_CompressionSparseIndex_When_Non_Null

    [Fact]
    public void Create_Emits_CompressionSparseIndex_When_Non_Null()
    {
        // Arrange
        CreateHypertableOperation op = new()
        {
            TableName = "sensor_data",
            TimeColumnName = "ts",
            CompressionSparseIndex = "bloom(device_id)"
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionSparseIndex: \"bloom(device_id)\"", result);
    }

    #endregion

    // ── Create: SparseIndex empty string (disable auto-created indexes) ──

    #region Create_Emits_CompressionSparseIndex_When_Empty_String

    [Fact]
    public void Create_Emits_CompressionSparseIndex_When_Empty_String()
    {
        // Arrange
        CreateHypertableOperation op = new()
        {
            TableName = "sensor_data",
            TimeColumnName = "ts",
            CompressionSparseIndex = string.Empty
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionSparseIndex: \"\"", result);
    }

    #endregion

    // ── Create: SparseIndex null → omitted ──

    #region Create_Omits_CompressionSparseIndex_When_Null

    [Fact]
    public void Create_Omits_CompressionSparseIndex_When_Null()
    {
        // Arrange
        CreateHypertableOperation op = new()
        {
            TableName = "sensor_data",
            TimeColumnName = "ts",
            CompressionSparseIndex = null
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("compressionSparseIndex:", result);
    }

    #endregion

    // ── Create: CompressChunkTimeInterval with value ──

    #region Create_Emits_CompressChunkTimeInterval_When_Non_Empty

    [Fact]
    public void Create_Emits_CompressChunkTimeInterval_When_Non_Empty()
    {
        // Arrange
        CreateHypertableOperation op = new()
        {
            TableName = "sensor_data",
            TimeColumnName = "ts",
            CompressChunkTimeInterval = "24 hours"
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressChunkTimeInterval: \"24 hours\"", result);
    }

    #endregion

    // ── Create: CompressChunkTimeInterval null → omitted ──

    #region Create_Omits_CompressChunkTimeInterval_When_Null

    [Fact]
    public void Create_Omits_CompressChunkTimeInterval_When_Null()
    {
        // Arrange
        CreateHypertableOperation op = new()
        {
            TableName = "sensor_data",
            TimeColumnName = "ts",
            CompressChunkTimeInterval = null
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("compressChunkTimeInterval:", result);
    }

    #endregion

    // ── Create: CompressChunkTimeInterval empty string → omitted ──

    #region Create_Omits_CompressChunkTimeInterval_When_Empty_String

    [Fact]
    public void Create_Omits_CompressChunkTimeInterval_When_Empty_String()
    {
        // Arrange
        CreateHypertableOperation op = new()
        {
            TableName = "sensor_data",
            TimeColumnName = "ts",
            CompressChunkTimeInterval = string.Empty
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("compressChunkTimeInterval:", result);
    }

    #endregion

    // ── Alter: new SparseIndex value emitted ──

    #region Alter_Emits_CompressionSparseIndex_When_Non_Null

    [Fact]
    public void Alter_Emits_CompressionSparseIndex_When_Non_Null()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressionSparseIndex = "bloom(device_id)"
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionSparseIndex: \"bloom(device_id)\"", result);
    }

    #endregion

    // ── Alter: old SparseIndex value emitted when non-null ──

    #region Alter_Emits_OldCompressionSparseIndex_When_Non_Null

    [Fact]
    public void Alter_Emits_OldCompressionSparseIndex_When_Non_Null()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressionSparseIndex = "bloom(device_id), minmax(temp)",
            OldCompressionSparseIndex = "bloom(device_id)"
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("oldCompressionSparseIndex: \"bloom(device_id)\"", result);
    }

    #endregion

    // ── Alter: old SparseIndex null → omitted ──

    #region Alter_Omits_OldCompressionSparseIndex_When_Null

    [Fact]
    public void Alter_Omits_OldCompressionSparseIndex_When_Null()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressionSparseIndex = "bloom(device_id)",
            OldCompressionSparseIndex = null
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("oldCompressionSparseIndex:", result);
    }

    #endregion

    // ── Alter: old SparseIndex empty string emitted ──

    #region Alter_Emits_OldCompressionSparseIndex_When_Empty_String

    [Fact]
    public void Alter_Emits_OldCompressionSparseIndex_When_Empty_String()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressionSparseIndex = "bloom(device_id)",
            OldCompressionSparseIndex = string.Empty
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("oldCompressionSparseIndex: \"\"", result);
    }

    #endregion

    // ── Alter: new CompressChunkTimeInterval emitted ──

    #region Alter_Emits_CompressChunkTimeInterval_When_Non_Empty

    [Fact]
    public void Alter_Emits_CompressChunkTimeInterval_When_Non_Empty()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressChunkTimeInterval = "7 days"
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressChunkTimeInterval: \"7 days\"", result);
    }

    #endregion

    // ── Alter: new CompressChunkTimeInterval null → omitted ──

    #region Alter_Omits_CompressChunkTimeInterval_When_Null

    [Fact]
    public void Alter_Omits_CompressChunkTimeInterval_When_Null()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressChunkTimeInterval = null
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("compressChunkTimeInterval:", result);
    }

    #endregion

    // ── Alter: old CompressChunkTimeInterval emitted ──

    #region Alter_Emits_OldCompressChunkTimeInterval_When_Non_Empty

    [Fact]
    public void Alter_Emits_OldCompressChunkTimeInterval_When_Non_Empty()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressChunkTimeInterval = "7 days",
            OldCompressChunkTimeInterval = "24 hours"
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("oldCompressChunkTimeInterval: \"24 hours\"", result);
    }

    #endregion

    // ── Alter: old CompressChunkTimeInterval null → omitted ──

    #region Alter_Omits_OldCompressChunkTimeInterval_When_Null

    [Fact]
    public void Alter_Omits_OldCompressChunkTimeInterval_When_Null()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressChunkTimeInterval = "7 days",
            OldCompressChunkTimeInterval = null
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("oldCompressChunkTimeInterval:", result);
    }

    #endregion

    // ── Alter: old CompressChunkTimeInterval empty string → omitted ──

    #region Alter_Omits_OldCompressChunkTimeInterval_When_Empty_String

    [Fact]
    public void Alter_Omits_OldCompressChunkTimeInterval_When_Empty_String()
    {
        // Arrange
        AlterHypertableOperation op = new()
        {
            TableName = "sensor_data",
            CompressChunkTimeInterval = "7 days",
            OldCompressChunkTimeInterval = string.Empty
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("oldCompressChunkTimeInterval:", result);
    }

    #endregion
}

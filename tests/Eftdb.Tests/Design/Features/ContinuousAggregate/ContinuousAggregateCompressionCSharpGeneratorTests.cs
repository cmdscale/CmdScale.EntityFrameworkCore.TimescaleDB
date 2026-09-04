using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.ContinuousAggregate;

public class ContinuousAggregateCompressionCSharpGeneratorTests
{
    private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

    private string Generate(CreateContinuousAggregateOperation operation)
    {
        IndentedStringBuilder builder = new();
        new ContinuousAggregateCSharpGenerator(code).Generate(operation, builder);
        return builder.ToString();
    }

    private string Generate(AlterContinuousAggregateOperation operation)
    {
        IndentedStringBuilder builder = new();
        new ContinuousAggregateCSharpGenerator(code).Generate(operation, builder);
        return builder.ToString();
    }

    // ── Create operations ─────────────────────────────────────────────────────

    #region Create_EnableCompression_True_EmitsParameter

    [Fact]
    public void Create_EnableCompression_True_EmitsParameter()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "compressed_cagg",
            ParentName = "metrics",
            EnableCompression = true,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("enableCompression: true", result);
    }

    #endregion

    #region Create_EnableCompression_False_OmitsParameter

    [Fact]
    public void Create_EnableCompression_False_OmitsParameter()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "uncompressed_cagg",
            ParentName = "metrics",
            EnableCompression = false,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("enableCompression", result);
    }

    #endregion

    #region Create_CompressionSegmentBy_EmitsParameter

    [Fact]
    public void Create_CompressionSegmentBy_EmitsParameter()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "seg_cagg",
            ParentName = "metrics",
            EnableCompression = true,
            CompressionSegmentBy = ["device_id", "region"],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionSegmentBy:", result);
        Assert.Contains("device_id", result);
        Assert.Contains("region", result);
    }

    #endregion

    #region Create_CompressionSegmentBy_Empty_OmitsParameter

    [Fact]
    public void Create_CompressionSegmentBy_Empty_OmitsParameter()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "no_seg_cagg",
            ParentName = "metrics",
            EnableCompression = true,
            CompressionSegmentBy = [],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("compressionSegmentBy", result);
    }

    #endregion

    #region Create_CompressionOrderBy_EmitsParameter

    [Fact]
    public void Create_CompressionOrderBy_EmitsParameter()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "ord_cagg",
            ParentName = "metrics",
            EnableCompression = true,
            CompressionOrderBy = ["time DESC"],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionOrderBy:", result);
        Assert.Contains("time DESC", result);
    }

    #endregion

    #region Create_CompressionOrderBy_Null_OmitsParameter

    [Fact]
    public void Create_CompressionOrderBy_Null_OmitsParameter()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "no_ord_cagg",
            ParentName = "metrics",
            EnableCompression = true,
            CompressionOrderBy = null,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("compressionOrderBy", result);
    }

    #endregion

    // ── Alter operations ──────────────────────────────────────────────────────

    #region Alter_EnableCompression_True_EmitsParameter

    [Fact]
    public void Alter_EnableCompression_True_EmitsParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_comp_cagg",
            EnableCompression = true,
            OldEnableCompression = false,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("enableCompression: true", result);
    }

    #endregion

    #region Alter_EnableCompression_False_OmitsParameter

    [Fact]
    public void Alter_EnableCompression_False_OmitsParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_no_comp_cagg",
            EnableCompression = false,
            OldEnableCompression = false,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("enableCompression", result);
    }

    #endregion

    #region Alter_OldEnableCompression_True_EmitsOldParameter

    [Fact]
    public void Alter_OldEnableCompression_True_EmitsOldParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_old_comp_cagg",
            EnableCompression = false,
            OldEnableCompression = true,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("oldEnableCompression: true", result);
    }

    #endregion

    #region Alter_OldEnableCompression_False_OmitsOldParameter

    [Fact]
    public void Alter_OldEnableCompression_False_OmitsOldParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_old_no_comp",
            EnableCompression = true,
            OldEnableCompression = false,
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.DoesNotContain("oldEnableCompression", result);
    }

    #endregion

    #region Alter_CompressionSegmentBy_EmitsParameter

    [Fact]
    public void Alter_CompressionSegmentBy_EmitsParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_seg",
            EnableCompression = true,
            CompressionSegmentBy = ["device_id"],
            OldCompressionSegmentBy = [],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionSegmentBy:", result);
        Assert.Contains("device_id", result);
    }

    #endregion

    #region Alter_OldCompressionSegmentBy_EmitsOldParameter

    [Fact]
    public void Alter_OldCompressionSegmentBy_EmitsOldParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_old_seg",
            OldCompressionSegmentBy = ["region"],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("oldCompressionSegmentBy:", result);
        Assert.Contains("region", result);
    }

    #endregion

    #region Alter_CompressionOrderBy_EmitsParameter

    [Fact]
    public void Alter_CompressionOrderBy_EmitsParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_ord",
            EnableCompression = true,
            CompressionOrderBy = ["bucket DESC"],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("compressionOrderBy:", result);
        Assert.Contains("bucket DESC", result);
    }

    #endregion

    #region Alter_OldCompressionOrderBy_EmitsOldParameter

    [Fact]
    public void Alter_OldCompressionOrderBy_EmitsOldParameter()
    {
        // Arrange
        AlterContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "alter_old_ord",
            OldCompressionOrderBy = ["bucket ASC"],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("oldCompressionOrderBy:", result);
        Assert.Contains("bucket ASC", result);
    }

    #endregion

    #region Create_FullCompressionParams_GeneratesCompleteCall

    [Fact]
    public void Create_FullCompressionParams_GeneratesCompleteCall()
    {
        // Arrange
        CreateContinuousAggregateOperation op = new()
        {
            MaterializedViewName = "full_comp",
            ParentName = "metrics",
            EnableCompression = true,
            CompressionSegmentBy = ["region"],
            CompressionOrderBy = ["bucket DESC"],
        };

        // Act
        string result = Generate(op);

        // Assert
        Assert.Contains("enableCompression: true", result);
        Assert.Contains("compressionSegmentBy:", result);
        Assert.Contains("compressionOrderBy:", result);
    }

    #endregion
}

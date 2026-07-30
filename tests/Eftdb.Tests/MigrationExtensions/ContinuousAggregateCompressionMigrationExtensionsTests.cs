using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions;

public class ContinuousAggregateCompressionMigrationExtensionsTests
{
    private static MigrationBuilder CreateBuilder() => new("Npgsql");

    // ── CreateContinuousAggregate ─────────────────────────────────────────────

    #region CreateContinuousAggregate_EnableCompression_MapsToOperation

    [Fact]
    public void CreateContinuousAggregate_EnableCompression_MapsToOperation()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.CreateContinuousAggregate(
            "comp_cagg",
            "metrics",
            enableCompression: true);

        // Assert
        CreateContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<CreateContinuousAggregateOperation>());
        Assert.True(op.EnableCompression);
    }

    #endregion

    #region CreateContinuousAggregate_CompressionSegmentBy_MapsToOperation

    [Fact]
    public void CreateContinuousAggregate_CompressionSegmentBy_MapsToOperation()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.CreateContinuousAggregate(
            "seg_cagg",
            "metrics",
            enableCompression: true,
            compressionSegmentBy: ["device_id", "region"]);

        // Assert
        CreateContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<CreateContinuousAggregateOperation>());
        Assert.Equal(["device_id", "region"], op.CompressionSegmentBy);
    }

    #endregion

    #region CreateContinuousAggregate_CompressionOrderBy_MapsToOperation

    [Fact]
    public void CreateContinuousAggregate_CompressionOrderBy_MapsToOperation()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.CreateContinuousAggregate(
            "ord_cagg",
            "metrics",
            enableCompression: true,
            compressionOrderBy: ["time DESC"]);

        // Assert
        CreateContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<CreateContinuousAggregateOperation>());
        Assert.Equal(["time DESC"], op.CompressionOrderBy);
    }

    #endregion

    #region CreateContinuousAggregate_NoCompression_DefaultsToFalse

    [Fact]
    public void CreateContinuousAggregate_NoCompression_DefaultsToFalse()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.CreateContinuousAggregate("plain_cagg", "metrics");

        // Assert
        CreateContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<CreateContinuousAggregateOperation>());
        Assert.False(op.EnableCompression);
        Assert.Null(op.CompressionSegmentBy);
        Assert.Null(op.CompressionOrderBy);
    }

    #endregion

    // ── AlterContinuousAggregate ──────────────────────────────────────────────

    #region AlterContinuousAggregate_EnableCompression_MapsToOperation

    [Fact]
    public void AlterContinuousAggregate_EnableCompression_MapsToOperation()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.AlterContinuousAggregate(
            "alter_comp_cagg",
            enableCompression: true,
            oldEnableCompression: false);

        // Assert
        AlterContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<AlterContinuousAggregateOperation>());
        Assert.True(op.EnableCompression);
        Assert.False(op.OldEnableCompression);
    }

    #endregion

    #region AlterContinuousAggregate_CompressionSegmentBy_MapsToOperation

    [Fact]
    public void AlterContinuousAggregate_CompressionSegmentBy_MapsToOperation()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.AlterContinuousAggregate(
            "alter_seg_cagg",
            compressionSegmentBy: ["device_id"],
            oldCompressionSegmentBy: ["region"]);

        // Assert
        AlterContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<AlterContinuousAggregateOperation>());
        Assert.Equal(["device_id"], op.CompressionSegmentBy);
        Assert.Equal(["region"], op.OldCompressionSegmentBy);
    }

    #endregion

    #region AlterContinuousAggregate_CompressionOrderBy_MapsToOperation

    [Fact]
    public void AlterContinuousAggregate_CompressionOrderBy_MapsToOperation()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.AlterContinuousAggregate(
            "alter_ord_cagg",
            compressionOrderBy: ["bucket DESC"],
            oldCompressionOrderBy: ["bucket ASC"]);

        // Assert
        AlterContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<AlterContinuousAggregateOperation>());
        Assert.Equal(["bucket DESC"], op.CompressionOrderBy);
        Assert.Equal(["bucket ASC"], op.OldCompressionOrderBy);
    }

    #endregion

    #region AlterContinuousAggregate_AllCompressionParams_MappedCorrectly

    [Fact]
    public void AlterContinuousAggregate_AllCompressionParams_MappedCorrectly()
    {
        // Arrange
        MigrationBuilder builder = CreateBuilder();

        // Act
        builder.AlterContinuousAggregate(
            "full_alter_cagg",
            schema: "analytics",
            enableCompression: true,
            compressionSegmentBy: ["device_id"],
            compressionOrderBy: ["time DESC"],
            oldEnableCompression: false,
            oldCompressionSegmentBy: null,
            oldCompressionOrderBy: null);

        // Assert
        AlterContinuousAggregateOperation op = Assert.Single(
            builder.Operations.OfType<AlterContinuousAggregateOperation>());
        Assert.Equal("full_alter_cagg", op.MaterializedViewName);
        Assert.Equal("analytics", op.Schema);
        Assert.True(op.EnableCompression);
        Assert.Equal(["device_id"], op.CompressionSegmentBy);
        Assert.Equal(["time DESC"], op.CompressionOrderBy);
        Assert.False(op.OldEnableCompression);
        Assert.Null(op.OldCompressionSegmentBy);
        Assert.Null(op.OldCompressionOrderBy);
    }

    #endregion
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable.HypertableScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

public class HypertableColumnstoreAnnotationApplierTests
{
    private readonly HypertableAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestTable", string schema = "public")
        => new() { Name = name, Schema = schema };

    private static HypertableInfo MinimalInfoWith(
        string? compressionSparseIndex = null,
        string? compressChunkTimeInterval = null) =>
        new(
            TimeColumnName: "ts",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: compressionSparseIndex,
            CompressChunkTimeInterval: compressChunkTimeInterval
        );

    // ── SparseIndex: null → annotation not set ──

    #region Should_Not_Set_SparseIndex_Annotation_When_Null

    [Fact]
    public void Should_Not_Set_SparseIndex_Annotation_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(compressionSparseIndex: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[HypertableAnnotations.CompressionSparseIndex]);
    }

    #endregion

    // ── SparseIndex: "" → annotation set as empty string ──

    #region Should_Set_SparseIndex_Annotation_As_Empty_String

    [Fact]
    public void Should_Set_SparseIndex_Annotation_As_Empty_String()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(compressionSparseIndex: string.Empty);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(string.Empty, table[HypertableAnnotations.CompressionSparseIndex]);
    }

    #endregion

    // ── SparseIndex: value → annotation set ──

    #region Should_Set_SparseIndex_Annotation_With_Value

    [Fact]
    public void Should_Set_SparseIndex_Annotation_With_Value()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(compressionSparseIndex: "bloom(device_id)");

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("bloom(device_id)", table[HypertableAnnotations.CompressionSparseIndex]);
    }

    #endregion

    // ── CompressChunkTimeInterval: null → annotation not set ──

    #region Should_Not_Set_CompressChunkTimeInterval_Annotation_When_Null

    [Fact]
    public void Should_Not_Set_CompressChunkTimeInterval_Annotation_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(compressChunkTimeInterval: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[HypertableAnnotations.CompressChunkTimeInterval]);
    }

    #endregion

    // ── CompressChunkTimeInterval: whitespace → annotation not set ──

    #region Should_Not_Set_CompressChunkTimeInterval_Annotation_When_Whitespace

    [Fact]
    public void Should_Not_Set_CompressChunkTimeInterval_Annotation_When_Whitespace()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(compressChunkTimeInterval: "   ");

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[HypertableAnnotations.CompressChunkTimeInterval]);
    }

    #endregion

    // ── CompressChunkTimeInterval: value → annotation set ──

    #region Should_Set_CompressChunkTimeInterval_Annotation_With_Value

    [Fact]
    public void Should_Set_CompressChunkTimeInterval_Annotation_With_Value()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(compressChunkTimeInterval: "24 hours");

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("24 hours", table[HypertableAnnotations.CompressChunkTimeInterval]);
    }

    #endregion

    // ── Both settings set together ──

    #region Should_Set_Both_Columnstore_Annotations_When_Both_Provided

    [Fact]
    public void Should_Set_Both_Columnstore_Annotations_When_Both_Provided()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = MinimalInfoWith(
            compressionSparseIndex: "bloom(device_id)",
            compressChunkTimeInterval: "7 days");

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("bloom(device_id)", table[HypertableAnnotations.CompressionSparseIndex]);
        Assert.Equal("7 days", table[HypertableAnnotations.CompressChunkTimeInterval]);
    }

    #endregion
}

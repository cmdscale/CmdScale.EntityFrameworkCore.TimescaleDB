using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using System.Text.Json;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.HypertableScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

public class HypertableAnnotationApplierTests
{
    private readonly HypertableAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestTable", string schema = "public")
    {
        return new DatabaseTable { Name = name, Schema = schema };
    }

    #region Should_Apply_Minimal_Hypertable_Annotations

    [Fact]
    public void Should_Apply_Minimal_Hypertable_Annotations()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify all annotations are set correctly
        Assert.Equal(true, table[HypertableAnnotations.IsHypertable]);
        Assert.Equal("Timestamp", table[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Equal("604800000000", table[HypertableAnnotations.ChunkTimeInterval]);
        Assert.Equal(false, table[HypertableAnnotations.EnableCompression]);

        // ChunkSkipColumns and AdditionalDimensions should NOT be set when empty
        Assert.Null(table[HypertableAnnotations.ChunkSkipColumns]);
        Assert.Null(table[HypertableAnnotations.AdditionalDimensions]);
    }

    #endregion

    #region Should_Apply_TimeColumn_Annotation

    [Fact]
    public void Should_Apply_TimeColumn_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "created_at",
            ChunkTimeInterval: "86400000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("created_at", table[HypertableAnnotations.HypertableTimeColumn]);
    }

    #endregion

    #region Should_Apply_ChunkTimeInterval_Annotation

    [Fact]
    public void Should_Apply_ChunkTimeInterval_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "3600000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("3600000000", table[HypertableAnnotations.ChunkTimeInterval]);
    }

    #endregion

    #region Should_Apply_Compression_Enabled_True

    [Fact]
    public void Should_Apply_Compression_Enabled_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Apply_Compression_Enabled_False

    [Fact]
    public void Should_Apply_Compression_Enabled_False()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(false, table[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Apply_Single_ChunkSkipColumn

    [Fact]
    public void Should_Apply_Single_ChunkSkipColumn()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: ["DeviceId"],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.ChunkSkipColumns]);
        Assert.Equal("DeviceId", table[HypertableAnnotations.ChunkSkipColumns]);
    }

    #endregion

    #region Should_Apply_Multiple_ChunkSkipColumns

    [Fact]
    public void Should_Apply_Multiple_ChunkSkipColumns()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: ["DeviceId", "Location", "SensorType"],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.ChunkSkipColumns]);
        Assert.Equal("DeviceId,Location,SensorType", table[HypertableAnnotations.ChunkSkipColumns]);
    }

    #endregion

    #region Should_Not_Apply_ChunkSkipColumns_When_Empty

    [Fact]
    public void Should_Not_Apply_ChunkSkipColumns_When_Empty()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[HypertableAnnotations.ChunkSkipColumns]);
    }

    #endregion

    #region Should_Apply_Single_Hash_Dimension

    [Fact]
    public void Should_Apply_Single_Hash_Dimension()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        Dimension hashDimension = Dimension.CreateHash("DeviceId", 4);
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: [hashDimension]
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.AdditionalDimensions]);
        string? json = table[HypertableAnnotations.AdditionalDimensions] as string;
        Assert.NotNull(json);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(json);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions);

        Dimension dimension = dimensions[0];
        Assert.Equal("DeviceId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
        Assert.Equal(4, dimension.NumberOfPartitions);
        Assert.Null(dimension.Interval);
    }

    #endregion

    #region Should_Apply_Single_Range_Dimension

    [Fact]
    public void Should_Apply_Single_Range_Dimension()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        Dimension rangeDimension = Dimension.CreateRange("Location", "1000");
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: [rangeDimension]
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.AdditionalDimensions]);
        string? json = table[HypertableAnnotations.AdditionalDimensions] as string;
        Assert.NotNull(json);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(json);
        Assert.NotNull(dimensions);
        Assert.Single(dimensions);

        Dimension dimension = dimensions[0];
        Assert.Equal("Location", dimension.ColumnName);
        Assert.Equal(EDimensionType.Range, dimension.Type);
        Assert.Equal("1000", dimension.Interval);
        Assert.Null(dimension.NumberOfPartitions);
    }

    #endregion

    #region Should_Apply_Multiple_Dimensions

    [Fact]
    public void Should_Apply_Multiple_Dimensions()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        Dimension hashDimension = Dimension.CreateHash("DeviceId", 8);
        Dimension rangeDimension = Dimension.CreateRange("Region", "86400000000");
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: [hashDimension, rangeDimension]
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.AdditionalDimensions]);
        string? json = table[HypertableAnnotations.AdditionalDimensions] as string;
        Assert.NotNull(json);

        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(json);
        Assert.NotNull(dimensions);
        Assert.Equal(2, dimensions.Count);

        // First dimension (hash)
        Assert.Equal("DeviceId", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Hash, dimensions[0].Type);
        Assert.Equal(8, dimensions[0].NumberOfPartitions);

        // Second dimension (range)
        Assert.Equal("Region", dimensions[1].ColumnName);
        Assert.Equal(EDimensionType.Range, dimensions[1].Type);
        Assert.Equal("86400000000", dimensions[1].Interval);
    }

    #endregion

    #region Should_Not_Apply_AdditionalDimensions_When_Empty

    [Fact]
    public void Should_Not_Apply_AdditionalDimensions_When_Empty()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[HypertableAnnotations.AdditionalDimensions]);
    }

    #endregion

    #region Should_Apply_All_Annotations_For_Fully_Configured_Hypertable

    [Fact]
    public void Should_Apply_All_Annotations_For_Fully_Configured_Hypertable()
    {
        // Arrange
        DatabaseTable table = CreateTable("SensorData", "sensors");
        Dimension hashDimension = Dimension.CreateHash("device_id", 16);
        Dimension rangeDimension = Dimension.CreateRange("region_code", "2592000000000");
        HypertableInfo info = new(
            TimeColumnName: "recorded_at",
            ChunkTimeInterval: "86400000000",
            CompressionEnabled: true,
            ChunkSkipColumns: ["device_id", "sensor_type", "region_code"],
            AdditionalDimensions: [hashDimension, rangeDimension]
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify ALL annotations are applied
        Assert.Equal(true, table[HypertableAnnotations.IsHypertable]);
        Assert.Equal("recorded_at", table[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Equal("86400000000", table[HypertableAnnotations.ChunkTimeInterval]);
        Assert.Equal(true, table[HypertableAnnotations.EnableCompression]);
        Assert.Equal("device_id,sensor_type,region_code", table[HypertableAnnotations.ChunkSkipColumns]);

        // Verify dimensions JSON
        string? dimensionsJson = table[HypertableAnnotations.AdditionalDimensions] as string;
        Assert.NotNull(dimensionsJson);
        List<Dimension>? dimensions = JsonSerializer.Deserialize<List<Dimension>>(dimensionsJson);
        Assert.NotNull(dimensions);
        Assert.Equal(2, dimensions.Count);
        Assert.Equal("device_id", dimensions[0].ColumnName);
        Assert.Equal(16, dimensions[0].NumberOfPartitions);
        Assert.Equal("region_code", dimensions[1].ColumnName);
        Assert.Equal("2592000000000", dimensions[1].Interval);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Invalid_Info_Type

    [Fact]
    public void Should_Throw_ArgumentException_For_Invalid_Info_Type()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        object invalidInfo = new { SomeProperty = "invalid" };

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, invalidInfo)
        );

        Assert.Equal("featureInfo", exception.ParamName);
        Assert.Contains("Expected HypertableInfo", exception.Message);
        Assert.Contains("<>f__AnonymousType", exception.Message);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Null_Info

    [Fact]
    public void Should_Throw_ArgumentException_For_Null_Info()
    {
        // Arrange
        DatabaseTable table = CreateTable();

        // Act & Assert
        Assert.Throws<NullReferenceException>(
            () => _applier.ApplyAnnotations(table, null!)
        );
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Wrong_Info_Type_With_Message

    [Fact]
    public void Should_Throw_ArgumentException_For_Wrong_Info_Type_With_Message()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        string wrongInfo = "wrong type";

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, wrongInfo)
        );

        Assert.Contains("Expected HypertableInfo", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    #endregion

    #region Should_Apply_IsHypertable_Always_True

    [Fact]
    public void Should_Apply_IsHypertable_Always_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - IsHypertable should always be set to true
        object? value = table[HypertableAnnotations.IsHypertable];
        Assert.NotNull(value);
        Assert.IsType<bool>(value);
        Assert.True((bool)value);
    }

    #endregion

    #region Should_Preserve_Existing_Table_Properties

    [Fact]
    public void Should_Preserve_Existing_Table_Properties()
    {
        // Arrange
        DatabaseTable table = CreateTable("PreservedTable", "custom_schema");
        table.Comment = "This is a test table";
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            ChunkSkipColumns: [],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - table properties should be preserved
        Assert.Equal("PreservedTable", table.Name);
        Assert.Equal("custom_schema", table.Schema);
        Assert.Equal("This is a test table", table.Comment);

        // And annotations should still be applied
        Assert.Equal(true, table[HypertableAnnotations.IsHypertable]);
        Assert.Equal("Timestamp", table[HypertableAnnotations.HypertableTimeColumn]);
    }

    #endregion

    #region Should_Handle_Special_Characters_In_Column_Names

    [Fact]
    public void Should_Handle_Special_Characters_In_Column_Names()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "time_stamp_utc",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: false,
            ChunkSkipColumns: ["device_id", "sensor_type_v2"],
            AdditionalDimensions: []
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("time_stamp_utc", table[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Equal("device_id,sensor_type_v2", table[HypertableAnnotations.ChunkSkipColumns]);
    }

    #endregion
}

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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[HypertableAnnotations.IsHypertable]);
        Assert.Equal("Timestamp", table[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Equal("604800000000", table[HypertableAnnotations.ChunkTimeInterval]);
        Assert.Equal(false, table[HypertableAnnotations.EnableCompression]);
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(false, table[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Apply_CompressionSegmentBy

    [Fact]
    public void Should_Apply_CompressionSegmentBy()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            CompressionSegmentBy: ["TenantId", "DeviceId"],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.CompressionSegmentBy]);
        Assert.Equal("TenantId, DeviceId", table[HypertableAnnotations.CompressionSegmentBy]);
        Assert.Equal(true, table[HypertableAnnotations.EnableCompression]);
    }

    #endregion

    #region Should_Apply_CompressionOrderBy

    [Fact]
    public void Should_Apply_CompressionOrderBy()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            CompressionSegmentBy: [],
            CompressionOrderBy: ["Timestamp DESC", "Value ASC NULLS LAST"],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.NotNull(table[HypertableAnnotations.CompressionOrderBy]);
        Assert.Equal("Timestamp DESC, Value ASC NULLS LAST", table[HypertableAnnotations.CompressionOrderBy]);
    }

    #endregion

    #region Should_Apply_Full_Compression_Configuration

    [Fact]
    public void Should_Apply_Full_Compression_Configuration()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            CompressionSegmentBy: ["DeviceId"],
            CompressionOrderBy: ["Timestamp DESC"],
            ChunkSkipColumns: ["DeviceId"],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[HypertableAnnotations.EnableCompression]);
        Assert.Equal("DeviceId", table[HypertableAnnotations.CompressionSegmentBy]);
        Assert.Equal("Timestamp DESC", table[HypertableAnnotations.CompressionOrderBy]);
        Assert.Equal("DeviceId", table[HypertableAnnotations.ChunkSkipColumns]);
    }

    #endregion

    #region Should_Not_Apply_Compression_Annotations_When_Lists_Empty

    [Fact]
    public void Should_Not_Apply_Compression_Annotations_When_Lists_Empty()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        HypertableInfo info = new(
            TimeColumnName: "Timestamp",
            ChunkTimeInterval: "604800000000",
            CompressionEnabled: true,
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[HypertableAnnotations.EnableCompression]);
        Assert.Null(table[HypertableAnnotations.CompressionSegmentBy]);
        Assert.Null(table[HypertableAnnotations.CompressionOrderBy]);
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: ["DeviceId"],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: ["DeviceId", "Location", "SensorType"],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [hashDimension],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [rangeDimension],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [hashDimension, rangeDimension],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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

        Assert.Equal("DeviceId", dimensions[0].ColumnName);
        Assert.Equal(EDimensionType.Hash, dimensions[0].Type);
        Assert.Equal(8, dimensions[0].NumberOfPartitions);

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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: ["device_id", "sensor_type", "region_code"],
            AdditionalDimensions: [hashDimension, rangeDimension],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[HypertableAnnotations.IsHypertable]);
        Assert.Equal("recorded_at", table[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Equal("86400000000", table[HypertableAnnotations.ChunkTimeInterval]);
        Assert.Equal(true, table[HypertableAnnotations.EnableCompression]);
        Assert.Equal("device_id,sensor_type,region_code", table[HypertableAnnotations.ChunkSkipColumns]);

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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: [],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("PreservedTable", table.Name);
        Assert.Equal("custom_schema", table.Schema);
        Assert.Equal("This is a test table", table.Comment);
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
            CompressionSegmentBy: [],
            CompressionOrderBy: [],
            ChunkSkipColumns: ["device_id", "sensor_type_v2"],
            AdditionalDimensions: [],
            CompressionSparseIndex: null,
            CompressChunkTimeInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("time_stamp_utc", table[HypertableAnnotations.HypertableTimeColumn]);
        Assert.Equal("device_id,sensor_type_v2", table[HypertableAnnotations.ChunkSkipColumns]);
    }

    #endregion

    // ── Auto-created index suppression ──────────────────────────────────────

    private static HypertableInfo MinimalInfo(string timeColumn = "time", List<Dimension>? dimensions = null) => new(
        TimeColumnName: timeColumn,
        ChunkTimeInterval: "604800000000",
        CompressionEnabled: false,
        CompressionSegmentBy: [],
        CompressionOrderBy: [],
        ChunkSkipColumns: [],
        AdditionalDimensions: dimensions ?? [],
        CompressionSparseIndex: null,
        CompressChunkTimeInterval: null
    );

    private static DatabaseIndex AddIndex(DatabaseTable table, string name, bool isUnique, params string[] columns)
    {
        DatabaseIndex index = new() { Name = name, Table = table, IsUnique = isUnique };
        foreach (string column in columns)
        {
            index.Columns.Add(new DatabaseColumn { Name = column, Table = table, StoreType = "timestamptz" });
        }

        table.Indexes.Add(index);
        return index;
    }

    #region Should_Remove_AutoCreated_Time_Index

    [Fact]
    public void Should_Remove_AutoCreated_Time_Index()
    {
        // Arrange
        DatabaseTable table = CreateTable("weather_data");
        AddIndex(table, "weather_data_time_idx", isUnique: false, "time");

        // Act
        _applier.ApplyAnnotations(table, MinimalInfo());

        // Assert
        Assert.Empty(table.Indexes);
    }

    #endregion

    #region Should_Remove_AutoCreated_Dimension_Index

    [Fact]
    public void Should_Remove_AutoCreated_Dimension_Index()
    {
        // Arrange
        DatabaseTable table = CreateTable("order_events");
        AddIndex(table, "order_events_time_idx", isUnique: false, "time");
        AddIndex(table, "order_events_region_time_idx", isUnique: false, "region", "time");
        HypertableInfo info = MinimalInfo(dimensions:
            [new Dimension { ColumnName = "region", Type = EDimensionType.Hash, NumberOfPartitions = 4 }]);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Empty(table.Indexes);
    }

    #endregion

    #region Should_Keep_UserDefined_Index_On_Time_Column

    [Fact]
    public void Should_Keep_UserDefined_Index_On_Time_Column()
    {
        // Arrange
        DatabaseTable table = CreateTable("weather_data");
        AddIndex(table, "ix_weather_by_time", isUnique: false, "time");

        // Act
        _applier.ApplyAnnotations(table, MinimalInfo());

        // Assert
        Assert.Single(table.Indexes);
    }

    #endregion

    #region Should_Keep_Unique_Index_Even_When_Name_Matches

    [Fact]
    public void Should_Keep_Unique_Index_Even_When_Name_Matches()
    {
        // Arrange
        DatabaseTable table = CreateTable("weather_data");
        AddIndex(table, "weather_data_time_idx", isUnique: true, "time");

        // Act
        _applier.ApplyAnnotations(table, MinimalInfo());

        // Assert
        Assert.Single(table.Indexes);
    }

    #endregion

    #region Should_Keep_Index_When_Columns_Do_Not_Match_Pattern

    [Fact]
    public void Should_Keep_Index_When_Columns_Do_Not_Match_Pattern()
    {
        // Arrange
        DatabaseTable table = CreateTable("weather_data");
        AddIndex(table, "weather_data_time_idx", isUnique: false, "time", "station_id");

        // Act
        _applier.ApplyAnnotations(table, MinimalInfo());

        // Assert
        Assert.Single(table.Indexes);
    }

    #endregion
}

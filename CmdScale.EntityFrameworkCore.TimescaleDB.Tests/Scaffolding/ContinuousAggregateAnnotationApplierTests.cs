using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.ContinuousAggregateScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

public class ContinuousAggregateAnnotationApplierTests
{
    private readonly ContinuousAggregateAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestView", string schema = "public")
    {
        return new DatabaseTable { Name = name, Schema = schema };
    }

    #region Should_Apply_Minimal_ContinuousAggregate_Annotations

    [Fact]
    public void Should_Apply_Minimal_ContinuousAggregate_Annotations()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT time_bucket('1 hour', timestamp) AS bucket, COUNT(*) FROM metrics GROUP BY 1",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify all mandatory annotations are set
        Assert.Equal("hourly_metrics", table[ContinuousAggregateAnnotations.MaterializedViewName]);
        Assert.Equal("metrics", table[ContinuousAggregateAnnotations.ParentName]);
        Assert.Equal(false, table[ContinuousAggregateAnnotations.MaterializedOnly]);
        Assert.Equal("SELECT time_bucket('1 hour', timestamp) AS bucket, COUNT(*) FROM metrics GROUP BY 1", table["TimescaleDB:ViewDefinition"]);

        // ChunkInterval should NOT be set when null
        Assert.Null(table[ContinuousAggregateAnnotations.ChunkInterval]);
    }

    #endregion

    #region Should_Apply_MaterializedViewName_Annotation

    [Fact]
    public void Should_Apply_MaterializedViewName_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "daily_aggregates_v2",
            Schema: "analytics",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "raw_data",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("daily_aggregates_v2", table[ContinuousAggregateAnnotations.MaterializedViewName]);
    }

    #endregion

    #region Should_Apply_ParentName_Annotation

    [Fact]
    public void Should_Apply_ParentName_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_view",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "sensor_readings",
            SourceSchema: "sensors",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("sensor_readings", table[ContinuousAggregateAnnotations.ParentName]);
    }

    #endregion

    #region Should_Apply_MaterializedOnly_True

    [Fact]
    public void Should_Apply_MaterializedOnly_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: true,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[ContinuousAggregateAnnotations.MaterializedOnly]);
    }

    #endregion

    #region Should_Apply_MaterializedOnly_False

    [Fact]
    public void Should_Apply_MaterializedOnly_False()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(false, table[ContinuousAggregateAnnotations.MaterializedOnly]);
    }

    #endregion

    #region Should_Apply_ChunkInterval_Annotation

    [Fact]
    public void Should_Apply_ChunkInterval_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: "1 day"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("1 day", table[ContinuousAggregateAnnotations.ChunkInterval]);
    }

    #endregion

    #region Should_Apply_ChunkInterval_With_Various_Formats

    [Theory]
    [InlineData("7 days")]
    [InlineData("1 hour")]
    [InlineData("30 minutes")]
    [InlineData("1 month")]
    [InlineData("00:30:00")]
    public void Should_Apply_ChunkInterval_With_Various_Formats(string chunkInterval)
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "test_view",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: chunkInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(chunkInterval, table[ContinuousAggregateAnnotations.ChunkInterval]);
    }

    #endregion

    #region Should_Not_Apply_ChunkInterval_When_Null

    [Fact]
    public void Should_Not_Apply_ChunkInterval_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ContinuousAggregateAnnotations.ChunkInterval]);
    }

    #endregion

    #region Should_Not_Apply_ChunkInterval_When_Empty

    [Fact]
    public void Should_Not_Apply_ChunkInterval_When_Empty()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: ""
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ContinuousAggregateAnnotations.ChunkInterval]);
    }

    #endregion

    #region Should_Apply_ViewDefinition_Annotation

    [Fact]
    public void Should_Apply_ViewDefinition_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        string viewDefinition = @"SELECT
            time_bucket('1 hour', timestamp) AS bucket,
            device_id,
            AVG(temperature) AS avg_temp,
            MAX(temperature) AS max_temp,
            MIN(temperature) AS min_temp
        FROM sensor_readings
        WHERE device_id IS NOT NULL
        GROUP BY 1, 2";

        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_temps",
            Schema: "public",
            ViewDefinition: viewDefinition,
            SourceHypertableName: "sensor_readings",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(viewDefinition, table["TimescaleDB:ViewDefinition"]);
    }

    #endregion

    #region Should_Apply_All_Annotations_For_Fully_Configured_ContinuousAggregate

    [Fact]
    public void Should_Apply_All_Annotations_For_Fully_Configured_ContinuousAggregate()
    {
        // Arrange
        DatabaseTable table = CreateTable("daily_summary", "analytics");
        string viewDefinition = @"SELECT
            time_bucket('1 day', recorded_at) AS day,
            region_id,
            SUM(sales) AS total_sales,
            COUNT(*) AS transaction_count
        FROM transactions
        GROUP BY 1, 2";

        ContinuousAggregateInfo info = new(
            MaterializedViewName: "daily_summary",
            Schema: "analytics",
            ViewDefinition: viewDefinition,
            SourceHypertableName: "transactions",
            SourceSchema: "sales",
            MaterializedOnly: true,
            ChunkInterval: "7 days"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify ALL annotations are applied
        Assert.Equal("daily_summary", table[ContinuousAggregateAnnotations.MaterializedViewName]);
        Assert.Equal("transactions", table[ContinuousAggregateAnnotations.ParentName]);
        Assert.Equal(true, table[ContinuousAggregateAnnotations.MaterializedOnly]);
        Assert.Equal("7 days", table[ContinuousAggregateAnnotations.ChunkInterval]);
        Assert.Equal(viewDefinition, table["TimescaleDB:ViewDefinition"]);
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
        Assert.Contains("Expected ContinuousAggregateInfo", exception.Message);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Wrong_Info_Type_With_Message

    [Fact]
    public void Should_Throw_ArgumentException_For_Wrong_Info_Type_With_Message()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        int wrongInfo = 42;

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, wrongInfo)
        );

        Assert.Contains("Expected ContinuousAggregateInfo", exception.Message);
        Assert.Contains("Int32", exception.Message);
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

    #region Should_Preserve_Existing_Table_Properties

    [Fact]
    public void Should_Preserve_Existing_Table_Properties()
    {
        // Arrange
        DatabaseTable table = CreateTable("existing_view", "custom_schema");
        table.Comment = "Pre-existing comment";
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "hourly_metrics",
            Schema: "public",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "metrics",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - table properties should be preserved
        Assert.Equal("existing_view", table.Name);
        Assert.Equal("custom_schema", table.Schema);
        Assert.Equal("Pre-existing comment", table.Comment);

        // And annotations should still be applied
        Assert.Equal("hourly_metrics", table[ContinuousAggregateAnnotations.MaterializedViewName]);
    }

    #endregion

    #region Should_Handle_Complex_ViewDefinition_With_Special_Characters

    [Fact]
    public void Should_Handle_Complex_ViewDefinition_With_Special_Characters()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        string viewDefinition = @"SELECT
            time_bucket('1 hour'::interval, ""timestamp"") AS bucket,
            device_id,
            AVG(CASE WHEN value > 0 THEN value ELSE NULL END) AS avg_positive,
            string_agg(DISTINCT tag, ',' ORDER BY tag) AS tags
        FROM ""Metrics""
        WHERE status != 'deleted' AND value IS NOT NULL
        GROUP BY 1, 2
        HAVING COUNT(*) > 0";

        ContinuousAggregateInfo info = new(
            MaterializedViewName: "complex_view",
            Schema: "public",
            ViewDefinition: viewDefinition,
            SourceHypertableName: "Metrics",
            SourceSchema: "public",
            MaterializedOnly: true,
            ChunkInterval: "1 day"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(viewDefinition, table["TimescaleDB:ViewDefinition"]);
    }

    #endregion

    #region Should_Handle_SourceHypertableName_From_Different_Schema

    [Fact]
    public void Should_Handle_SourceHypertableName_From_Different_Schema()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "aggregated_data",
            Schema: "analytics",
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "raw_events",
            SourceSchema: "ingestion",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - ParentName should be the table name, not including schema
        Assert.Equal("raw_events", table[ContinuousAggregateAnnotations.ParentName]);
    }

    #endregion

    #region Should_Apply_Annotations_To_Different_Table_Schemas

    [Theory]
    [InlineData("public")]
    [InlineData("analytics")]
    [InlineData("custom_schema")]
    [InlineData("_timescaledb_internal")]
    public void Should_Apply_Annotations_To_Different_Table_Schemas(string schema)
    {
        // Arrange
        DatabaseTable table = CreateTable("test_view", schema);
        ContinuousAggregateInfo info = new(
            MaterializedViewName: "test_view",
            Schema: schema,
            ViewDefinition: "SELECT ...",
            SourceHypertableName: "source_table",
            SourceSchema: "public",
            MaterializedOnly: false,
            ChunkInterval: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("test_view", table[ContinuousAggregateAnnotations.MaterializedViewName]);
        Assert.Equal("source_table", table[ContinuousAggregateAnnotations.ParentName]);
    }

    #endregion
}

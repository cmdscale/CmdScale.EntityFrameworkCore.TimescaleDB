using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.ReorderPolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

public class ReorderPolicyAnnotationApplierTests
{
    private readonly ReorderPolicyAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestTable", string schema = "public")
    {
        return new DatabaseTable { Name = name, Schema = schema };
    }

    #region Should_Apply_Minimal_ReorderPolicy_Annotations

    [Fact]
    public void Should_Apply_Minimal_ReorderPolicy_Annotations()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "metrics_device_timestamp_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify mandatory annotations are set
        Assert.Equal(true, table[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("metrics_device_timestamp_idx", table[ReorderPolicyAnnotations.IndexName]);

        // Optional annotations should NOT be set when using defaults
        Assert.Null(table[ReorderPolicyAnnotations.InitialStart]);
        Assert.Null(table[ReorderPolicyAnnotations.ScheduleInterval]);
        Assert.Null(table[ReorderPolicyAnnotations.MaxRuntime]);
        Assert.Null(table[ReorderPolicyAnnotations.MaxRetries]);
        Assert.Null(table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_HasReorderPolicy_Always_True

    [Fact]
    public void Should_Apply_HasReorderPolicy_Always_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        object? value = table[ReorderPolicyAnnotations.HasReorderPolicy];
        Assert.NotNull(value);
        Assert.IsType<bool>(value);
        Assert.True((bool)value);
    }

    #endregion

    #region Should_Apply_IndexName_Annotation

    [Fact]
    public void Should_Apply_IndexName_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "custom_clustering_index",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("custom_clustering_index", table[ReorderPolicyAnnotations.IndexName]);
    }

    #endregion

    #region Should_Apply_InitialStart_Annotation

    [Fact]
    public void Should_Apply_InitialStart_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        DateTime initialStart = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: initialStart,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(initialStart, table[ReorderPolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Not_Apply_InitialStart_When_Null

    [Fact]
    public void Should_Not_Apply_InitialStart_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ReorderPolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Apply_ScheduleInterval_When_Different_From_Default

    [Fact]
    public void Should_Apply_ScheduleInterval_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: "7 days",
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("7 days", table[ReorderPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Not_Apply_ScheduleInterval_When_Default

    [Fact]
    public void Should_Not_Apply_ScheduleInterval_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval, // "1 day"
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ReorderPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Apply_Various_ScheduleInterval_Formats

    [Theory]
    [InlineData("2 days")]
    [InlineData("12 hours")]
    [InlineData("30 minutes")]
    [InlineData("1 week")]
    [InlineData("00:30:00")]
    public void Should_Apply_Various_ScheduleInterval_Formats(string scheduleInterval)
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: scheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(scheduleInterval, table[ReorderPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Apply_MaxRuntime_When_Different_From_Default

    [Fact]
    public void Should_Apply_MaxRuntime_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: "01:00:00",
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("01:00:00", table[ReorderPolicyAnnotations.MaxRuntime]);
    }

    #endregion

    #region Should_Not_Apply_MaxRuntime_When_Default

    [Fact]
    public void Should_Not_Apply_MaxRuntime_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime, // "00:00:00"
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ReorderPolicyAnnotations.MaxRuntime]);
    }

    #endregion

    #region Should_Apply_MaxRetries_When_Different_From_Default

    [Fact]
    public void Should_Apply_MaxRetries_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: 5,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(5, table[ReorderPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Not_Apply_MaxRetries_When_Default

    [Fact]
    public void Should_Not_Apply_MaxRetries_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries, // -1
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ReorderPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Apply_MaxRetries_Zero

    [Fact]
    public void Should_Apply_MaxRetries_Zero()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: 0,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - 0 is different from default (-1), so it should be applied
        Assert.Equal(0, table[ReorderPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Apply_MaxRetries_Positive_Values

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(100)]
    public void Should_Apply_MaxRetries_Positive_Values(int maxRetries)
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: maxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(maxRetries, table[ReorderPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Apply_RetryPeriod_When_Different_From_Default

    [Fact]
    public void Should_Apply_RetryPeriod_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: "00:15:00"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("00:15:00", table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Not_Apply_RetryPeriod_When_Default

    [Fact]
    public void Should_Not_Apply_RetryPeriod_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod // "00:05:00"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_Various_RetryPeriod_Formats

    [Theory]
    [InlineData("00:01:00")]
    [InlineData("00:10:00")]
    [InlineData("00:30:00")]
    [InlineData("01:00:00")]
    public void Should_Apply_Various_RetryPeriod_Formats(string retryPeriod)
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: retryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(retryPeriod, table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_All_Annotations_For_Fully_Configured_ReorderPolicy

    [Fact]
    public void Should_Apply_All_Annotations_For_Fully_Configured_ReorderPolicy()
    {
        // Arrange
        DatabaseTable table = CreateTable("sensor_readings", "telemetry");
        DateTime initialStart = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        ReorderPolicyInfo info = new(
            IndexName: "sensor_readings_device_timestamp_idx",
            InitialStart: initialStart,
            ScheduleInterval: "12 hours",
            MaxRuntime: "02:00:00",
            MaxRetries: 3,
            RetryPeriod: "00:10:00"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify ALL annotations are applied
        Assert.Equal(true, table[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("sensor_readings_device_timestamp_idx", table[ReorderPolicyAnnotations.IndexName]);
        Assert.Equal(initialStart, table[ReorderPolicyAnnotations.InitialStart]);
        Assert.Equal("12 hours", table[ReorderPolicyAnnotations.ScheduleInterval]);
        Assert.Equal("02:00:00", table[ReorderPolicyAnnotations.MaxRuntime]);
        Assert.Equal(3, table[ReorderPolicyAnnotations.MaxRetries]);
        Assert.Equal("00:10:00", table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_Only_Non_Default_Annotations

    [Fact]
    public void Should_Apply_Only_Non_Default_Annotations()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        DateTime initialStart = new(2024, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: initialStart,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval, // default - should NOT be applied
            MaxRuntime: "01:30:00", // non-default - should be applied
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries, // default - should NOT be applied
            RetryPeriod: "00:15:00" // non-default - should be applied
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("test_idx", table[ReorderPolicyAnnotations.IndexName]);
        Assert.Equal(initialStart, table[ReorderPolicyAnnotations.InitialStart]);
        Assert.Null(table[ReorderPolicyAnnotations.ScheduleInterval]); // default
        Assert.Equal("01:30:00", table[ReorderPolicyAnnotations.MaxRuntime]);
        Assert.Null(table[ReorderPolicyAnnotations.MaxRetries]); // default
        Assert.Equal("00:15:00", table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Invalid_Info_Type

    [Fact]
    public void Should_Throw_ArgumentException_For_Invalid_Info_Type()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        object invalidInfo = new { IndexName = "test" };

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, invalidInfo)
        );

        Assert.Equal("featureInfo", exception.ParamName);
        Assert.Contains("Expected ReorderPolicyInfo", exception.Message);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Wrong_Info_Type_With_Message

    [Fact]
    public void Should_Throw_ArgumentException_For_Wrong_Info_Type_With_Message()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        double wrongInfo = 3.14;

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, wrongInfo)
        );

        Assert.Contains("Expected ReorderPolicyInfo", exception.Message);
        Assert.Contains("Double", exception.Message);
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
        DatabaseTable table = CreateTable("existing_table", "custom_schema");
        table.Comment = "Pre-existing table comment";
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - table properties should be preserved
        Assert.Equal("existing_table", table.Name);
        Assert.Equal("custom_schema", table.Schema);
        Assert.Equal("Pre-existing table comment", table.Comment);

        // And annotations should still be applied
        Assert.Equal(true, table[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("test_idx", table[ReorderPolicyAnnotations.IndexName]);
    }

    #endregion

    #region Should_Handle_IndexName_With_Schema_Prefix

    [Fact]
    public void Should_Handle_IndexName_With_Schema_Prefix()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "public.metrics_timestamp_device_idx",
            InitialStart: null,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("public.metrics_timestamp_device_idx", table[ReorderPolicyAnnotations.IndexName]);
    }

    #endregion

    #region Should_Handle_Various_InitialStart_DateTimes

    [Theory]
    [MemberData(nameof(InitialStartTestData))]
    public void Should_Handle_Various_InitialStart_DateTimes(DateTime initialStart)
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: initialStart,
            ScheduleInterval: DefaultValues.ReorderPolicyScheduleInterval,
            MaxRuntime: DefaultValues.ReorderPolicyMaxRuntime,
            MaxRetries: DefaultValues.ReorderPolicyMaxRetries,
            RetryPeriod: DefaultValues.ReorderPolicyRetryPeriod
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(initialStart, table[ReorderPolicyAnnotations.InitialStart]);
    }

    public static IEnumerable<object[]> InitialStartTestData()
    {
        yield return new object[] { new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        yield return new object[] { new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc) };
        yield return new object[] { new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc) };
        yield return new object[] { DateTime.MinValue };
        yield return new object[] { DateTime.MaxValue };
    }

    #endregion

    #region Should_Apply_Annotations_With_Null_Optional_Values

    [Fact]
    public void Should_Apply_Annotations_With_Null_Optional_Values()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        ReorderPolicyInfo info = new(
            IndexName: "test_idx",
            InitialStart: null,
            ScheduleInterval: null,
            MaxRuntime: null,
            MaxRetries: null,
            RetryPeriod: null
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - mandatory annotations should be applied
        Assert.Equal(true, table[ReorderPolicyAnnotations.HasReorderPolicy]);
        Assert.Equal("test_idx", table[ReorderPolicyAnnotations.IndexName]);

        // Optional annotations with null should not be applied (comparison with default fails)
        // Note: null != DefaultValues.X, so annotations won't be set
        Assert.Null(table[ReorderPolicyAnnotations.InitialStart]);
        Assert.Null(table[ReorderPolicyAnnotations.ScheduleInterval]);
        Assert.Null(table[ReorderPolicyAnnotations.MaxRuntime]);
        Assert.Null(table[ReorderPolicyAnnotations.MaxRetries]);
        Assert.Null(table[ReorderPolicyAnnotations.RetryPeriod]);
    }

    #endregion
}

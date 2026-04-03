using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.RetentionPolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

public class RetentionPolicyAnnotationApplierTests
{
    private readonly RetentionPolicyAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestTable", string schema = "public")
    {
        return new DatabaseTable { Name = name, Schema = schema };
    }

    #region Should_Apply_Minimal_RetentionPolicy_With_DropAfter_And_All_Defaults

    [Fact]
    public void Should_Apply_Minimal_RetentionPolicy_With_DropAfter_And_All_Defaults()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify mandatory annotations are set
        Assert.Equal(true, table[RetentionPolicyAnnotations.HasRetentionPolicy]);
        Assert.Equal("30 days", table[RetentionPolicyAnnotations.DropAfter]);
        Assert.Null(table[RetentionPolicyAnnotations.DropCreatedBefore]);

        // Optional annotations should NOT be set when using defaults
        Assert.Null(table[RetentionPolicyAnnotations.InitialStart]);
        Assert.Null(table[RetentionPolicyAnnotations.ScheduleInterval]);
        Assert.Null(table[RetentionPolicyAnnotations.MaxRuntime]);
        Assert.Null(table[RetentionPolicyAnnotations.MaxRetries]);
        Assert.Null(table[RetentionPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_Minimal_RetentionPolicy_With_DropCreatedBefore

    [Fact]
    public void Should_Apply_Minimal_RetentionPolicy_With_DropCreatedBefore()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: null,
            DropCreatedBefore: "7 days",
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[RetentionPolicyAnnotations.HasRetentionPolicy]);
        Assert.Null(table[RetentionPolicyAnnotations.DropAfter]);
        Assert.Equal("7 days", table[RetentionPolicyAnnotations.DropCreatedBefore]);
    }

    #endregion

    #region Should_Apply_HasRetentionPolicy_Always_True

    [Fact]
    public void Should_Apply_HasRetentionPolicy_Always_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        object? value = table[RetentionPolicyAnnotations.HasRetentionPolicy];
        Assert.NotNull(value);
        Assert.IsType<bool>(value);
        Assert.True((bool)value);
    }

    #endregion

    #region Should_Apply_InitialStart_Annotation

    [Fact]
    public void Should_Apply_InitialStart_Annotation()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        DateTime initialStart = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: initialStart,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(initialStart, table[RetentionPolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Not_Apply_InitialStart_When_Null

    [Fact]
    public void Should_Not_Apply_InitialStart_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[RetentionPolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Apply_ScheduleInterval_When_Different_From_Default

    [Fact]
    public void Should_Apply_ScheduleInterval_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: "7 days",
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("7 days", table[RetentionPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Not_Apply_ScheduleInterval_When_Default

    [Fact]
    public void Should_Not_Apply_ScheduleInterval_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval, // "1 day"
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[RetentionPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Apply_MaxRuntime_When_Different_From_Default

    [Fact]
    public void Should_Apply_MaxRuntime_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: "01:00:00",
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("01:00:00", table[RetentionPolicyAnnotations.MaxRuntime]);
    }

    #endregion

    #region Should_Not_Apply_MaxRuntime_When_Default

    [Fact]
    public void Should_Not_Apply_MaxRuntime_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime, // "00:00:00"
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[RetentionPolicyAnnotations.MaxRuntime]);
    }

    #endregion

    #region Should_Apply_MaxRetries_When_Different_From_Default

    [Fact]
    public void Should_Apply_MaxRetries_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: 5,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(5, table[RetentionPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Not_Apply_MaxRetries_When_Default

    [Fact]
    public void Should_Not_Apply_MaxRetries_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries, // -1
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[RetentionPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Apply_MaxRetries_Zero

    [Fact]
    public void Should_Apply_MaxRetries_Zero()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: 0,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - 0 is different from default (-1), so it should be applied
        Assert.Equal(0, table[RetentionPolicyAnnotations.MaxRetries]);
    }

    #endregion

    #region Should_Apply_RetryPeriod_When_Different_From_Default

    [Fact]
    public void Should_Apply_RetryPeriod_When_Different_From_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: "00:15:00"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("00:15:00", table[RetentionPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Not_Apply_RetryPeriod_When_Default

    [Fact]
    public void Should_Not_Apply_RetryPeriod_When_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval // "1 day"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[RetentionPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_All_Annotations_For_Fully_Configured_RetentionPolicy

    [Fact]
    public void Should_Apply_All_Annotations_For_Fully_Configured_RetentionPolicy()
    {
        // Arrange
        DatabaseTable table = CreateTable("sensor_readings", "telemetry");
        DateTime initialStart = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        RetentionPolicyInfo info = new(
            DropAfter: "90 days",
            DropCreatedBefore: null,
            InitialStart: initialStart,
            ScheduleInterval: "12 hours",
            MaxRuntime: "02:00:00",
            MaxRetries: 3,
            RetryPeriod: "00:10:00"
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - verify ALL annotations are applied
        Assert.Equal(true, table[RetentionPolicyAnnotations.HasRetentionPolicy]);
        Assert.Equal("90 days", table[RetentionPolicyAnnotations.DropAfter]);
        Assert.Null(table[RetentionPolicyAnnotations.DropCreatedBefore]);
        Assert.Equal(initialStart, table[RetentionPolicyAnnotations.InitialStart]);
        Assert.Equal("12 hours", table[RetentionPolicyAnnotations.ScheduleInterval]);
        Assert.Equal("02:00:00", table[RetentionPolicyAnnotations.MaxRuntime]);
        Assert.Equal(3, table[RetentionPolicyAnnotations.MaxRetries]);
        Assert.Equal("00:10:00", table[RetentionPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Apply_Only_Non_Default_Annotations

    [Fact]
    public void Should_Apply_Only_Non_Default_Annotations()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        DateTime initialStart = new(2024, 3, 15, 8, 0, 0, DateTimeKind.Utc);
        RetentionPolicyInfo info = new(
            DropAfter: "60 days",
            DropCreatedBefore: null,
            InitialStart: initialStart,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval, // default - should NOT be applied
            MaxRuntime: "01:30:00", // non-default - should be applied
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries, // default - should NOT be applied
            RetryPeriod: "00:15:00" // non-default - should be applied
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[RetentionPolicyAnnotations.HasRetentionPolicy]);
        Assert.Equal("60 days", table[RetentionPolicyAnnotations.DropAfter]);
        Assert.Equal(initialStart, table[RetentionPolicyAnnotations.InitialStart]);
        Assert.Null(table[RetentionPolicyAnnotations.ScheduleInterval]); // default
        Assert.Equal("01:30:00", table[RetentionPolicyAnnotations.MaxRuntime]);
        Assert.Null(table[RetentionPolicyAnnotations.MaxRetries]); // default
        Assert.Equal("00:15:00", table[RetentionPolicyAnnotations.RetryPeriod]);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Invalid_Info_Type

    [Fact]
    public void Should_Throw_ArgumentException_For_Invalid_Info_Type()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        object invalidInfo = new { DropAfter = "30 days" };

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, invalidInfo)
        );

        Assert.Equal("featureInfo", exception.ParamName);
        Assert.Contains("Expected RetentionPolicyInfo", exception.Message);
    }

    #endregion

    #region Should_Preserve_Existing_Table_Properties

    [Fact]
    public void Should_Preserve_Existing_Table_Properties()
    {
        // Arrange
        DatabaseTable table = CreateTable("existing_table", "custom_schema");
        table.Comment = "Pre-existing table comment";
        RetentionPolicyInfo info = new(
            DropAfter: "30 days",
            DropCreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: DefaultValues.RetentionPolicyScheduleInterval,
            MaxRuntime: DefaultValues.RetentionPolicyMaxRuntime,
            MaxRetries: DefaultValues.RetentionPolicyMaxRetries,
            RetryPeriod: DefaultValues.RetentionPolicyScheduleInterval
        );

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert - table properties should be preserved
        Assert.Equal("existing_table", table.Name);
        Assert.Equal("custom_schema", table.Schema);
        Assert.Equal("Pre-existing table comment", table.Comment);

        // And annotations should still be applied
        Assert.Equal(true, table[RetentionPolicyAnnotations.HasRetentionPolicy]);
        Assert.Equal("30 days", table[RetentionPolicyAnnotations.DropAfter]);
    }

    #endregion
}

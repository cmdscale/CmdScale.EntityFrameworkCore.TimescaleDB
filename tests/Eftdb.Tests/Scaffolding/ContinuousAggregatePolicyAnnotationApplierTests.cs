using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.ContinuousAggregatePolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Scaffolding;

/// <summary>
/// Tests that verify ContinuousAggregatePolicyAnnotationApplier correctly applies annotations
/// to scaffolded database tables from extracted policy info.
/// </summary>
public class ContinuousAggregatePolicyAnnotationApplierTests
{
    private readonly ContinuousAggregatePolicyAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestView", string schema = "public")
    {
        return new DatabaseTable { Name = name, Schema = schema };
    }

    #region Should_Apply_HasRefreshPolicy_Always_True

    [Fact]
    public void Should_Apply_HasRefreshPolicy_Always_True()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        object? value = table[ContinuousAggregatePolicyAnnotations.HasRefreshPolicy];
        Assert.NotNull(value);
        Assert.IsType<bool>(value);
        Assert.True((bool)value);
    }

    #endregion

    #region Should_Apply_StartOffset_Annotation

    [Fact]
    public void Should_Apply_StartOffset_Annotation()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "7 days",
            EndOffset: null,
            ScheduleInterval: null,
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal("7 days", table[ContinuousAggregatePolicyAnnotations.StartOffset]);
    }

    #endregion

    #region Should_Not_Apply_StartOffset_When_Null

    [Fact]
    public void Should_Not_Apply_StartOffset_When_Null()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: null,
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.StartOffset]);
    }

    #endregion

    #region Should_Apply_EndOffset_Annotation

    [Fact]
    public void Should_Apply_EndOffset_Annotation()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: null,
            EndOffset: "30 minutes",
            ScheduleInterval: null,
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal("30 minutes", table[ContinuousAggregatePolicyAnnotations.EndOffset]);
    }

    #endregion

    #region Should_Not_Apply_EndOffset_When_Null

    [Fact]
    public void Should_Not_Apply_EndOffset_When_Null()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: null,
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.EndOffset]);
    }

    #endregion

    #region Should_Apply_ScheduleInterval_Annotation

    [Fact]
    public void Should_Apply_ScheduleInterval_Annotation()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: null,
            EndOffset: null,
            ScheduleInterval: "2 hours",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal("2 hours", table[ContinuousAggregatePolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Apply_InitialStart_Annotation

    [Fact]
    public void Should_Apply_InitialStart_Annotation()
    {
        DatabaseTable table = CreateTable();
        DateTime initialStart = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: initialStart,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(initialStart, table[ContinuousAggregatePolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Not_Apply_InitialStart_When_Null

    [Fact]
    public void Should_Not_Apply_InitialStart_When_Null()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Apply_IncludeTieredData_When_Not_Null

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Apply_IncludeTieredData_When_Not_Null(bool includeTieredData)
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: includeTieredData,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(includeTieredData, table[ContinuousAggregatePolicyAnnotations.IncludeTieredData]);
    }

    #endregion

    #region Should_Not_Apply_IncludeTieredData_When_Null

    [Fact]
    public void Should_Not_Apply_IncludeTieredData_When_Null()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.IncludeTieredData]);
    }

    #endregion

    #region Should_Not_Apply_BucketsPerBatch_When_Default

    [Fact]
    public void Should_Not_Apply_BucketsPerBatch_When_Default()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: 1, // default value
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.BucketsPerBatch]);
    }

    #endregion

    #region Should_Apply_BucketsPerBatch_When_Non_Default

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(100)]
    public void Should_Apply_BucketsPerBatch_When_Non_Default(int bucketsPerBatch)
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: bucketsPerBatch,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(bucketsPerBatch, table[ContinuousAggregatePolicyAnnotations.BucketsPerBatch]);
    }

    #endregion

    #region Should_Not_Apply_MaxBatchesPerExecution_When_Default

    [Fact]
    public void Should_Not_Apply_MaxBatchesPerExecution_When_Default()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: 0, // default value
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution]);
    }

    #endregion

    #region Should_Apply_MaxBatchesPerExecution_When_Non_Default

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(50)]
    public void Should_Apply_MaxBatchesPerExecution_When_Non_Default(int maxBatches)
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: maxBatches,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(maxBatches, table[ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution]);
    }

    #endregion

    #region Should_Not_Apply_RefreshNewestFirst_When_Default

    [Fact]
    public void Should_Not_Apply_RefreshNewestFirst_When_Default()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: true // default value
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.RefreshNewestFirst]);
    }

    #endregion

    #region Should_Apply_RefreshNewestFirst_When_Non_Default

    [Fact]
    public void Should_Apply_RefreshNewestFirst_When_Non_Default()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: false
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(false, table[ContinuousAggregatePolicyAnnotations.RefreshNewestFirst]);
    }

    #endregion

    #region Should_Apply_All_Annotations_For_Fully_Configured_Policy

    [Fact]
    public void Should_Apply_All_Annotations_For_Fully_Configured_Policy()
    {
        DatabaseTable table = CreateTable("hourly_metrics", "telemetry");
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "7 days",
            EndOffset: "1 hour",
            ScheduleInterval: "30 minutes",
            InitialStart: initialStart,
            IncludeTieredData: true,
            BucketsPerBatch: 5,
            MaxBatchesPerExecution: 10,
            RefreshNewestFirst: false
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(true, table[ContinuousAggregatePolicyAnnotations.HasRefreshPolicy]);
        Assert.Equal("7 days", table[ContinuousAggregatePolicyAnnotations.StartOffset]);
        Assert.Equal("1 hour", table[ContinuousAggregatePolicyAnnotations.EndOffset]);
        Assert.Equal("30 minutes", table[ContinuousAggregatePolicyAnnotations.ScheduleInterval]);
        Assert.Equal(initialStart, table[ContinuousAggregatePolicyAnnotations.InitialStart]);
        Assert.Equal(true, table[ContinuousAggregatePolicyAnnotations.IncludeTieredData]);
        Assert.Equal(5, table[ContinuousAggregatePolicyAnnotations.BucketsPerBatch]);
        Assert.Equal(10, table[ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution]);
        Assert.Equal(false, table[ContinuousAggregatePolicyAnnotations.RefreshNewestFirst]);
    }

    #endregion

    #region Should_Apply_Only_Non_Default_Annotations

    [Fact]
    public void Should_Apply_Only_Non_Default_Annotations()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: 1,            // default - should NOT be applied
            MaxBatchesPerExecution: 0,      // default - should NOT be applied
            RefreshNewestFirst: true        // default - should NOT be applied
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal(true, table[ContinuousAggregatePolicyAnnotations.HasRefreshPolicy]);
        Assert.Equal("1 month", table[ContinuousAggregatePolicyAnnotations.StartOffset]);
        Assert.Equal("1 hour", table[ContinuousAggregatePolicyAnnotations.EndOffset]);
        Assert.Equal("1 hour", table[ContinuousAggregatePolicyAnnotations.ScheduleInterval]);
        Assert.Null(table[ContinuousAggregatePolicyAnnotations.InitialStart]);
        Assert.Null(table[ContinuousAggregatePolicyAnnotations.IncludeTieredData]);
        Assert.Null(table[ContinuousAggregatePolicyAnnotations.BucketsPerBatch]);
        Assert.Null(table[ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution]);
        Assert.Null(table[ContinuousAggregatePolicyAnnotations.RefreshNewestFirst]);
    }

    #endregion

    #region Should_Throw_ArgumentException_For_Invalid_Info_Type

    [Fact]
    public void Should_Throw_ArgumentException_For_Invalid_Info_Type()
    {
        DatabaseTable table = CreateTable();
        object invalidInfo = new { StartOffset = "1 month" };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, invalidInfo)
        );

        Assert.Equal("featureInfo", exception.ParamName);
        Assert.Contains("ContinuousAggregatePolicyInfo", exception.Message);
    }

    #endregion

    #region Should_Preserve_Existing_Table_Properties

    [Fact]
    public void Should_Preserve_Existing_Table_Properties()
    {
        DatabaseTable table = CreateTable("hourly_metrics", "analytics");
        table.Comment = "Continuous aggregate for hourly metrics";
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Equal("hourly_metrics", table.Name);
        Assert.Equal("analytics", table.Schema);
        Assert.Equal("Continuous aggregate for hourly metrics", table.Comment);
        Assert.Equal(true, table[ContinuousAggregatePolicyAnnotations.HasRefreshPolicy]);
    }

    #endregion

    #region Should_Not_Apply_BucketsPerBatch_When_Null

    [Fact]
    public void Should_Not_Apply_BucketsPerBatch_When_Null()
    {
        DatabaseTable table = CreateTable();
        ContinuousAggregatePolicyInfo info = new(
            StartOffset: "1 month",
            EndOffset: "1 hour",
            ScheduleInterval: "1 hour",
            InitialStart: null,
            IncludeTieredData: null,
            BucketsPerBatch: null,
            MaxBatchesPerExecution: null,
            RefreshNewestFirst: null
        );

        _applier.ApplyAnnotations(table, info);

        Assert.Null(table[ContinuousAggregatePolicyAnnotations.BucketsPerBatch]);
    }

    #endregion
}

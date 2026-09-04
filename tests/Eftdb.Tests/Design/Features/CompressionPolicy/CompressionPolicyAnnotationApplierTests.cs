using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy.CompressionPolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.CompressionPolicy;

public class CompressionPolicyAnnotationApplierTests
{
    private readonly CompressionPolicyAnnotationApplier _applier = new();

    private static DatabaseTable CreateTable(string name = "TestTable", string schema = "public")
        => new() { Name = name, Schema = schema };

    // ── Guard ──────────────────────────────────────────────────────────────────

    #region Should_Throw_ArgumentException_For_Invalid_Info_Type

    [Fact]
    public void Should_Throw_ArgumentException_For_Invalid_Info_Type()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        object invalidInfo = new { After = "7 days" };

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => _applier.ApplyAnnotations(table, invalidInfo));

        Assert.Equal("featureInfo", ex.ParamName);
        Assert.Contains("CompressionPolicyInfo", ex.Message);
    }

    #endregion

    // ── HasCompressionPolicy ───────────────────────────────────────────────────

    #region Should_Set_HasCompressionPolicy_True

    [Fact]
    public void Should_Set_HasCompressionPolicy_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[CompressionPolicyAnnotations.HasCompressionPolicy]);
    }

    #endregion

    // ── After ─────────────────────────────────────────────────────────────────

    #region Should_Apply_After_When_Set

    [Fact]
    public void Should_Apply_After_When_Set()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("7 days", table[CompressionPolicyAnnotations.After]);
    }

    #endregion

    #region Should_Not_Apply_After_When_Null

    [Fact]
    public void Should_Not_Apply_After_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: null,
            CreatedBefore: "30 days",
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[CompressionPolicyAnnotations.After]);
    }

    #endregion

    // ── CreatedBefore ─────────────────────────────────────────────────────────

    #region Should_Apply_CreatedBefore_When_Set

    [Fact]
    public void Should_Apply_CreatedBefore_When_Set()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: null,
            CreatedBefore: "30 days",
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("30 days", table[CompressionPolicyAnnotations.CreatedBefore]);
    }

    #endregion

    // ── InitialStart ──────────────────────────────────────────────────────────

    #region Should_Apply_InitialStart_When_Set

    [Fact]
    public void Should_Apply_InitialStart_When_Set()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        DateTime start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: start,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(start, table[CompressionPolicyAnnotations.InitialStart]);
    }

    #endregion

    #region Should_Not_Apply_InitialStart_When_Null

    [Fact]
    public void Should_Not_Apply_InitialStart_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[CompressionPolicyAnnotations.InitialStart]);
    }

    #endregion

    // ── ScheduleInterval ──────────────────────────────────────────────────────

    #region Should_Apply_ScheduleInterval_When_Differs_From_Computed_Default

    [Fact]
    public void Should_Apply_ScheduleInterval_When_Differs_From_Computed_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        table[HypertableAnnotations.ChunkTimeInterval] = "7 days";
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: "6 hours",
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("6 hours", table[CompressionPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    #region Should_Not_Apply_ScheduleInterval_When_Matches_Computed_Default

    [Fact]
    public void Should_Not_Apply_ScheduleInterval_When_Matches_Computed_Default()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        table[HypertableAnnotations.ChunkTimeInterval] = "7 days";
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: "12 hours",
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[CompressionPolicyAnnotations.ScheduleInterval]);
    }

    #endregion

    // ── Timezone ──────────────────────────────────────────────────────────────

    #region Should_Apply_Timezone_When_Set

    [Fact]
    public void Should_Apply_Timezone_When_Set()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: "Europe/Berlin",
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal("Europe/Berlin", table[CompressionPolicyAnnotations.Timezone]);
    }

    #endregion

    #region Should_Not_Apply_Timezone_When_Null

    [Fact]
    public void Should_Not_Apply_Timezone_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[CompressionPolicyAnnotations.Timezone]);
    }

    #endregion

    // ── IfNotExists ───────────────────────────────────────────────────────────

    #region Should_Apply_IfNotExists_When_True

    [Fact]
    public void Should_Apply_IfNotExists_When_True()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: true);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(true, table[CompressionPolicyAnnotations.IfNotExists]);
    }

    #endregion

    #region Should_Apply_IfNotExists_When_False

    [Fact]
    public void Should_Apply_IfNotExists_When_False()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: false);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Equal(false, table[CompressionPolicyAnnotations.IfNotExists]);
    }

    #endregion

    #region Should_Not_Apply_IfNotExists_When_Null

    [Fact]
    public void Should_Not_Apply_IfNotExists_When_Null()
    {
        // Arrange
        DatabaseTable table = CreateTable();
        CompressionPolicyInfo info = new(
            After: "7 days",
            CreatedBefore: null,
            InitialStart: null,
            ScheduleInterval: null,
            Timezone: null,
            IfNotExists: null);

        // Act
        _applier.ApplyAnnotations(table, info);

        // Assert
        Assert.Null(table[CompressionPolicyAnnotations.IfNotExists]);
    }

    #endregion
}

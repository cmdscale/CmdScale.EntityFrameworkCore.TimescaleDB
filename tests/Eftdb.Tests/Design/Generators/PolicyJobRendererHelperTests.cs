using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

/// <summary>
/// Unit tests for <see cref="PolicyJobRendererHelper"/> covering
/// <see cref="PolicyJobRendererHelper.ChainInitialStart"/>,
/// <see cref="PolicyJobRendererHelper.ChainScheduleInterval"/>, and
/// <see cref="PolicyJobRendererHelper.AddInitialStartNamedArg"/>.
/// No DbContext or database is required — all methods operate on annotation
/// dictionaries and <see cref="MethodCallCodeFragment"/> instances.
/// </summary>
public class PolicyJobRendererHelperTests
{
    private sealed record StubAnnotation(string Name, object? Value) : IAnnotation;

    private static Dictionary<string, IAnnotation> Annotations(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => (IAnnotation)new StubAnnotation(p.Key, p.Value));

    private static readonly MethodInfo WithChunkIntervalMethod =
        typeof(ContinuousAggregateStringBuilder<object>)
            .GetMethod(nameof(ContinuousAggregateStringBuilder<>.WithChunkInterval),
                BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly MethodInfo WithInitialStartMethod =
        typeof(ContinuousAggregatePolicyStringBuilder<object>)
            .GetMethod(nameof(ContinuousAggregatePolicyStringBuilder<object>.WithInitialStart),
                BindingFlags.Public | BindingFlags.Instance)!;

    private static MethodCallCodeFragment RootFragment() => new(WithChunkIntervalMethod, "1 hour");

    // ── ChainInitialStart ────────────────────────────────────────────────────

    #region ChainInitialStart_Chains_Method_When_DateTime_Present

    [Fact]
    public void ChainInitialStart_Chains_Method_When_DateTime_Present()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        DateTime initialStart = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(("test:InitialStart", initialStart));

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainInitialStart(
            root, annotations, "test:InitialStart", WithInitialStartMethod);

        // Assert
        Assert.NotNull(result.ChainedCall);
        Assert.Equal(WithInitialStartMethod.Name, result.ChainedCall.Method);
        Assert.Equal(initialStart, result.ChainedCall.Arguments[0]);
    }

    #endregion

    #region ChainInitialStart_Returns_Unchanged_When_Absent

    [Fact]
    public void ChainInitialStart_Returns_Unchanged_When_Absent()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        Dictionary<string, IAnnotation> annotations = Annotations();

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainInitialStart(
            root, annotations, "test:InitialStart", WithInitialStartMethod);

        // Assert
        Assert.Null(result.ChainedCall);
        Assert.Same(root, result);
    }

    #endregion

    #region ChainInitialStart_Returns_Unchanged_When_Value_Not_DateTime

    [Fact]
    public void ChainInitialStart_Returns_Unchanged_When_Value_Not_DateTime()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        Dictionary<string, IAnnotation> annotations = Annotations(("test:InitialStart", "2025-06-01T00:00:00Z"));

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainInitialStart(
            root, annotations, "test:InitialStart", WithInitialStartMethod);

        // Assert
        Assert.Null(result.ChainedCall);
        Assert.Same(root, result);
    }

    #endregion

    // ── ChainScheduleInterval ────────────────────────────────────────────────

    #region ChainScheduleInterval_Chains_Method_When_String_Present

    [Fact]
    public void ChainScheduleInterval_Chains_Method_When_String_Present()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        Dictionary<string, IAnnotation> annotations = Annotations(("test:ScheduleInterval", "24 hours"));

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainScheduleInterval(
            root, annotations, "test:ScheduleInterval", WithChunkIntervalMethod);

        // Assert
        Assert.NotNull(result.ChainedCall);
        Assert.Equal(WithChunkIntervalMethod.Name, result.ChainedCall.Method);
        Assert.Equal("24 hours", result.ChainedCall.Arguments[0]);
    }

    #endregion

    #region ChainScheduleInterval_Returns_Unchanged_When_Absent

    [Fact]
    public void ChainScheduleInterval_Returns_Unchanged_When_Absent()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        Dictionary<string, IAnnotation> annotations = Annotations();

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainScheduleInterval(
            root, annotations, "test:ScheduleInterval", WithChunkIntervalMethod);

        // Assert
        Assert.Null(result.ChainedCall);
        Assert.Same(root, result);
    }

    #endregion

    #region ChainScheduleInterval_Returns_Unchanged_When_Whitespace

    [Fact]
    public void ChainScheduleInterval_Returns_Unchanged_When_Whitespace()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        Dictionary<string, IAnnotation> annotations = Annotations(("test:ScheduleInterval", "   "));

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainScheduleInterval(
            root, annotations, "test:ScheduleInterval", WithChunkIntervalMethod);

        // Assert
        Assert.Null(result.ChainedCall);
        Assert.Same(root, result);
    }

    #endregion

    #region ChainScheduleInterval_Returns_Unchanged_When_Value_Not_String

    [Fact]
    public void ChainScheduleInterval_Returns_Unchanged_When_Value_Not_String()
    {
        // Arrange
        MethodCallCodeFragment root = RootFragment();
        Dictionary<string, IAnnotation> annotations = Annotations(("test:ScheduleInterval", 3600));

        // Act
        MethodCallCodeFragment result = PolicyJobRendererHelper.ChainScheduleInterval(
            root, annotations, "test:ScheduleInterval", WithChunkIntervalMethod);

        // Assert
        Assert.Null(result.ChainedCall);
        Assert.Same(root, result);
    }

    #endregion

    // ── AddInitialStartNamedArg ───────────────────────────────────────────────

    #region AddInitialStartNamedArg_Adds_ISO8601_String_For_UTC_DateTime

    [Fact]
    public void AddInitialStartNamedArg_Adds_ISO8601_String_For_UTC_DateTime()
    {
        // Arrange
        DateTime utcTime = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<string, IAnnotation> annotations = Annotations(("test:InitialStart", utcTime));
        Dictionary<string, object?> namedArgs = [];

        // Act
        PolicyJobRendererHelper.AddInitialStartNamedArg(annotations, "test:InitialStart", "InitialStart", namedArgs);

        // Assert
        Assert.True(namedArgs.ContainsKey("InitialStart"));
        string? value = namedArgs["InitialStart"] as string;
        Assert.NotNull(value);
        Assert.EndsWith("Z", value, StringComparison.Ordinal);
        DateTime parsed = DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(utcTime, parsed);
    }

    #endregion

    #region AddInitialStartNamedArg_Converts_Local_DateTime_To_UTC

    [Fact]
    public void AddInitialStartNamedArg_Converts_Local_DateTime_To_UTC()
    {
        // Arrange
        DateTime localTime = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);
        Dictionary<string, IAnnotation> annotations = Annotations(("test:InitialStart", localTime));
        Dictionary<string, object?> namedArgs = [];

        // Act
        PolicyJobRendererHelper.AddInitialStartNamedArg(annotations, "test:InitialStart", "InitialStart", namedArgs);

        // Assert
        string? value = namedArgs["InitialStart"] as string;
        Assert.NotNull(value);
        Assert.EndsWith("Z", value, StringComparison.Ordinal);

        DateTime parsed = DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.Equal(localTime.ToUniversalTime(), parsed);
    }

    #endregion

    #region AddInitialStartNamedArg_Does_Nothing_When_Annotation_Absent

    [Fact]
    public void AddInitialStartNamedArg_Does_Nothing_When_Annotation_Absent()
    {
        // Arrange
        Dictionary<string, IAnnotation> annotations = Annotations();
        Dictionary<string, object?> namedArgs = [];

        // Act
        PolicyJobRendererHelper.AddInitialStartNamedArg(annotations, "test:InitialStart", "InitialStart", namedArgs);

        // Assert
        Assert.Empty(namedArgs);
    }

    #endregion

    #region AddInitialStartNamedArg_Does_Nothing_When_Value_Not_DateTime

    [Fact]
    public void AddInitialStartNamedArg_Does_Nothing_When_Value_Not_DateTime()
    {
        // Arrange
        Dictionary<string, IAnnotation> annotations = Annotations(("test:InitialStart", "2025-06-01T00:00:00Z"));
        Dictionary<string, object?> namedArgs = [];

        // Act
        PolicyJobRendererHelper.AddInitialStartNamedArg(annotations, "test:InitialStart", "InitialStart", namedArgs);

        // Assert
        Assert.Empty(namedArgs);
    }

    #endregion
}

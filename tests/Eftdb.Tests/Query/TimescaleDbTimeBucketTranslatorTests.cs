using System.Reflection;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Query;

#pragma warning disable EF1001 // Internal EF Core API usage.

public class TimescaleDbTimeBucketTranslatorTests
{
    private readonly Mock<ISqlExpressionFactory> _sqlExpressionFactoryMock = new();
    private readonly Mock<IDiagnosticsLogger<DbLoggerCategory.Query>> _loggerMock = new();
    private readonly TimescaleDbTimeBucketTranslator _translator;

    private string? _capturedFunctionName;
    private int _capturedArgumentCount;
    private Type? _capturedReturnType;
    private bool _capturedNullable;
    private bool[]? _capturedNullability;

    public TimescaleDbTimeBucketTranslatorTests()
    {
        _translator = new TimescaleDbTimeBucketTranslator(_sqlExpressionFactoryMock.Object);

        _sqlExpressionFactoryMock
            .Setup(f => f.Function(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<SqlExpression>>(),
                It.IsAny<bool>(),
                It.IsAny<IEnumerable<bool>>(),
                It.IsAny<Type>(),
                It.IsAny<RelationalTypeMapping?>()))
            .Callback<string, IEnumerable<SqlExpression>, bool, IEnumerable<bool>, Type, RelationalTypeMapping?>(
                (name, args, nullable, nullability, returnType, _) =>
                {
                    _capturedFunctionName = name;
                    _capturedArgumentCount = args.Count();
                    _capturedNullable = nullable;
                    _capturedNullability = [.. nullability];
                    _capturedReturnType = returnType;
                })
            .Returns((SqlFunctionExpression)null!);
    }

    private static SqlExpression CreateMockArgument()
    {
        Mock<SqlExpression> mock = new(typeof(int), null!);
        return mock.Object;
    }

    private void AssertTimeBucketTranslation(int expectedArgCount, Type expectedReturnType)
    {
        Assert.Equal("time_bucket", _capturedFunctionName);
        Assert.Equal(expectedArgCount, _capturedArgumentCount);
        Assert.True(_capturedNullable);
        Assert.NotNull(_capturedNullability);
        Assert.Equal(expectedArgCount, _capturedNullability!.Length);
        Assert.All(_capturedNullability, n => Assert.True(n));
        Assert.Equal(expectedReturnType, _capturedReturnType);
    }

    #region Should_Return_Null_For_Non_Matching_Method

    [Fact]
    public void Should_Return_Null_For_Non_Matching_Method()
    {
        // Arrange
        MethodInfo nonMatchingMethod = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(int)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument()];

        // Act
        SqlExpression? result = _translator.Translate(null, nonMatchingMethod, arguments, _loggerMock.Object);

        // Assert
        Assert.Null(result);
        Assert.Null(_capturedFunctionName);
    }

    #endregion

    #region Should_Translate_TimeBucket_TimeSpan_DateTime

    [Fact]
    public void Should_Translate_TimeBucket_TimeSpan_DateTime()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTime)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(2, typeof(DateTime));
    }

    #endregion

    #region Should_Translate_TimeBucket_TimeSpan_DateTimeOffset

    [Fact]
    public void Should_Translate_TimeBucket_TimeSpan_DateTimeOffset()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTimeOffset)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(2, typeof(DateTimeOffset));
    }

    #endregion

    #region Should_Translate_TimeBucket_TimeSpan_DateOnly

    [Fact]
    public void Should_Translate_TimeBucket_TimeSpan_DateOnly()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateOnly)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(2, typeof(DateOnly));
    }

    #endregion

    #region Should_Translate_TimeBucket_TimeSpan_DateTime_Offset

    [Fact]
    public void Should_Translate_TimeBucket_TimeSpan_DateTime_Offset()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTime), typeof(TimeSpan)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(3, typeof(DateTime));
    }

    #endregion

    #region Should_Translate_TimeBucket_TimeSpan_DateTimeOffset_Offset

    [Fact]
    public void Should_Translate_TimeBucket_TimeSpan_DateTimeOffset_Offset()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTimeOffset), typeof(TimeSpan)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(3, typeof(DateTimeOffset));
    }

    #endregion

    #region Should_Translate_TimeBucket_TimeSpan_DateTimeOffset_Timezone

    [Fact]
    public void Should_Translate_TimeBucket_TimeSpan_DateTimeOffset_Timezone()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTimeOffset), typeof(string)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(3, typeof(DateTimeOffset));
    }

    #endregion

    #region Should_Translate_TimeBucket_Int_Int

    [Fact]
    public void Should_Translate_TimeBucket_Int_Int()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(int), typeof(int)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(2, typeof(int));
    }

    #endregion

    #region Should_Translate_TimeBucket_Int_Int_Offset

    [Fact]
    public void Should_Translate_TimeBucket_Int_Int_Offset()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(int), typeof(int), typeof(int)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(3, typeof(int));
    }

    #endregion

    #region Should_Translate_TimeBucket_Long_Long

    [Fact]
    public void Should_Translate_TimeBucket_Long_Long()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(long), typeof(long)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(2, typeof(long));
    }

    #endregion

    #region Should_Translate_TimeBucket_Long_Long_Offset

    [Fact]
    public void Should_Translate_TimeBucket_Long_Long_Offset()
    {
        // Arrange
        MethodInfo method = typeof(TimescaleDbFunctionsExtensions).GetMethod(
            nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(long), typeof(long), typeof(long)])!;
        List<SqlExpression> arguments = [CreateMockArgument(), CreateMockArgument(), CreateMockArgument(), CreateMockArgument()];

        // Act
        _translator.Translate(null, method, arguments, _loggerMock.Object);

        // Assert
        AssertTimeBucketTranslation(3, typeof(long));
    }

    #endregion
}

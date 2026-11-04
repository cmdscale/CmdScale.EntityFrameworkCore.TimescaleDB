using System.Reflection;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Moq;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Query;

#pragma warning disable EF1001 // Internal EF Core API usage.

public class TimescaleDbFirstLastTranslatorTests
{
    private readonly Mock<IDiagnosticsLogger<DbLoggerCategory.Query>> _loggerMock = new();
    private readonly TimescaleDbFirstLastTranslator _translator = new(null!);

    private static SqlExpression CreateMockArgument(Type type)
    {
        Mock<SqlExpression> mock = new(type, null!);
        return mock.Object;
    }

    private static EnumerableExpression CreateRowSource(int valueCount = 2)
    {
        List<SqlExpression> values = [];
        for (int i = 0; i < valueCount; i++)
        {
            values.Add(CreateMockArgument(i == 0 ? typeof(double) : typeof(DateTime)));
        }

        return new EnumerableExpression(new PgRowValueExpression(values, typeof(ValueTuple<double, DateTime>)));
    }

    private static MethodInfo GetMethod(string name)
        => typeof(TimescaleDbFunctionsExtensions).GetMethod(name)!.MakeGenericMethod(typeof(double), typeof(DateTime));

    #region Should_Return_Null_For_Non_Matching_Method

    [Fact]
    public void Should_Return_Null_For_Non_Matching_Method()
    {
        // Arrange
        MethodInfo nonMatchingMethod = typeof(Math).GetMethod(nameof(Math.Abs), [typeof(int)])!;

        // Act
        SqlExpression? result = _translator.Translate(nonMatchingMethod, CreateRowSource(), [], _loggerMock.Object);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_For_Generic_Method_Of_Another_Type

    [Fact]
    public void Should_Return_Null_For_Generic_Method_Of_Another_Type()
    {
        // Arrange
        MethodInfo otherGenericMethod = typeof(Enumerable)
            .GetMethod(nameof(Enumerable.First), 1, [typeof(IEnumerable<>).MakeGenericType(Type.MakeGenericMethodParameter(0))])!
            .MakeGenericMethod(typeof(double));

        // Act
        SqlExpression? result = _translator.Translate(otherGenericMethod, CreateRowSource(), [], _loggerMock.Object);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Throw_When_Source_Is_Distinct

    [Theory]
    [InlineData(nameof(TimescaleDbFunctionsExtensions.First))]
    [InlineData(nameof(TimescaleDbFunctionsExtensions.Last))]
    public void Should_Throw_When_Source_Is_Distinct(string methodName)
    {
        // Arrange
        EnumerableExpression source = CreateRowSource().SetDistinct(true);

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => _translator.Translate(GetMethod(methodName), source, [], _loggerMock.Object));

        // Assert
        Assert.Contains($"EF.Functions.{methodName} does not support Distinct or OrderBy", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Source_Is_Ordered

    [Theory]
    [InlineData(nameof(TimescaleDbFunctionsExtensions.First))]
    [InlineData(nameof(TimescaleDbFunctionsExtensions.Last))]
    public void Should_Throw_When_Source_Is_Ordered(string methodName)
    {
        // Arrange
        EnumerableExpression source = CreateRowSource()
            .ApplyOrdering(new OrderingExpression(CreateMockArgument(typeof(double)), ascending: true));

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => _translator.Translate(GetMethod(methodName), source, [], _loggerMock.Object));

        // Assert
        Assert.Contains($"EF.Functions.{methodName} does not support Distinct or OrderBy", exception.Message);
    }

    #endregion

    #region Should_Return_Null_For_Other_Generic_Method_Of_The_Same_Type

    [Fact]
    public void Should_Return_Null_For_Other_Generic_Method_Of_The_Same_Type()
    {
        // Arrange
        MethodInfo otherMethod = typeof(TimescaleDbFunctionsExtensions)
            .GetMethod("Throw", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(double));

        // Act
        SqlExpression? result = _translator.Translate(otherMethod, CreateRowSource(), [], _loggerMock.Object);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Should_Return_Null_When_Selector_Is_Not_A_Row_Of_Two_Values

    [Fact]
    public void Should_Return_Null_When_Selector_Is_Not_A_Row_Of_Two_Values()
    {
        // Arrange
        EnumerableExpression tripleSource = CreateRowSource(valueCount: 3);
        EnumerableExpression scalarSource = new(CreateMockArgument(typeof(double)));
        MethodInfo method = GetMethod(nameof(TimescaleDbFunctionsExtensions.First));

        // Act
        SqlExpression? tripleResult = _translator.Translate(method, tripleSource, [], _loggerMock.Object);
        SqlExpression? scalarResult = _translator.Translate(method, scalarSource, [], _loggerMock.Object);

        // Assert
        Assert.Null(tripleResult);
        Assert.Null(scalarResult);
    }

    #endregion
}

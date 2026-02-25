using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;

/// <summary>
/// Translates <see cref="TimescaleDbFunctionsExtensions.TimeBucket"/> calls to <c>time_bucket</c> SQL function.
/// </summary>
internal sealed class TimescaleDbTimeBucketTranslator(ISqlExpressionFactory sqlExpressionFactory) : IMethodCallTranslator
{
    private static readonly MethodInfo[] TimeBucketMethods =
    [
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTime)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTimeOffset)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateOnly)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTime), typeof(TimeSpan)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTimeOffset), typeof(TimeSpan)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(TimeSpan), typeof(DateTimeOffset), typeof(string)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(int), typeof(int)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(int), typeof(int), typeof(int)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(long), typeof(long)])!,
        typeof(TimescaleDbFunctionsExtensions).GetMethod(nameof(TimescaleDbFunctionsExtensions.TimeBucket), [typeof(DbFunctions), typeof(long), typeof(long), typeof(long)])!,
    ];

    private static readonly bool[][] PropagateNullability =
    [
        [],
        [true],
        [true, true],
        [true, true, true],
    ];

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (!TimeBucketMethods.Contains(method))
        {
            return null;
        }

        // Skip the DbFunctions parameter — not passed to SQL
        IReadOnlyList<SqlExpression> functionArguments = arguments.Skip(1).ToList();

        return sqlExpressionFactory.Function(
            "time_bucket",
            functionArguments,
            nullable: true,
            argumentsPropagateNullability: PropagateNullability[functionArguments.Count],
            returnType: method.ReturnType,
            typeMapping: functionArguments[1].TypeMapping);
    }
}

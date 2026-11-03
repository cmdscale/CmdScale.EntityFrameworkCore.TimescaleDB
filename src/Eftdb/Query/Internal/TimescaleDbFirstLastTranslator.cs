using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;

#pragma warning disable EF1001 // Npgsql hands a ValueTuple selector to aggregate translators only as a PgRowValueExpression.

/// <summary>
/// Translates <see cref="TimescaleDbFunctionsExtensions.First{TValue, TTime}"/> and
/// <see cref="TimescaleDbFunctionsExtensions.Last{TValue, TTime}"/> calls to the <c>first</c> and <c>last</c> SQL aggregates.
/// </summary>
internal sealed class TimescaleDbFirstLastTranslator(NpgsqlSqlExpressionFactory sqlExpressionFactory) : IAggregateMethodCallTranslator
{
    private static readonly MethodInfo FirstDefinition = typeof(TimescaleDbFunctionsExtensions)
        .GetMethod(nameof(TimescaleDbFunctionsExtensions.First))!.GetGenericMethodDefinition();

    private static readonly MethodInfo LastDefinition = typeof(TimescaleDbFunctionsExtensions)
        .GetMethod(nameof(TimescaleDbFunctionsExtensions.Last))!.GetGenericMethodDefinition();

    public SqlExpression? Translate(
        MethodInfo method,
        EnumerableExpression source,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (!method.IsGenericMethod || method.DeclaringType != typeof(TimescaleDbFunctionsExtensions))
        {
            return null;
        }

        MethodInfo definition = method.GetGenericMethodDefinition();
        string? functionName = definition == FirstDefinition ? "first" : definition == LastDefinition ? "last" : null;

        if (functionName is null || source.Selector is not PgRowValueExpression { Values.Count: 2 } row)
        {
            return null;
        }

        // Returning null here would make EF fall back to a collection subquery and fail with an unrelated error.
        if (source.IsDistinct || source.Orderings.Count > 0)
        {
            throw new InvalidOperationException(
                $"EF.Functions.{method.Name} does not support Distinct or OrderBy inside the grouping; the time argument already orders the rows.");
        }

        SqlExpression value = sqlExpressionFactory.ApplyDefaultTypeMapping(row.Values[0]);
        SqlExpression time = sqlExpressionFactory.ApplyDefaultTypeMapping(row.Values[1]);

        return sqlExpressionFactory.AggregateFunction(
            functionName,
            [value, time],
            source,
            nullable: true,
            argumentsPropagateNullability: [false, false],
            returnType: method.ReturnType,
            typeMapping: value.TypeMapping);
    }
}

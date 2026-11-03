using Microsoft.EntityFrameworkCore.Query;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;

/// <summary>
/// Registers TimescaleDB aggregate method call translators with the EF Core query pipeline.
/// </summary>
internal sealed class TimescaleDbAggregateMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory) : IAggregateMethodCallTranslatorPlugin
{
    public IEnumerable<IAggregateMethodCallTranslator> Translators { get; } =
    [
        new TimescaleDbFirstLastTranslator((NpgsqlSqlExpressionFactory)sqlExpressionFactory),
    ];
}

using Microsoft.EntityFrameworkCore.Query;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query.Internal;

/// <summary>
/// Registers TimescaleDB method call translators with the EF Core query pipeline.
/// </summary>
internal sealed class TimescaleDbMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory) : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } =
    [
        new TimescaleDbTimeBucketTranslator(sqlExpressionFactory),
    ];
}

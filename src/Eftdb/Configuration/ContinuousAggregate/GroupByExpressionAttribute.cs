namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Defines a property of a continuous aggregate entity as a GROUP BY expression selected as a column:
    /// <c>[GroupByExpression("{0} IS NOT NULL", nameof(Event.ImpersonatedActorId))]</c>.
    /// </summary>
    /// <param name="sql">
    /// The grouping expression template. Column references are <c>{0}</c>, <c>{1}</c>, … placeholders
    /// bound to <paramref name="columns"/>; literal braces are written as <c>{{</c> and <c>}}</c>.
    /// </param>
    /// <param name="columns">Source property or column names bound to the placeholders, in order.</param>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GroupByExpressionAttribute(string sql, params string[] columns) : Attribute
    {
        /// <summary>The grouping expression template.</summary>
        public string Sql { get; } = sql;

        /// <summary>The source columns bound to the placeholders.</summary>
        public string[] Columns { get; } = columns;
    }
}

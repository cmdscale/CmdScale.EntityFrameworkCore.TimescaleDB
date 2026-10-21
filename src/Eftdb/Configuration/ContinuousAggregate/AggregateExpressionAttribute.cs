namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Defines a property of a continuous aggregate entity as a SQL aggregate expression, such as a
    /// toolkit sketch: <c>[AggregateExpression("mcv_agg(500, {0})", nameof(Event.ActorId))]</c>.
    /// </summary>
    /// <param name="sql">
    /// The aggregate expression template. Column references are <c>{0}</c>, <c>{1}</c>, … placeholders
    /// bound to <paramref name="columns"/>; literal braces are written as <c>{{</c> and <c>}}</c>.
    /// </param>
    /// <param name="columns">Source property or column names bound to the placeholders, in order.</param>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AggregateExpressionAttribute(string sql, params string[] columns) : Attribute
    {
        /// <summary>The aggregate expression template.</summary>
        public string Sql { get; } = sql;

        /// <summary>The source columns bound to the placeholders.</summary>
        public string[] Columns { get; } = columns;
    }
}

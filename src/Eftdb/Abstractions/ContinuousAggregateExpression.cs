namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    /// <summary>
    /// Maps a column on a continuous aggregate to a SQL expression over source columns — e.g.
    /// <c>actors = mcv_agg(500, actor_id)</c> or <c>impersonated = impersonated_actor_id IS NOT NULL</c>.
    /// Column references in <see cref="Sql"/> are written as <c>{0}</c>, <c>{1}</c>, … placeholders,
    /// each bound to the entry at that index in <see cref="Columns"/>; literal braces are written as
    /// <c>{{</c> and <c>}}</c>.
    /// </summary>
    public sealed class ContinuousAggregateExpression(string alias, string sql, IReadOnlyList<string> columns)
        : IEquatable<ContinuousAggregateExpression>
    {
        /// <summary>The name of the resulting column on the continuous aggregate.</summary>
        public string Alias { get; } = alias;

        /// <summary>The SQL expression template, with <c>{n}</c> placeholders for column references.</summary>
        public string Sql { get; } = sql;

        /// <summary>
        /// The source columns bound to the placeholders. In model annotations these are property names,
        /// complex-type paths or column names; on migration operations they are database column names.
        /// </summary>
        public IReadOnlyList<string> Columns { get; } = columns;

        public bool Equals(ContinuousAggregateExpression? other)
            => other is not null && Alias == other.Alias && Sql == other.Sql && Columns.SequenceEqual(other.Columns);

        public override bool Equals(object? obj) => Equals(obj as ContinuousAggregateExpression);

        public override int GetHashCode() => HashCode.Combine(Alias, Sql, Columns.Count);
    }
}

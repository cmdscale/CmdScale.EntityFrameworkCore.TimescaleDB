namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features
{
    /// <summary>
    /// Carries cross-cutting information that individual <see cref="IFeatureDiffer"/> implementations
    /// need but cannot derive on their own: renames detected by EF Core's base differ, and parent
    /// objects that another feature differ has decided to drop and recreate.
    /// </summary>
    /// <remarks>
    /// Schemas are always stored normalized to a concrete value (never null); callers must normalize
    /// missing schemas to <see cref="DefaultValues.DefaultSchema"/> before building or querying the maps,
    /// matching how the model extractors normalize <c>GetSchema()</c>.
    /// </remarks>
    public sealed class FeatureDiffContext
    {
        /// <summary>Maps a source object's <c>(schema, oldTableName)</c> to its <c>(schema, newTableName)</c>.</summary>
        public IReadOnlyDictionary<(string Schema, string Name), (string Schema, string Name)> TableRenames { get; init; }
            = new Dictionary<(string, string), (string, string)>();

        /// <summary>Maps an index's <c>(schema, oldIndexName)</c> to its <c>(schema, newIndexName)</c>.</summary>
        public IReadOnlyDictionary<(string Schema, string Name), (string Schema, string Name)> IndexRenames { get; init; }
            = new Dictionary<(string, string), (string, string)>();

        /// <summary>
        /// Maps a column's <c>(schema, newTableName, oldColumnName)</c> to its new column name. The table key uses
        /// the post-rename table name because EF Core emits <c>RenameColumnOperation</c> against the renamed table.
        /// </summary>
        public IReadOnlyDictionary<(string Schema, string Table, string Column), string> ColumnRenames { get; init; }
            = new Dictionary<(string, string, string), string>();

        /// <summary>
        /// Continuous aggregates (by <c>(schema, viewName)</c>) that are being dropped and recreated in this diff.
        /// Recreating a continuous aggregate cascades to drop its refresh and retention policies, so dependent
        /// policy differs must re-add those policies even when their configuration is unchanged. Populated by the
        /// orchestrator after the continuous aggregate differ runs.
        /// </summary>
        public ISet<(string Schema, string ViewName)> RecreatedAggregates { get; init; }
            = new HashSet<(string, string)>();

        /// <summary>An empty context with identity rename maps; used when a differ is invoked without orchestration.</summary>
        public static FeatureDiffContext Empty { get; } = new();

        public (string Schema, string Name) ResolveTable(string schema, string name)
            => TableRenames.TryGetValue((schema, name), out (string Schema, string Name) mapped) ? mapped : (schema, name);

        public (string Schema, string Name) ResolveIndex(string schema, string name)
            => IndexRenames.TryGetValue((schema, name), out (string Schema, string Name) mapped) ? mapped : (schema, name);

        public string ResolveColumn(string schema, string table, string column)
            => ColumnRenames.TryGetValue((schema, table, column), out string? mapped) ? mapped : column;
    }
}

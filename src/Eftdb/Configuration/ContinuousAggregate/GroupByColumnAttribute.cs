namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate
{
    /// <summary>
    /// Marks a property of a continuous aggregate entity as an additional GROUP BY column.
    /// </summary>
    /// <remarks>
    /// Complements the Fluent API's <c>AddGroupByColumn</c> for data-annotations configuration.
    /// Raw SQL GROUP BY expressions cannot be expressed as an attribute; use the Fluent API for those.
    /// </remarks>
    /// <param name="sourceColumn">
    /// The source column on the parent hypertable to group by. Defaults to the annotated property's
    /// own name. Accepts a CLR property name or a database column name.
    /// </param>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GroupByColumnAttribute(string? sourceColumn = null) : Attribute
    {
        /// <summary>
        /// The source column to group by, or <c>null</c> to use the annotated property's name.
        /// </summary>
        public string? SourceColumn { get; } = sourceColumn;
    }
}

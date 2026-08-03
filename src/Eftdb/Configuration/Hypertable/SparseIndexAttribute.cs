using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// Configures a single sparse index entry on a hypertable's columnstore.
    /// Apply multiple times to declare several entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="ESparseIndexType.Bloom"/> for one or more columns and
    /// <see cref="ESparseIndexType.MinMax"/> for a single column. Specifying more than one column
    /// with <see cref="ESparseIndexType.MinMax"/> is detected at model finalization and raises an error,
    /// because the type system cannot enforce this at compile time through attributes.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class SparseIndexAttribute : Attribute
    {
        /// <summary>The sparse index function to apply.</summary>
        public ESparseIndexType Kind { get; }

        /// <summary>The column or property names this entry targets.</summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// Initializes a sparse index attribute.
        /// </summary>
        /// <param name="kind">The sparse index function.</param>
        /// <param name="columns">One or more column or property names. Use <c>nameof()</c> for refactoring safety.</param>
        /// <exception cref="ArgumentException">Thrown when no columns are supplied.</exception>
        public SparseIndexAttribute(ESparseIndexType kind, params string[] columns)
        {
            if (columns == null || columns.Length == 0)
            {
                throw new ArgumentException("At least one column must be specified.", nameof(columns));
            }

            Kind = kind;
            Columns = columns;
        }
    }
}

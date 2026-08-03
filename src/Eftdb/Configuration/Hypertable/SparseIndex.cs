using System.Linq.Expressions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable
{
    /// <summary>
    /// Represents a single sparse index entry
    /// </summary>
    public sealed class SparseIndex
    {
        /// <summary>The sparse index function to apply.</summary>
        public ESparseIndexType Kind { get; }

        /// <summary>The column (or property) names this entry targets.</summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// Initializes a sparse index entry.
        /// </summary>
        /// <param name="kind">The sparse index function.</param>
        /// <param name="columns">One or more column or property names.</param>
        /// <exception cref="ArgumentException">Thrown when no columns are supplied.</exception>
        public SparseIndex(ESparseIndexType kind, IReadOnlyList<string> columns)
        {
            if (columns == null || columns.Count == 0)
            {
                throw new ArgumentException("At least one column must be specified.", nameof(columns));
            }

            Kind = kind;
            Columns = columns;
        }

        /// <summary>
        /// Serializes this entry to its canonical SQL form: <c>bloom(col1,col2)</c> or <c>minmax(col)</c>.
        /// </summary>
        public string ToSql()
        {
            string func = Kind switch
            {
                ESparseIndexType.Bloom => "bloom",
                ESparseIndexType.MinMax => "minmax",
                _ => throw new InvalidOperationException($"Unknown ESparseIndexType: {Kind}"),
            };

            return $"{func}({string.Join(",", Columns)})";
        }
    }

    /// <summary>
    /// Fluent builder for creating <see cref="SparseIndex"/> instances using type-safe lambda expressions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    public sealed class SparseIndexSelector<TEntity>
    {
        /// <summary>
        /// Creates a bloom-filter sparse index entry for one or more properties.
        /// At least one expression must be supplied; composite bloom entries are fully supported.
        /// </summary>
        /// <param name="expressions">One or more property selectors.</param>
        public SparseIndex Bloom(params Expression<Func<TEntity, object>>[] expressions)
        {
            if (expressions == null || expressions.Length == 0)
            {
                throw new ArgumentException("At least one property expression must be supplied.", nameof(expressions));
            }

            List<string> columns = [.. expressions.Select(ExpressionHelper.GetPropertyName)];
            return new SparseIndex(ESparseIndexType.Bloom, columns);
        }

        /// <summary>
        /// Creates a min/max sparse index entry for a single property.
        /// Min/max is a single-column index type; use <see cref="Bloom"/> for composite entries.
        /// </summary>
        /// <param name="expression">The property selector.</param>
        public SparseIndex MinMax(Expression<Func<TEntity, object>> expression)
            => new(ESparseIndexType.MinMax, [ExpressionHelper.GetPropertyName(expression)]);
    }
}

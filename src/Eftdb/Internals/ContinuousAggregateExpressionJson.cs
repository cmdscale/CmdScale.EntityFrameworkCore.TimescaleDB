using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Reads and writes the JSON form of <see cref="ContinuousAggregateExpression"/> lists stored in the
    /// <c>AggregateExpressions</c> and <c>GroupByExpressions</c> annotations.
    /// </summary>
    internal static class ContinuousAggregateExpressionJson
    {
        public static List<ContinuousAggregateExpression> Read(IReadOnlyAnnotatable annotatable, string annotation)
            => annotatable.FindAnnotation(annotation)?.Value is string { Length: > 0 } json
                ? JsonSerializer.Deserialize<List<ContinuousAggregateExpression>>(json)!
                : [];

        public static string Write(IReadOnlyList<ContinuousAggregateExpression> expressions)
            => JsonSerializer.Serialize(expressions);

        /// <summary>
        /// Checks that every placeholder in <paramref name="sql"/> has a column and every column is
        /// referenced.
        /// </summary>
        /// <exception cref="ArgumentException">The template and columns do not match.</exception>
        public static void Validate(string alias, string sql, IReadOnlyList<string> columns)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("The expression alias must not be empty.", nameof(alias));
            }

            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new ArgumentException($"The SQL expression for '{alias}' must not be empty.", nameof(sql));
            }

            List<int> indices;
            try
            {
                indices = ColumnTemplate.GetPlaceholderIndices(sql);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(ex.Message, nameof(sql), ex);
            }

            int missing = indices.FirstOrDefault(i => i >= columns.Count, -1);
            if (missing >= 0)
            {
                throw new ArgumentException($"The SQL expression for '{alias}' references {{{missing}}}, but only {columns.Count} column(s) were given.", nameof(sql));
            }

            int unused = Enumerable.Range(0, columns.Count).FirstOrDefault(i => !indices.Contains(i), -1);
            if (unused >= 0)
            {
                throw new ArgumentException($"The SQL expression for '{alias}' never references column {{{unused}}} ('{columns[unused]}').", nameof(columns));
            }
        }
    }
}

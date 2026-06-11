using System.Text.RegularExpressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Determines whether a resolved PostgreSQL store type is valid as a TimescaleDB time/partition
    /// dimension.
    /// </summary>
    /// <remarks>
    /// Validation is performed against the resolved store type rather than the .NET type, because the
    /// .NET type is not authoritative: custom mappings (for example the Npgsql NodaTime plugin mapping
    /// <c>Instant</c> to <c>timestamptz</c>, or a value-converted type) are valid precisely because of
    /// the store-type mapping they register.
    /// </remarks>
    internal static partial class TimeColumnStoreTypeValidator
    {
        // PostgreSQL store types accepted by TimescaleDB as a time dimension
        private static readonly HashSet<string> AllowedStoreTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "timestamp without time zone",
            "timestamp with time zone",
            "timestamp",
            "timestamptz",
            "date",
            "smallint",
            "int2",
            "integer",
            "int",
            "int4",
            "bigint",
            "int8",
        };

        /// <summary>
        /// Returns <c>true</c> if <paramref name="storeType"/> is a PostgreSQL type valid as a TimescaleDB time dimension.
        /// </summary>
        public static bool IsValid(string? storeType)
        {
            string? normalized = Normalize(storeType);
            return normalized != null && AllowedStoreTypes.Contains(normalized);
        }

        // Strips a length/precision qualifier (e.g. "timestamp(6) with time zone" -> "timestamp with
        // time zone") and collapses whitespace so the comparison is robust to formatting.
        private static string? Normalize(string? storeType)
        {
            if (string.IsNullOrWhiteSpace(storeType))
            {
                return null;
            }

            string value = storeType.Trim();

            int open = value.IndexOf('(');
            if (open >= 0)
            {
                int close = value.IndexOf(')', open);
                if (close > open)
                {
                    value = value[..open] + value[(close + 1)..];
                }
            }

            return WhitespaceRegex().Replace(value, " ").Trim();
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}

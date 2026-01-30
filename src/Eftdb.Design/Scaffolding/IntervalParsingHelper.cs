using System.Text.Json;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Provides helper methods for parsing and normalizing TimescaleDB interval values.
    /// </summary>
    public static class IntervalParsingHelper
    {
        /// <summary>
        /// Parses an interval or integer value from a JSON element.
        /// </summary>
        /// <param name="element">The JSON element to parse.</param>
        /// <returns>
        /// A normalized interval string for string-based intervals,
        /// or a string representation of the integer for integer-based time columns,
        /// or null if the element is null or cannot be parsed.
        /// </returns>
        /// <remarks>
        /// TimescaleDB stores intervals as strings (e.g., "1 mon", "7 days")
        /// or integers for integer-based time columns.
        /// </remarks>
        public static string? ParseIntervalOrInteger(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                string value = element.GetString() ?? string.Empty;
                return NormalizeInterval(value);
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt64().ToString();
            }

            return null;
        }

        /// <summary>
        /// Normalizes PostgreSQL interval format to a user-friendly format.
        /// </summary>
        /// <param name="pgInterval">The PostgreSQL interval string to normalize.</param>
        /// <returns>A normalized interval string.</returns>
        /// <remarks>
        /// PostgreSQL stores intervals in formats like:
        /// - "1 mon" for 1 month
        /// - "7 days" for 7 days
        /// - "01:00:00" for 1 hour
        /// This method normalizes these to match the format users would use in Fluent API:
        /// - "1 month"
        /// - "7 days"
        /// - "1 hour"
        /// </remarks>
        public static string NormalizeInterval(string pgInterval)
        {
            if (string.IsNullOrWhiteSpace(pgInterval))
            {
                return pgInterval;
            }

            string normalized = pgInterval.Trim();

            normalized = normalized.Replace(" mon", " month");

            if (TimeSpan.TryParse(normalized, out TimeSpan timeSpan))
            {
                if (timeSpan.TotalMinutes < 60 && timeSpan.Minutes > 0 && timeSpan.Hours == 0)
                {
                    return $"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")}";
                }
                if (timeSpan.TotalHours < 24 && timeSpan.Hours > 0)
                {
                    return $"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : "")}";
                }
                if (timeSpan.Days > 0)
                {
                    return $"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : "")}";
                }
            }

            return normalized;
        }
    }
}

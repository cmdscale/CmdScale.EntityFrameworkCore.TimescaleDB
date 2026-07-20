using System.Text.Json;
using System.Text.RegularExpressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Provides helper methods for parsing and normalizing TimescaleDB interval values.
    /// </summary>
    public static partial class IntervalParsingHelper
    {
        private const long MicrosecondsPerSecond = 1_000_000L;

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
        public static string NormalizeInterval(string pgInterval)
        {
            if (string.IsNullOrWhiteSpace(pgInterval))
            {
                return pgInterval;
            }

            string normalized = pgInterval.Trim();
            normalized = MonthsRegex().Replace(normalized, "months");
            normalized = MonthRegex().Replace(normalized, "month");

            // PostgreSQL renders sub-day intervals as HH:MM:SS and interval arithmetic
            // (`INTERVAL '1 microsecond' * N`) as HHH:MM:SS where HHH can exceed 23.
            // The optional D. prefix covers TimeSpan-style day components ("1.00:00:00").
            if (TryParseTimeParts(normalized, out long days, out long hours, out long minutes, out long seconds, out long fractionMicroseconds))
            {
                if (fractionMicroseconds > 0)
                {
                    return normalized;
                }

                long totalSeconds = ((((days * 24) + hours) * 60 + minutes) * 60) + seconds;
                if (totalSeconds == 0)
                {
                    return normalized;
                }

                return totalSeconds switch
                {
                    _ when totalSeconds % 86_400 == 0 => Pluralize(totalSeconds / 86_400, "day"),
                    _ when totalSeconds % 3_600 == 0 => Pluralize(totalSeconds / 3_600, "hour"),
                    _ when totalSeconds % 60 == 0 => Pluralize(totalSeconds / 60, "minute"),
                    _ => Pluralize(totalSeconds, "second"),
                };
            }

            return normalized;
        }

        /// <summary>
        /// Converts a fixed-duration interval to its total length in microseconds.
        /// </summary>
        /// <param name="interval">The interval string ("30 minutes", "1 day", "168:00:00").</param>
        /// <param name="microseconds">The total number of microseconds when parsing succeeds.</param>
        /// <returns>
        /// <c>false</c> for calendar units (month, year), composite forms ("2 days 03:00:00"), bare
        /// integers, and anything else without a fixed microsecond duration.
        /// </returns>
        public static bool TryGetTotalMicroseconds(string interval, out long microseconds)
        {
            microseconds = 0;
            if (string.IsNullOrWhiteSpace(interval))
            {
                return false;
            }

            string value = interval.Trim();

            if (value.Contains(':'))
            {
                if (!TryParseTimeParts(value, out long days, out long hours, out long minutes, out long seconds, out long fractionMicroseconds))
                {
                    return false;
                }

                microseconds = (((((days * 24) + hours) * 60) + minutes) * 60 + seconds) * MicrosecondsPerSecond + fractionMicroseconds;
                return true;
            }

            Match match = NumberUnitRegex().Match(value);
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out long amount))
            {
                return false;
            }

            long? unitMicroseconds = match.Groups[2].Value.ToLowerInvariant() switch
            {
                "us" or "usec" or "usecs" or "microsecond" or "microseconds" => 1L,
                "ms" or "msec" or "msecs" or "millisecond" or "milliseconds" => 1_000L,
                "s" or "sec" or "secs" or "second" or "seconds" => MicrosecondsPerSecond,
                "min" or "mins" or "minute" or "minutes" => 60L * MicrosecondsPerSecond,
                "h" or "hr" or "hrs" or "hour" or "hours" => 3_600L * MicrosecondsPerSecond,
                "d" or "day" or "days" => 86_400L * MicrosecondsPerSecond,
                "w" or "week" or "weeks" => 604_800L * MicrosecondsPerSecond,
                _ => null, // Calendar units (month, year) have no fixed duration.
            };

            if (unitMicroseconds is null)
            {
                return false;
            }

            microseconds = amount * unitMicroseconds.Value;
            return true;
        }

        /// <summary>
        /// Parses a <c>[D.]H+:MM:SS[.ffffff]</c> value into its components. The hour component may
        /// exceed 23 when no day prefix is present. Returns <c>false</c> for anything else, including
        /// composite forms with leading unit words.
        /// </summary>
        private static bool TryParseTimeParts(string value, out long days, out long hours, out long minutes, out long seconds, out long fractionMicroseconds)
        {
            days = hours = minutes = seconds = fractionMicroseconds = 0;

            string[] parts = value.Split(':');
            if (parts.Length != 3)
            {
                return false;
            }

            string hourPart = parts[0];
            int dayDot = hourPart.IndexOf('.');
            if (dayDot >= 0)
            {
                if (!long.TryParse(hourPart[..dayDot], out days) || days < 0)
                {
                    return false;
                }

                hourPart = hourPart[(dayDot + 1)..];
            }

            if (!long.TryParse(hourPart, out hours) || hours < 0 || (dayDot >= 0 && hours > 23))
            {
                return false;
            }

            if (!long.TryParse(parts[1], out minutes) || minutes is < 0 or >= 60)
            {
                return false;
            }

            string secondsPart = parts[2];
            int secondsDot = secondsPart.IndexOf('.');
            if (secondsDot >= 0)
            {
                string fraction = secondsPart[(secondsDot + 1)..];
                if (fraction.Length == 0 || !fraction.All(char.IsAsciiDigit))
                {
                    return false;
                }

                string padded = fraction.Length >= 6 ? fraction[..6] : fraction.PadRight(6, '0');
                fractionMicroseconds = long.Parse(padded);
                secondsPart = secondsPart[..secondsDot];
            }

            return long.TryParse(secondsPart, out seconds) && seconds is >= 0 and < 60;
        }

        private static string Pluralize(long amount, string unit)
            => amount == 1 ? $"1 {unit}" : $"{amount} {unit}s";

        [GeneratedRegex(@"\bmons\b")]
        private static partial Regex MonthsRegex();

        [GeneratedRegex(@"\bmon\b")]
        private static partial Regex MonthRegex();

        [GeneratedRegex(@"^(\d+)\s+([a-zA-Z]+)$")]
        private static partial Regex NumberUnitRegex();
    }
}

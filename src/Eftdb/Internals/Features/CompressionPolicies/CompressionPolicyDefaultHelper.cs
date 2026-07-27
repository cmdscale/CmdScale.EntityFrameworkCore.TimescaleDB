namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies
{
    /// <summary>
    /// Computes the TimescaleDB default <c>schedule_interval</c> for <c>add_compression_policy()</c>
    /// based on the hypertable's chunk time interval.
    /// </summary>
    /// <remarks>
    /// TimescaleDB applies one of two rules:
    /// <list type="bullet">
    ///   <item>chunk_time_interval ≥ 1 day → default is <c>"12 hours"</c></item>
    ///   <item>chunk_time_interval &lt; 1 day → default is <c>chunk_time_interval / 2</c></item>
    /// </list>
    /// Integer-time hypertables (bigint chunk intervals stored as raw integer strings) cannot be
    /// halved meaningfully here, so they fall back to null (treat the value as explicitly configured).
    /// </remarks>
    internal static class CompressionPolicyDefaultHelper
    {
        private const long MicrosecondsPerSecond = 1_000_000L;
        private const long MicrosecondsPerMinute = 60L * MicrosecondsPerSecond;
        private const long MicrosecondsPerHour = 3_600L * MicrosecondsPerSecond;
        private const long MicrosecondsPerDay = 86_400L * MicrosecondsPerSecond;

        /// <summary>
        /// Returns the expected default <c>schedule_interval</c> for <c>add_compression_policy()</c>
        /// for a hypertable with the given chunk time interval, or <see langword="null"/> when the
        /// chunk interval cannot be parsed (e.g., integer-time hypertable or composite form).
        /// </summary>
        internal static string? ComputeDefaultScheduleInterval(string? chunkTimeInterval)
        {
            if (string.IsNullOrWhiteSpace(chunkTimeInterval))
            {
                return DefaultValues.CompressionPolicyScheduleInterval;
            }

            if (!TryGetTotalMicroseconds(chunkTimeInterval, out long chunkMicroseconds))
            {
                return null;
            }

            if (chunkMicroseconds >= MicrosecondsPerDay)
            {
                return DefaultValues.CompressionPolicyScheduleInterval;
            }

            long halfMicroseconds = chunkMicroseconds / 2;
            return MicrosecondsToInterval(halfMicroseconds);
        }

        /// <summary>
        /// Converts a total microsecond count to a canonical interval string.
        /// Only handles fixed sub-day durations (hours, minutes, seconds).
        /// Returns <see langword="null"/> for zero or non-representable values.
        /// </summary>
        private static string? MicrosecondsToInterval(long microseconds)
        {
            if (microseconds <= 0)
            {
                return null;
            }

            if (microseconds % MicrosecondsPerHour == 0)
            {
                long hours = microseconds / MicrosecondsPerHour;
                return hours == 1 ? "1 hour" : $"{hours} hours";
            }

            if (microseconds % MicrosecondsPerMinute == 0)
            {
                long minutes = microseconds / MicrosecondsPerMinute;
                return minutes == 1 ? "1 minute" : $"{minutes} minutes";
            }

            if (microseconds % MicrosecondsPerSecond == 0)
            {
                long seconds = microseconds / MicrosecondsPerSecond;
                return seconds == 1 ? "1 second" : $"{seconds} seconds";
            }

            return null;
        }

        /// <summary>
        /// Converts a fixed-duration interval string to its total length in microseconds.
        /// Returns <see langword="false"/> for calendar units (month, year), composite forms
        /// ("2 days 03:00:00"), bare integers, and anything else without a fixed duration.
        /// </summary>
        private static bool TryGetTotalMicroseconds(string interval, out long microseconds)
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

            int spaceIndex = value.IndexOf(' ');
            if (spaceIndex <= 0)
            {
                return false;
            }

            string numberPart = value[..spaceIndex];
            string unitPart = value[(spaceIndex + 1)..];

            if (!long.TryParse(numberPart, out long amount))
            {
                return false;
            }

            long? unitMicroseconds = unitPart.ToLowerInvariant() switch
            {
                "us" or "usec" or "usecs" or "microsecond" or "microseconds" => 1L,
                "ms" or "msec" or "msecs" or "millisecond" or "milliseconds" => 1_000L,
                "s" or "sec" or "secs" or "second" or "seconds" => MicrosecondsPerSecond,
                "min" or "mins" or "minute" or "minutes" => MicrosecondsPerMinute,
                "h" or "hr" or "hrs" or "hour" or "hours" => MicrosecondsPerHour,
                "d" or "day" or "days" => MicrosecondsPerDay,
                "w" or "week" or "weeks" => 7L * MicrosecondsPerDay,
                _ => null,
            };

            if (unitMicroseconds is null)
            {
                return false;
            }

            microseconds = amount * unitMicroseconds.Value;
            return true;
        }

        /// <summary>
        /// Parses a <c>[D.]H+:MM:SS[.ffffff]</c> value into its components.
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
    }
}

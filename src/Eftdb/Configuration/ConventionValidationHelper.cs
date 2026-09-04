using System.Globalization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration
{
    /// <summary>
    /// Shared validation helpers for entity-type conventions, covering common patterns
    /// </summary>
    internal static class ConventionValidationHelper
    {
        /// <summary>
        /// Validates that exactly one of two mutually-exclusive fields is set, throwing when
        /// both are set or neither is set.
        /// </summary>
        /// <param name="entityName">The CLR type name of the entity, for use in exception messages.</param>
        /// <param name="attributeName">The attribute name shown in the exception message prefix (e.g., <c>"[CompressionPolicy]"</c>).</param>
        /// <param name="firstFieldName">The name of the first field (e.g., <c>"After"</c>).</param>
        /// <param name="hasFirst">Whether the first field is set.</param>
        /// <param name="secondFieldName">The name of the second field (e.g., <c>"CreatedBefore"</c>).</param>
        /// <param name="hasSecond">Whether the second field is set.</param>
        internal static void ValidateExclusiveFields(
            string? entityName,
            string attributeName,
            string firstFieldName,
            bool hasFirst,
            string secondFieldName,
            bool hasSecond)
            => ValidateExclusiveFields($"{attributeName} on '{entityName}'", firstFieldName, hasFirst, secondFieldName, hasSecond);

        /// <summary>
        /// Validates that exactly one of two mutually-exclusive fields is set, throwing when
        /// both are set or neither is set.
        /// </summary>
        /// <param name="contextPrefix">The exception message prefix identifying the configuration source (e.g., <c>"WithRetentionPolicy"</c>).</param>
        /// <param name="firstFieldName">The name of the first field (e.g., <c>"dropAfter"</c>).</param>
        /// <param name="hasFirst">Whether the first field is set.</param>
        /// <param name="secondFieldName">The name of the second field (e.g., <c>"dropCreatedBefore"</c>).</param>
        /// <param name="hasSecond">Whether the second field is set.</param>
        internal static void ValidateExclusiveFields(
            string contextPrefix,
            string firstFieldName,
            bool hasFirst,
            string secondFieldName,
            bool hasSecond)
        {
            if (hasFirst && hasSecond)
            {
                throw new InvalidOperationException(
                    $"{contextPrefix}: '{firstFieldName}' and '{secondFieldName}' are mutually exclusive. Specify exactly one.");
            }

            if (!hasFirst && !hasSecond)
            {
                throw new InvalidOperationException(
                    $"{contextPrefix}: Exactly one of '{firstFieldName}' or '{secondFieldName}' must be specified.");
            }
        }

        /// <summary>
        /// Parses a policy <c>InitialStart</c> <see cref="DateTime"/> from an attribute string value,
        /// throwing when the value is present but cannot be parsed. The result is always
        /// <see cref="DateTimeKind.Utc"/>.
        /// </summary>
        /// <remarks>
        /// Parsing uses <see cref="CultureInfo.InvariantCulture"/> with
        /// <see cref="DateTimeStyles.AssumeUniversal"/> | <see cref="DateTimeStyles.AdjustToUniversal"/>
        /// so the produced instant does not depend on the machine's local time zone. Strings carrying an
        /// explicit designator ("Z" or an offset) convert to UTC correctly; strings without a designator
        /// are interpreted as already being UTC. Treating unsuffixed values as UTC is the only
        /// machine-independent interpretation: the alternative (local time) would render a different
        /// literal into migrations on every machine and produce phantom alter-policy operations.
        /// </remarks>
        /// <param name="rawValue">The raw attribute string to parse.</param>
        /// <param name="entityName">The CLR type name of the entity, for use in exception messages.</param>
        /// <param name="attributeName">The attribute name shown in the exception message prefix.</param>
        /// <returns>The parsed UTC <see cref="DateTime"/>, or <see langword="null"/> when <paramref name="rawValue"/> is null or whitespace.</returns>
        internal static DateTime? ParseInitialStart(string? rawValue, string? entityName, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            if (DateTime.TryParse(
                    rawValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException(
                $"{attributeName} on '{entityName}': InitialStart '{rawValue}' is not a valid DateTime format. Use an ISO 8601 string.");
        }

        /// <summary>
        /// Normalizes a policy <c>InitialStart</c> value to a machine-independent UTC instant so that
        /// values written by the fluent API, parsed from attributes, and read back from snapshots all
        /// compare on the same footing.
        /// </summary>
        /// <remarks>
        /// Kind handling:
        /// <list type="bullet">
        /// <item><see cref="DateTimeKind.Utc"/>: returned unchanged.</item>
        /// <item><see cref="DateTimeKind.Local"/>: converted via <see cref="DateTime.ToUniversalTime"/>.</item>
        /// <item><see cref="DateTimeKind.Unspecified"/>: reinterpreted as UTC via
        /// <see cref="DateTime.SpecifyKind"/> (NOT <see cref="DateTime.ToUniversalTime"/>, which would
        /// treat it as local and reintroduce a machine dependency). This matches the attribute path's
        /// "unsuffixed = UTC" rule.</item>
        /// </list>
        /// </remarks>
        /// <param name="value">The value to normalize.</param>
        /// <returns>The equivalent UTC <see cref="DateTime"/>.</returns>
        internal static DateTime NormalizeInitialStartToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        /// <summary>
        /// Nullable overload of <see cref="NormalizeInitialStartToUtc(DateTime)"/>; returns
        /// <see langword="null"/> unchanged.
        /// </summary>
        /// <param name="value">The value to normalize, or <see langword="null"/>.</param>
        /// <returns>The equivalent UTC <see cref="DateTime"/>, or <see langword="null"/>.</returns>
        internal static DateTime? NormalizeInitialStartToUtc(DateTime? value)
            => value.HasValue ? NormalizeInitialStartToUtc(value.Value) : null;
    }
}

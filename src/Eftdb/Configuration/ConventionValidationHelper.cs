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
        /// Parses a <see cref="DateTime"/> from an attribute string value, throwing when
        /// the value is present but cannot be parsed.
        /// </summary>
        /// <param name="rawValue">The raw attribute string to parse.</param>
        /// <param name="entityName">The CLR type name of the entity, for use in exception messages.</param>
        /// <param name="attributeName">The attribute name shown in the exception message prefix.</param>
        /// <returns>The parsed <see cref="DateTime"/>, or <see langword="null"/> when <paramref name="rawValue"/> is null or whitespace.</returns>
        internal static DateTime? ParseInitialStart(string? rawValue, string? entityName, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            if (DateTime.TryParse(rawValue, out DateTime parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException(
                $"{attributeName} on '{entityName}': InitialStart '{rawValue}' is not a valid DateTime format. Use an ISO 8601 string.");
        }
    }
}

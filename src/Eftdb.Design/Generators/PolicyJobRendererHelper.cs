using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRendererHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Shared rendering helpers for policy job parameters that appear across multiple TimescaleDB
    /// policy types (e.g. refresh, reorder, retention).
    /// </summary>
    internal static class PolicyJobRendererHelper
    {
        /// <summary>
        /// Chains a <c>WithInitialStart(DateTime)</c>-style call when the annotation is present.
        /// The annotation value must be a <see cref="DateTime"/>; other types are silently ignored.
        /// </summary>
        /// <param name="call">The current method-call fragment to chain onto.</param>
        /// <param name="annotations">The entity's live annotation dictionary.</param>
        /// <param name="initialStartKey">The annotation key storing the <see cref="DateTime"/> value.</param>
        /// <param name="withInitialStartMethod">
        ///     Reflection handle for the <c>WithInitialStart</c> method on the policy builder.
        /// </param>
        /// <returns>The fragment, extended with the chain when the annotation is present.</returns>
        public static MethodCallCodeFragment ChainInitialStart(
            MethodCallCodeFragment call,
            IDictionary<string, IAnnotation> annotations,
            string initialStartKey,
            MethodInfo withInitialStartMethod)
        {
            IAnnotation? annotation = Find(annotations, initialStartKey);
            if (annotation?.Value is not DateTime initialStart)
            {
                return call;
            }

            return call.Chain(withInitialStartMethod, initialStart);
        }

        /// <summary>
        /// Chains a <c>WithScheduleInterval(string)</c>-style call when the annotation is present
        /// and the policy builder exposes schedule interval as a separate chain method. Callers that
        /// pass schedule interval as an argument to the policy entry call should use the annotation
        /// value directly and skip this helper.
        /// </summary>
        /// <param name="call">The current method-call fragment to chain onto.</param>
        /// <param name="annotations">The entity's live annotation dictionary.</param>
        /// <param name="scheduleIntervalKey">The annotation key storing the interval string.</param>
        /// <param name="withScheduleIntervalMethod">
        ///     Reflection handle for the <c>WithScheduleInterval</c> (or equivalent) method on the
        ///     policy builder.
        /// </param>
        /// <returns>The fragment, extended with the chain when the annotation is present.</returns>
        public static MethodCallCodeFragment ChainScheduleInterval(
            MethodCallCodeFragment call,
            IDictionary<string, IAnnotation> annotations,
            string scheduleIntervalKey,
            MethodInfo withScheduleIntervalMethod)
        {
            string? value = GetString(annotations, scheduleIntervalKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                return call;
            }

            return call.Chain(withScheduleIntervalMethod, value);
        }

        /// <summary>
        /// Builds the named-argument dictionary entry for <c>InitialStart</c> in data-annotation
        /// mode, converting the stored <see cref="DateTime"/> to an ISO 8601 UTC round-trip string
        /// as expected by <see cref="ContinuousAggregatePolicyAttribute.InitialStart"/>.
        /// </summary>
        /// <param name="annotations">The entity's live annotation dictionary.</param>
        /// <param name="initialStartKey">The annotation key storing the <see cref="DateTime"/> value.</param>
        /// <param name="namedArgumentKey">
        ///     The name to use for the attribute named argument (typically <c>nameof(Attribute.InitialStart)</c>).
        /// </param>
        /// <param name="namedArgs">The dictionary to add the argument to when the annotation is present.</param>
        public static void AddInitialStartNamedArg(
            IDictionary<string, IAnnotation> annotations,
            string initialStartKey,
            string namedArgumentKey,
            Dictionary<string, object?> namedArgs)
        {
            IAnnotation? annotation = Find(annotations, initialStartKey);
            if (annotation?.Value is not DateTime initialStart)
            {
                return;
            }

            // ISO 8601 round-trip UTC string — matches ContinuousAggregatePolicyAttribute.InitialStart type (string).
            namedArgs[namedArgumentKey] = initialStart.ToUniversalTime().ToString("O");
        }
    }
}

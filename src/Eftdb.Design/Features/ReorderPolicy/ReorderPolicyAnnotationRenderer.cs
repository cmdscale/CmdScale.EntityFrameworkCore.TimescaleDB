using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRendererHelper;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.PolicyJobRendererHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ReorderPolicy
{
    /// <summary>
    /// Renders <c>TimescaleDB:HasReorderPolicy</c> and related annotations as a
    /// <c>.WithReorderPolicy(...)</c> Fluent API chain or a <c>[ReorderPolicy(...)]</c>
    /// attribute.
    /// </summary>
    internal sealed class ReorderPolicyAnnotationRenderer : IFeatureAnnotationRenderer
    {
        /// <summary>
        /// Reflects the scaffold-targeting <c>WithReorderPolicy</c> overload whose receiver is
        /// <c>EntityTypeBuilder&lt;&gt;</c>.
        /// </summary>
        private static readonly MethodInfo WithReorderPolicyMethod =
            typeof(ReorderPolicyTypeBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(ReorderPolicyTypeBuilder.WithReorderPolicy)
                         && m.GetParameters().Length == 6
                         && m.GetParameters()[0].ParameterType.IsGenericType
                         && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<>)
                         && m.ReturnType.IsGenericType
                         && m.ReturnType.GetGenericTypeDefinition() == typeof(ReorderPolicyStringBuilder<>));

        private static readonly MethodInfo WithInitialStartMethod =
            typeof(ReorderPolicyStringBuilder<>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == nameof(ReorderPolicyStringBuilder<>.WithInitialStart));

        public IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? indexName = GetString(annotations, ReorderPolicyAnnotations.IndexName);
            if (string.IsNullOrWhiteSpace(indexName))
            {
                return [];
            }

            string? scheduleInterval = GetString(annotations, ReorderPolicyAnnotations.ScheduleInterval);
            string? maxRuntime = GetString(annotations, ReorderPolicyAnnotations.MaxRuntime);
            int? maxRetries = Find(annotations, ReorderPolicyAnnotations.MaxRetries)?.Value is int r ? r : null;
            string? retryPeriod = GetString(annotations, ReorderPolicyAnnotations.RetryPeriod);

            object?[] entryArgs = BuildReorderPolicyArgs(indexName, scheduleInterval, maxRuntime, maxRetries, retryPeriod);
            MethodCallCodeFragment call = new(WithReorderPolicyMethod, entryArgs);

            call = ChainInitialStart(call, annotations, ReorderPolicyAnnotations.InitialStart, WithInitialStartMethod);

            ConsumeAllReorderAnnotations(annotations);
            return [call];
        }

        public IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? indexName = GetString(annotations, ReorderPolicyAnnotations.IndexName);
            if (string.IsNullOrWhiteSpace(indexName))
            {
                ConsumeAllReorderAnnotations(annotations);
                return [];
            }

            object?[] positionalArgs = [indexName];
            Dictionary<string, object?> namedArgs = [];

            string? scheduleInterval = GetString(annotations, ReorderPolicyAnnotations.ScheduleInterval);
            if (!string.IsNullOrWhiteSpace(scheduleInterval))
            {
                namedArgs[nameof(ReorderPolicyAttribute.ScheduleInterval)] = scheduleInterval;
            }

            string? maxRuntime = GetString(annotations, ReorderPolicyAnnotations.MaxRuntime);
            if (!string.IsNullOrWhiteSpace(maxRuntime))
            {
                namedArgs[nameof(ReorderPolicyAttribute.MaxRuntime)] = maxRuntime;
            }

            AddInitialStartNamedArg(
                annotations,
                ReorderPolicyAnnotations.InitialStart,
                nameof(ReorderPolicyAttribute.InitialStart),
                namedArgs);

            if (Find(annotations, ReorderPolicyAnnotations.MaxRetries)?.Value is int maxRetries)
            {
                namedArgs[nameof(ReorderPolicyAttribute.MaxRetries)] = maxRetries;
            }

            string? retryPeriod = GetString(annotations, ReorderPolicyAnnotations.RetryPeriod);
            if (!string.IsNullOrWhiteSpace(retryPeriod))
            {
                namedArgs[nameof(ReorderPolicyAttribute.RetryPeriod)] = retryPeriod;
            }

            ConsumeAllReorderAnnotations(annotations);
            return [new AttributeCodeFragment(typeof(ReorderPolicyAttribute), positionalArgs, namedArgs)];
        }

        public void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return;
            }

            ConsumeAllReorderAnnotations(annotations);
        }

        /// <summary>
        /// Guards all three rendering paths: a reorder policy annotation is only emitted when the
        /// hypertable renderer already succeeded. The hypertable renderer consumes <c>IsHypertable</c>
        /// on success. If <c>IsHypertable</c> is still present the hypertable renderer failed and the
        /// reorder policy annotations must be left for the <c>.HasAnnotation</c> fallback.
        /// </summary>
        private static bool ShouldRender(IDictionary<string, IAnnotation> annotations)
            => Find(annotations, ReorderPolicyAnnotations.HasReorderPolicy)?.Value is true
            && Find(annotations, HypertableAnnotations.IsHypertable) is null;

        /// <summary>
        /// Builds positional arguments for the scaffold-targeting <c>WithReorderPolicy</c> overload.
        /// All five arguments are always emitted: trimmed forms could resolve against the user-facing
        /// overload whose second parameter is <c>initialStart</c> (<c>DateTime?</c>), silently binding
        /// later arguments to the wrong parameters.
        /// </summary>
        private static object?[] BuildReorderPolicyArgs(
            string indexName,
            string? scheduleInterval,
            string? maxRuntime,
            int? maxRetries,
            string? retryPeriod)
            => [indexName, scheduleInterval, maxRuntime, maxRetries, retryPeriod];

        private static void ConsumeAllReorderAnnotations(IDictionary<string, IAnnotation> annotations)
        {
            Consume(annotations,
                ReorderPolicyAnnotations.HasReorderPolicy,
                ReorderPolicyAnnotations.IndexName,
                ReorderPolicyAnnotations.InitialStart,
                ReorderPolicyAnnotations.ScheduleInterval,
                ReorderPolicyAnnotations.MaxRuntime,
                ReorderPolicyAnnotations.MaxRetries,
                ReorderPolicyAnnotations.RetryPeriod);
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.AnnotationRendererHelper;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.PolicyJobRendererHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers
{
    /// <summary>
    /// Renders <c>TimescaleDB:HasRetentionPolicy</c> and related annotations as a
    /// <c>.WithRetentionPolicy(...)</c> Fluent API chain or a <c>[RetentionPolicy(...)]</c>
    /// attribute.
    /// </summary>
    internal sealed class RetentionPolicyAnnotationRenderer : IFeatureAnnotationRenderer
    {
        private static readonly MethodInfo WithRetentionPolicyMethod =
            typeof(RetentionPolicyTypeBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(RetentionPolicyTypeBuilder.WithRetentionPolicy)
                         && m.GetParameters().Length == 7
                         && m.GetParameters()[0].ParameterType.IsGenericType
                         && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<>)
                         && m.GetParameters()[3].ParameterType == typeof(string)
                         && m.ReturnType.IsGenericType
                         && m.ReturnType.GetGenericTypeDefinition() == typeof(RetentionPolicyStringBuilder<>));

        private static readonly MethodInfo WithInitialStartMethod =
            typeof(RetentionPolicyStringBuilder<>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == nameof(RetentionPolicyStringBuilder<>.WithInitialStart));

        public IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? dropAfter = GetString(annotations, RetentionPolicyAnnotations.DropAfter);
            string? dropCreatedBefore = GetString(annotations, RetentionPolicyAnnotations.DropCreatedBefore);
            string? scheduleInterval = GetString(annotations, RetentionPolicyAnnotations.ScheduleInterval);
            string? maxRuntime = GetString(annotations, RetentionPolicyAnnotations.MaxRuntime);
            int? maxRetries = Find(annotations, RetentionPolicyAnnotations.MaxRetries)?.Value is int r ? r : null;
            string? retryPeriod = GetString(annotations, RetentionPolicyAnnotations.RetryPeriod);

            object?[] entryArgs = BuildRetentionPolicyArgs(dropAfter, dropCreatedBefore, scheduleInterval, maxRuntime, maxRetries, retryPeriod);
            MethodCallCodeFragment call = new(WithRetentionPolicyMethod, entryArgs);

            call = ChainInitialStart(call, annotations, RetentionPolicyAnnotations.InitialStart, WithInitialStartMethod);

            ConsumeAllRetentionAnnotations(annotations);
            return [call];
        }

        public IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? dropAfter = GetString(annotations, RetentionPolicyAnnotations.DropAfter);
            string? dropCreatedBefore = GetString(annotations, RetentionPolicyAnnotations.DropCreatedBefore);
            object?[] positionalArgs = dropAfter is not null
                ? [dropAfter]
                : [null, dropCreatedBefore];
            Dictionary<string, object?> namedArgs = [];

            string? scheduleInterval = GetString(annotations, RetentionPolicyAnnotations.ScheduleInterval);
            if (!string.IsNullOrWhiteSpace(scheduleInterval))
            {
                namedArgs[nameof(RetentionPolicyAttribute.ScheduleInterval)] = scheduleInterval;
            }

            string? maxRuntime = GetString(annotations, RetentionPolicyAnnotations.MaxRuntime);
            if (!string.IsNullOrWhiteSpace(maxRuntime))
            {
                namedArgs[nameof(RetentionPolicyAttribute.MaxRuntime)] = maxRuntime;
            }

            AddInitialStartNamedArg(
                annotations,
                RetentionPolicyAnnotations.InitialStart,
                nameof(RetentionPolicyAttribute.InitialStart),
                namedArgs);

            if (Find(annotations, RetentionPolicyAnnotations.MaxRetries)?.Value is int maxRetries)
            {
                namedArgs[nameof(RetentionPolicyAttribute.MaxRetries)] = maxRetries;
            }

            string? retryPeriod = GetString(annotations, RetentionPolicyAnnotations.RetryPeriod);
            if (!string.IsNullOrWhiteSpace(retryPeriod))
            {
                namedArgs[nameof(RetentionPolicyAttribute.RetryPeriod)] = retryPeriod;
            }

            ConsumeAllRetentionAnnotations(annotations);
            return [new AttributeCodeFragment(typeof(RetentionPolicyAttribute), positionalArgs, namedArgs)];
        }

        public void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return;
            }

            ConsumeAllRetentionAnnotations(annotations);
        }

        /// <summary>
        /// Guards all three rendering paths: a retention policy annotation is only emitted when a
        /// parent feature renderer already succeeded. The hypertable renderer consumes
        /// <c>IsHypertable</c> on success; the continuous aggregate renderer consumes
        /// <c>MaterializedViewName</c> on success. If either key annotation is still present in the
        /// dictionary the corresponding parent renderer failed or was not applicable, and the
        /// retention policy annotations must be left for the <c>.HasAnnotation</c> fallback.
        /// </summary>
        private static bool ShouldRender(IDictionary<string, IAnnotation> annotations)
            => Find(annotations, RetentionPolicyAnnotations.HasRetentionPolicy)?.Value is true
            && Find(annotations, HypertableAnnotations.IsHypertable) is null
            && Find(annotations, ContinuousAggregateAnnotations.MaterializedViewName) is null;

        /// <summary>
        /// Builds positional arguments for the scaffold-targeting <c>WithRetentionPolicy</c> overload.
        /// All six arguments are always emitted: trimmed forms resolve against the user-facing overload
        /// whose third parameter is <c>initialStart</c> (<c>DateTime?</c>), which either fails to compile
        /// or silently maps later arguments onto the wrong parameters.
        /// </summary>
        private static object?[] BuildRetentionPolicyArgs(
            string? dropAfter,
            string? dropCreatedBefore,
            string? scheduleInterval,
            string? maxRuntime,
            int? maxRetries,
            string? retryPeriod)
            => [dropAfter, dropCreatedBefore, scheduleInterval, maxRuntime, maxRetries, retryPeriod];

        private static void ConsumeAllRetentionAnnotations(IDictionary<string, IAnnotation> annotations)
        {
            Consume(annotations,
                RetentionPolicyAnnotations.HasRetentionPolicy,
                RetentionPolicyAnnotations.DropAfter,
                RetentionPolicyAnnotations.DropCreatedBefore,
                RetentionPolicyAnnotations.InitialStart,
                RetentionPolicyAnnotations.ScheduleInterval,
                RetentionPolicyAnnotations.MaxRuntime,
                RetentionPolicyAnnotations.MaxRetries,
                RetentionPolicyAnnotations.RetryPeriod);
        }
    }
}

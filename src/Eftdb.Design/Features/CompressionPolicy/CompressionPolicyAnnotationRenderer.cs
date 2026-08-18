using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRendererHelper;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.PolicyJobRendererHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy
{
    /// <summary>
    /// Renders <c>TimescaleDB:HasCompressionPolicy</c> and related annotations as a
    /// <c>.WithCompressionPolicy(...)</c> Fluent API chain or a <c>[CompressionPolicy(...)]</c>
    /// attribute.
    /// </summary>
    internal sealed class CompressionPolicyAnnotationRenderer : IFeatureAnnotationRenderer
    {
        /// <summary>
        /// Reflects the scaffold-targeting <c>WithCompressionPolicy</c> overload whose receiver is
        /// <c>EntityTypeBuilder&lt;&gt;</c> and returns <c>CompressionPolicyStringBuilder&lt;&gt;</c>.
        /// </summary>
        private static readonly MethodInfo WithCompressionPolicyMethod =
            typeof(CompressionPolicyTypeBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(CompressionPolicyTypeBuilder.WithCompressionPolicy)
                         && m.GetParameters().Length == 6
                         && m.GetParameters()[0].ParameterType.IsGenericType
                         && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<>)
                         && m.ReturnType.IsGenericType
                         && m.ReturnType.GetGenericTypeDefinition() == typeof(CompressionPolicyStringBuilder<>));

        private static readonly MethodInfo WithInitialStartMethod =
            typeof(CompressionPolicyStringBuilder<>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == nameof(CompressionPolicyStringBuilder<>.WithInitialStart));

        public IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? after = GetString(annotations, CompressionPolicyAnnotations.After);
            string? createdBefore = GetString(annotations, CompressionPolicyAnnotations.CreatedBefore);
            string? scheduleInterval = GetString(annotations, CompressionPolicyAnnotations.ScheduleInterval);
            string? timezone = GetString(annotations, CompressionPolicyAnnotations.Timezone);
            bool? ifNotExists = Find(annotations, CompressionPolicyAnnotations.IfNotExists)?.Value is bool b ? b : null;

            object?[] entryArgs = BuildCompressionPolicyArgs(after, createdBefore, scheduleInterval, timezone, ifNotExists);
            MethodCallCodeFragment call = new(WithCompressionPolicyMethod, entryArgs);

            call = ChainInitialStart(call, annotations, CompressionPolicyAnnotations.InitialStart, WithInitialStartMethod);

            ConsumeAllCompressionPolicyAnnotations(annotations);
            return [call];
        }

        public IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? after = GetString(annotations, CompressionPolicyAnnotations.After);
            string? createdBefore = GetString(annotations, CompressionPolicyAnnotations.CreatedBefore);
            Dictionary<string, object?> namedArgs = [];

            if (!string.IsNullOrWhiteSpace(after))
            {
                namedArgs[nameof(CompressionPolicyAttribute.After)] = after;
            }

            if (!string.IsNullOrWhiteSpace(createdBefore))
            {
                namedArgs[nameof(CompressionPolicyAttribute.CreatedBefore)] = createdBefore;
            }

            string? scheduleInterval = GetString(annotations, CompressionPolicyAnnotations.ScheduleInterval);
            if (!string.IsNullOrWhiteSpace(scheduleInterval))
            {
                namedArgs[nameof(CompressionPolicyAttribute.ScheduleInterval)] = scheduleInterval;
            }

            AddInitialStartNamedArg(
                annotations,
                CompressionPolicyAnnotations.InitialStart,
                nameof(CompressionPolicyAttribute.InitialStart),
                namedArgs);

            string? timezone = GetString(annotations, CompressionPolicyAnnotations.Timezone);
            if (!string.IsNullOrWhiteSpace(timezone))
            {
                namedArgs[nameof(CompressionPolicyAttribute.Timezone)] = timezone;
            }

            if (Find(annotations, CompressionPolicyAnnotations.IfNotExists)?.Value is bool ifNotExists)
            {
                namedArgs[nameof(CompressionPolicyAttribute.IfNotExists)] = ifNotExists;
            }

            ConsumeAllCompressionPolicyAnnotations(annotations);
            return [new AttributeCodeFragment(typeof(CompressionPolicyAttribute), [], namedArgs)];
        }

        public void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return;
            }

            ConsumeAllCompressionPolicyAnnotations(annotations);
        }

        /// <summary>
        /// Guards all three rendering paths: a compression policy annotation is only emitted when the
        /// parent feature renderer already succeeded. For hypertables, the hypertable renderer consumes
        /// <c>IsHypertable</c> on success — if it is still present the hypertable renderer failed.
        /// For continuous aggregates, the CA renderer consumes <c>MaterializedViewName</c> on success —
        /// if it is still present the CA renderer failed. Both checks must pass: the policy annotation
        /// is left for the <c>.HasAnnotation</c> fallback when either parent renderer did not succeed.
        /// </summary>
        private static bool ShouldRender(IDictionary<string, IAnnotation> annotations)
            => Find(annotations, CompressionPolicyAnnotations.HasCompressionPolicy)?.Value is true
            && Find(annotations, HypertableAnnotations.IsHypertable) is null
            && Find(annotations, ContinuousAggregateAnnotations.MaterializedViewName) is null;

        /// <summary>
        /// Builds positional arguments for the scaffold-targeting <c>WithCompressionPolicy</c> overload.
        /// All five arguments are always emitted: trimmed forms could resolve against the user-facing
        /// overload whose fourth parameter is <c>initialStart</c> (<c>DateTime?</c>), silently binding
        /// later arguments to the wrong parameters.
        /// </summary>
        private static object?[] BuildCompressionPolicyArgs(
            string? after,
            string? createdBefore,
            string? scheduleInterval,
            string? timezone,
            bool? ifNotExists)
            => [after, createdBefore, scheduleInterval, timezone, ifNotExists];

        private static void ConsumeAllCompressionPolicyAnnotations(IDictionary<string, IAnnotation> annotations)
        {
            Consume(annotations,
                CompressionPolicyAnnotations.HasCompressionPolicy,
                CompressionPolicyAnnotations.After,
                CompressionPolicyAnnotations.CreatedBefore,
                CompressionPolicyAnnotations.InitialStart,
                CompressionPolicyAnnotations.ScheduleInterval,
                CompressionPolicyAnnotations.Timezone,
                CompressionPolicyAnnotations.IfNotExists);
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.AnnotationRendererHelper;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers.PolicyJobRendererHelper;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers
{
    /// <summary>
    /// Renders <c>TimescaleDB:ContinuousAggregatePolicy:*</c> annotations as a
    /// <c>.WithRefreshPolicy(...)</c> Fluent API chain or a <c>[ContinuousAggregatePolicy(...)]</c>
    /// attribute.
    /// </summary>
    internal sealed class ContinuousAggregatePolicyAnnotationRenderer : IFeatureAnnotationRenderer
    {
        private static readonly MethodInfo WithRefreshPolicyMethod =
            typeof(ContinuousAggregateBuilderPolicyExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(ContinuousAggregateBuilderPolicyExtensions.WithRefreshPolicy)
                         && m.GetParameters().Length == 4
                         && m.GetParameters()[0].ParameterType.IsGenericType
                         && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ContinuousAggregateStringBuilder<>));

        private static MethodInfo PolicyBuilderMethod(string name) =>
            typeof(ContinuousAggregatePolicyStringBuilder<>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == name);

        private static readonly MethodInfo WithInitialStartMethod = PolicyBuilderMethod(nameof(ContinuousAggregatePolicyStringBuilder<object>.WithInitialStart));
        private static readonly MethodInfo WithIncludeTieredDataMethod = PolicyBuilderMethod(nameof(ContinuousAggregatePolicyStringBuilder<object>.WithIncludeTieredData));
        private static readonly MethodInfo WithBucketsPerBatchMethod = PolicyBuilderMethod(nameof(ContinuousAggregatePolicyStringBuilder<object>.WithBucketsPerBatch));
        private static readonly MethodInfo WithMaxBatchesPerExecutionMethod = PolicyBuilderMethod(nameof(ContinuousAggregatePolicyStringBuilder<object>.WithMaxBatchesPerExecution));
        private static readonly MethodInfo WithRefreshNewestFirstMethod = PolicyBuilderMethod(nameof(ContinuousAggregatePolicyStringBuilder<object>.WithRefreshNewestFirst));

        public IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            string? startOffset = GetString(annotations, ContinuousAggregatePolicyAnnotations.StartOffset);
            string? endOffset = GetString(annotations, ContinuousAggregatePolicyAnnotations.EndOffset);
            string? scheduleInterval = GetString(annotations, ContinuousAggregatePolicyAnnotations.ScheduleInterval);

            object?[] entryArgs = BuildRefreshPolicyArgs(startOffset, endOffset, scheduleInterval);
            MethodCallCodeFragment call = new(WithRefreshPolicyMethod, entryArgs);

            // Chain InitialStart via the shared job helper
            call = ChainInitialStart(call, annotations, ContinuousAggregatePolicyAnnotations.InitialStart, WithInitialStartMethod);

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.IncludeTieredData)?.Value is bool includeTieredData)
            {
                call = call.Chain(WithIncludeTieredDataMethod, includeTieredData);
            }

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value is int bucketsPerBatch)
            {
                call = call.Chain(WithBucketsPerBatchMethod, bucketsPerBatch);
            }

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value is int maxBatches)
            {
                call = call.Chain(WithMaxBatchesPerExecutionMethod, maxBatches);
            }

            // RefreshNewestFirst is only stored when false (the default is true), so emit WithRefreshNewestFirst(false).
            if (Find(annotations, ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value is bool refreshNewestFirst)
            {
                call = call.Chain(WithRefreshNewestFirstMethod, refreshNewestFirst);
            }

            ConsumeAllPolicyAnnotations(annotations);
            return [call];
        }

        public IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return [];
            }

            Dictionary<string, object?> namedArgs = [];

            string? startOffset = GetString(annotations, ContinuousAggregatePolicyAnnotations.StartOffset);
            if (!string.IsNullOrWhiteSpace(startOffset))
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.StartOffset)] = startOffset;
            }

            string? endOffset = GetString(annotations, ContinuousAggregatePolicyAnnotations.EndOffset);
            if (!string.IsNullOrWhiteSpace(endOffset))
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.EndOffset)] = endOffset;
            }

            string? scheduleInterval = GetString(annotations, ContinuousAggregatePolicyAnnotations.ScheduleInterval);
            if (!string.IsNullOrWhiteSpace(scheduleInterval))
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.ScheduleInterval)] = scheduleInterval;
            }

            // InitialStart is stored as DateTime in the annotation; the attribute expects an ISO 8601 string.
            AddInitialStartNamedArg(
                annotations,
                ContinuousAggregatePolicyAnnotations.InitialStart,
                nameof(ContinuousAggregatePolicyAttribute.InitialStart),
                namedArgs);

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.IncludeTieredData)?.Value is bool includeTieredData)
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.IncludeTieredData)] = includeTieredData;
            }

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.BucketsPerBatch)?.Value is int bucketsPerBatch)
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.BucketsPerBatch)] = bucketsPerBatch;
            }

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution)?.Value is int maxBatches)
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.MaxBatchesPerExecution)] = maxBatches;
            }

            if (Find(annotations, ContinuousAggregatePolicyAnnotations.RefreshNewestFirst)?.Value is bool refreshNewestFirst)
            {
                namedArgs[nameof(ContinuousAggregatePolicyAttribute.RefreshNewestFirst)] = refreshNewestFirst;
            }

            ConsumeAllPolicyAnnotations(annotations);
            return [new AttributeCodeFragment(typeof(ContinuousAggregatePolicyAttribute), [], namedArgs)];
        }

        public void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            if (!ShouldRender(annotations))
            {
                return;
            }

            ConsumeAllPolicyAnnotations(annotations);
        }

        /// <summary>
        /// Guards all three rendering paths: a refresh policy annotation is only emitted when the CA
        /// renderer already succeeded (it consumes <c>MaterializedViewName</c> on success). If
        /// <c>MaterializedViewName</c> is still in the dictionary the CA render failed and the policy
        /// annotations must be left for the <c>.HasAnnotation</c> fallback.
        /// </summary>
        private static bool ShouldRender(IDictionary<string, IAnnotation> annotations)
            => Find(annotations, ContinuousAggregatePolicyAnnotations.HasRefreshPolicy)?.Value is true
            && Find(annotations, ContinuousAggregateAnnotations.MaterializedViewName) is null;

        /// <summary>
        /// Builds positional arguments for <c>WithRefreshPolicy(startOffset, endOffset, scheduleInterval)</c>,
        /// trimming trailing null arguments when the non-null values are in leading positions only.
        /// All three must be emitted when <c>scheduleInterval</c> is non-null because it is third-positional.
        /// </summary>
        private static object?[] BuildRefreshPolicyArgs(string? startOffset, string? endOffset, string? scheduleInterval)
        {
            if (scheduleInterval is not null)
            {
                return [startOffset, endOffset, scheduleInterval];
            }

            if (endOffset is not null)
            {
                return [startOffset, endOffset];
            }

            if (startOffset is not null)
            {
                return [startOffset];
            }

            return [];
        }

        private static void ConsumeAllPolicyAnnotations(IDictionary<string, IAnnotation> annotations)
        {
            Consume(annotations,
                ContinuousAggregatePolicyAnnotations.HasRefreshPolicy,
                ContinuousAggregatePolicyAnnotations.StartOffset,
                ContinuousAggregatePolicyAnnotations.EndOffset,
                ContinuousAggregatePolicyAnnotations.ScheduleInterval,
                ContinuousAggregatePolicyAnnotations.InitialStart,
                ContinuousAggregatePolicyAnnotations.IfNotExists,
                ContinuousAggregatePolicyAnnotations.IncludeTieredData,
                ContinuousAggregatePolicyAnnotations.BucketsPerBatch,
                ContinuousAggregatePolicyAnnotations.MaxBatchesPerExecution,
                ContinuousAggregatePolicyAnnotations.RefreshNewestFirst);
        }
    }
}

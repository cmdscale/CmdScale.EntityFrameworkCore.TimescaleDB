using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.ReorderPolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Applies reorder policy annotations to scaffolded database tables.
    /// </summary>
    public sealed class ReorderPolicyAnnotationApplier : IAnnotationApplier
    {
        public void ApplyAnnotations(DatabaseTable table, object featureInfo)
        {
            if (featureInfo is not ReorderPolicyInfo policyInfo)
            {
                throw new ArgumentException($"Expected {nameof(ReorderPolicyInfo)}, got {featureInfo.GetType().Name}", nameof(featureInfo));
            }

            table[ReorderPolicyAnnotations.HasReorderPolicy] = true;
            table[ReorderPolicyAnnotations.IndexName] = policyInfo.IndexName;

            if (policyInfo.InitialStart.HasValue)
            {
                table[ReorderPolicyAnnotations.InitialStart] = policyInfo.InitialStart.Value;
            }

            // Set annotations only if they differ from TimescaleDB defaults
            if (policyInfo.ScheduleInterval != DefaultValues.ReorderPolicyScheduleInterval)
            {
                table[ReorderPolicyAnnotations.ScheduleInterval] = policyInfo.ScheduleInterval;
            }

            if (policyInfo.MaxRuntime != DefaultValues.ReorderPolicyMaxRuntime)
            {
                table[ReorderPolicyAnnotations.MaxRuntime] = policyInfo.MaxRuntime;
            }

            if (policyInfo.MaxRetries != DefaultValues.ReorderPolicyMaxRetries)
            {
                table[ReorderPolicyAnnotations.MaxRetries] = policyInfo.MaxRetries;
            }

            if (policyInfo.RetryPeriod != DefaultValues.ReorderPolicyRetryPeriod)
            {
                table[ReorderPolicyAnnotations.RetryPeriod] = policyInfo.RetryPeriod;
            }
        }
    }
}

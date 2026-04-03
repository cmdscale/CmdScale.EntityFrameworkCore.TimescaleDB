using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.RetentionPolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Applies retention policy annotations to scaffolded database tables.
    /// </summary>
    public sealed class RetentionPolicyAnnotationApplier : IAnnotationApplier
    {
        public void ApplyAnnotations(DatabaseTable table, object featureInfo)
        {
            if (featureInfo is not RetentionPolicyInfo policyInfo)
            {
                throw new ArgumentException($"Expected {nameof(RetentionPolicyInfo)}, got {featureInfo.GetType().Name}", nameof(featureInfo));
            }

            table[RetentionPolicyAnnotations.HasRetentionPolicy] = true;

            if (!string.IsNullOrWhiteSpace(policyInfo.DropAfter))
            {
                table[RetentionPolicyAnnotations.DropAfter] = policyInfo.DropAfter;
            }

            if (!string.IsNullOrWhiteSpace(policyInfo.DropCreatedBefore))
            {
                table[RetentionPolicyAnnotations.DropCreatedBefore] = policyInfo.DropCreatedBefore;
            }

            if (policyInfo.InitialStart.HasValue)
            {
                table[RetentionPolicyAnnotations.InitialStart] = policyInfo.InitialStart.Value;
            }

            // Set annotations only if they differ from TimescaleDB defaults
            if (policyInfo.ScheduleInterval != DefaultValues.RetentionPolicyScheduleInterval)
            {
                table[RetentionPolicyAnnotations.ScheduleInterval] = policyInfo.ScheduleInterval;
            }

            if (policyInfo.MaxRuntime != DefaultValues.RetentionPolicyMaxRuntime)
            {
                table[RetentionPolicyAnnotations.MaxRuntime] = policyInfo.MaxRuntime;
            }

            if (policyInfo.MaxRetries != DefaultValues.RetentionPolicyMaxRetries)
            {
                table[RetentionPolicyAnnotations.MaxRetries] = policyInfo.MaxRetries;
            }

            if (policyInfo.RetryPeriod != DefaultValues.RetentionPolicyScheduleInterval)
            {
                table[RetentionPolicyAnnotations.RetryPeriod] = policyInfo.RetryPeriod;
            }
        }
    }
}

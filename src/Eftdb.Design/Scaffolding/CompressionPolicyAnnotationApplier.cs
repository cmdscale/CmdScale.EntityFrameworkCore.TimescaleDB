using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.CompressionPolicyScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Applies compression policy annotations to scaffolded database tables.
    /// </summary>
    public sealed class CompressionPolicyAnnotationApplier : IAnnotationApplier
    {
        public void ApplyAnnotations(DatabaseTable table, object featureInfo)
        {
            if (featureInfo is not CompressionPolicyInfo policyInfo)
            {
                throw new ArgumentException($"Expected {nameof(CompressionPolicyInfo)}, got {featureInfo.GetType().Name}", nameof(featureInfo));
            }

            table[CompressionPolicyAnnotations.HasCompressionPolicy] = true;

            if (!string.IsNullOrWhiteSpace(policyInfo.After))
            {
                table[CompressionPolicyAnnotations.After] = policyInfo.After;
            }

            if (!string.IsNullOrWhiteSpace(policyInfo.CreatedBefore))
            {
                table[CompressionPolicyAnnotations.CreatedBefore] = policyInfo.CreatedBefore;
            }

            if (policyInfo.InitialStart.HasValue)
            {
                table[CompressionPolicyAnnotations.InitialStart] = policyInfo.InitialStart.Value;
            }

            // Only emit the schedule interval when it differs from the TimescaleDB-computed default for
            // this hypertable's chunk interval. This prevents phantom alters on roundtrip: if the user
            // never set a schedule_interval and the DB stored the default, the scaffolded model must also
            // carry no explicit interval so the differ sees null == null.
            // The hypertable annotation applier runs before the compression policy applier (registration
            // order in TimescaleDatabaseModelFactory), so the ChunkTimeInterval annotation is already set.
            if (!string.IsNullOrWhiteSpace(policyInfo.ScheduleInterval))
            {
                string? chunkTimeInterval = table[HypertableAnnotations.ChunkTimeInterval] as string;
                string? computedDefault = CompressionPolicyDefaultHelper.ComputeDefaultScheduleInterval(chunkTimeInterval);

                if (computedDefault == null || policyInfo.ScheduleInterval != computedDefault)
                {
                    table[CompressionPolicyAnnotations.ScheduleInterval] = policyInfo.ScheduleInterval;
                }
            }

            if (!string.IsNullOrWhiteSpace(policyInfo.Timezone))
            {
                table[CompressionPolicyAnnotations.Timezone] = policyInfo.Timezone;
            }

            if (policyInfo.IfNotExists.HasValue)
            {
                table[CompressionPolicyAnnotations.IfNotExists] = policyInfo.IfNotExists.Value;
            }
        }
    }
}

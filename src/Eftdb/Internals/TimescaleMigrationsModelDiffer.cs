using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.RetentionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update.Internal;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
#pragma warning disable EF1001 // Suppress warning about internal APIs usage, common for providers/extensions
    public class TimescaleMigrationsModelDiffer(
        IRelationalTypeMappingSource typeMappingSource,
        IMigrationsAnnotationProvider migrationsAnnotationProvider,
        IRelationalAnnotationProvider relationalAnnotationProvider,
        IRowIdentityMapFactory rowIdentityMapFactory,
        CommandBatchPreparerDependencies commandBatchPreparerDependencies) : MigrationsModelDiffer(typeMappingSource, migrationsAnnotationProvider, relationalAnnotationProvider, rowIdentityMapFactory, commandBatchPreparerDependencies)
    {
        private readonly IReadOnlyList<IFeatureDiffer> _featureDiffers = [
                new HypertableDiffer(),
                new ReorderPolicyDiffer(),
                new ContinuousAggregateDiffer(),
                new ContinuousAggregatePolicyDiffer(),
                new RetentionPolicyDiffer(),
            ];

        public override IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            // Get all operations
            List<MigrationOperation> allOperations = [.. base.GetDifferences(source, target)];

            foreach (IFeatureDiffer differ in _featureDiffers)
            {
                allOperations.AddRange(differ.GetDifferences(source, target));
            }

            // Sort the entire list based on the priority defined in the helper method
            List<MigrationOperation> sortedOperations = [.. allOperations.OrderBy(GetOperationPriority)];
            return sortedOperations;
        }

        /// <summary>
        /// Assigns a priority to operations to ensure correct execution order.
        /// Lower numbers execute first.
        /// Add/Create operations use positive priorities (run after standard EF table creation).
        /// Drop operations use negative priorities (run before standard EF table drops).
        /// </summary>
        private static int GetOperationPriority(MigrationOperation operation)
        {
            switch (operation)
            {
                // --- Drop operations: negative priorities, reverse dependency order ---
                // Retention policies depend on hypertables and continuous aggregates
                case DropRetentionPolicyOperation:
                    return -60;

                // CA policies depend on continuous aggregates
                case RemoveContinuousAggregatePolicyOperation:
                    return -50;

                // Continuous aggregates depend on parent hypertables
                case DropContinuousAggregateOperation:
                    return -40;

                // Reorder policies depend on hypertables
                case DropReorderPolicyOperation:
                    return -20;

                // --- Add/Alter operations: positive priorities, dependency order ---
                case CreateHypertableOperation:
                    return 10;

                case AddReorderPolicyOperation:
                case AlterReorderPolicyOperation:
                    return 20;

                case CreateContinuousAggregateOperation:
                    return 30;
                case AlterContinuousAggregateOperation:
                    return 40;

                case AddContinuousAggregatePolicyOperation:
                    return 50;

                case AddRetentionPolicyOperation:
                case AlterRetentionPolicyOperation:
                    return 60;

                // Standard EF Core operations (CreateTable, DropTable, etc.)
                default:
                    return 0;
            }
        }
    }
#pragma warning restore EF1001
}
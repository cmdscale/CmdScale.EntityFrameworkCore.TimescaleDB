using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregatePolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ContinuousAggregates;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.ReorderPolicies;
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
        /// </summary>
        private static int GetOperationPriority(MigrationOperation operation)
        {
            switch (operation)
            {
                case CreateHypertableOperation:
                    return 10;

                case AddReorderPolicyOperation:
                case AlterReorderPolicyOperation:
                case DropReorderPolicyOperation:
                    return 20;

                case CreateContinuousAggregateOperation:
                    return 30;
                case AlterContinuousAggregateOperation:
                case DropContinuousAggregateOperation:
                    return 40;

                case AddContinuousAggregatePolicyOperation:
                case RemoveContinuousAggregatePolicyOperation:
                    return 50;

                // Standard EF Core operations (CreateTable, etc.)
                default:
                    return 0;
            }
        }
    }
#pragma warning restore EF1001
}
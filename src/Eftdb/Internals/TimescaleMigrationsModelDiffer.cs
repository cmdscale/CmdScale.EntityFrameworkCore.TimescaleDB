using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;
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
        public override IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
        {
            // Standard EF Core operations, which include the rename operations the feature differs need to
            // distinguish a rename from a drop-and-create.
            List<MigrationOperation> allOperations = [.. base.GetDifferences(source, target)];

            FeatureDiffContext context = BuildContext(allOperations);

            allOperations.AddRange(new HypertableDiffer().GetDifferences(source, target, context));
            allOperations.AddRange(new ReorderPolicyDiffer().GetDifferences(source, target, context));

            IReadOnlyList<MigrationOperation> aggregateOperations = new ContinuousAggregateDiffer().GetDifferences(source, target, context);
            allOperations.AddRange(aggregateOperations);

            PopulateRecreatedAggregates(aggregateOperations, context.RecreatedAggregates);

            allOperations.AddRange(new ContinuousAggregatePolicyDiffer().GetDifferences(source, target, context));
            allOperations.AddRange(new RetentionPolicyDiffer().GetDifferences(source, target, context));
            allOperations.AddRange(new CompressionPolicyDiffer().GetDifferences(source, target, context));

            // Sort the entire list based on the priority defined in the helper method
            List<MigrationOperation> sortedOperations = [.. allOperations.OrderBy(GetOperationPriority)];
            return sortedOperations;
        }

        /// <summary>
        /// Builds rename maps from the standard EF Core operations so feature differs can recognize renamed
        /// tables, indexes, and columns instead of treating them as drop-and-create.
        /// </summary>
        private static FeatureDiffContext BuildContext(IEnumerable<MigrationOperation> baseOperations)
        {
            Dictionary<(string, string), (string, string)> tableRenames = [];
            Dictionary<(string, string), (string, string)> indexRenames = [];
            Dictionary<(string, string, string), string> columnRenames = [];

            foreach (MigrationOperation operation in baseOperations)
            {
                switch (operation)
                {
                    case RenameTableOperation rename:
                        {
                            string oldSchema = rename.Schema ?? DefaultValues.DefaultSchema;
                            string newSchema = rename.NewSchema ?? rename.Schema ?? DefaultValues.DefaultSchema;
                            string newName = rename.NewName ?? rename.Name;
                            tableRenames[(oldSchema, rename.Name)] = (newSchema, newName);
                            break;
                        }
                    case RenameIndexOperation rename when rename.NewName != null:
                        {
                            string schema = rename.Schema ?? DefaultValues.DefaultSchema;
                            indexRenames[(schema, rename.Name)] = (schema, rename.NewName);
                            break;
                        }
                    case RenameColumnOperation rename:
                        {
                            // RenameColumnOperation targets the table by its post-rename name, so the column key
                            // uses the new table name to line up with rename-rewritten source operations.
                            string schema = rename.Schema ?? DefaultValues.DefaultSchema;
                            columnRenames[(schema, rename.Table, rename.Name)] = rename.NewName;
                            break;
                        }
                }
            }

            return new FeatureDiffContext
            {
                TableRenames = tableRenames,
                IndexRenames = indexRenames,
                ColumnRenames = columnRenames,
                RecreatedAggregates = new HashSet<(string, string)>(),
            };
        }

        /// <summary>
        /// Records continuous aggregates that appear in both a drop and a create operation, signalling that their
        /// refresh and retention policies must be re-added after the recreate.
        /// </summary>
        private static void PopulateRecreatedAggregates(IReadOnlyList<MigrationOperation> aggregateOperations, ISet<(string Schema, string ViewName)> recreated)
        {
            HashSet<(string, string)> dropped = [.. aggregateOperations
                .OfType<DropContinuousAggregateOperation>()
                .Select(o => (o.Schema, o.MaterializedViewName))];

            foreach (CreateContinuousAggregateOperation create in aggregateOperations.OfType<CreateContinuousAggregateOperation>())
            {
                if (dropped.Contains((create.Schema, create.MaterializedViewName)))
                {
                    recreated.Add((create.Schema, create.MaterializedViewName));
                }
            }
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

                case DropCompressionPolicyOperation:
                    return -45;

                // Reorder policies depend on hypertables
                case DropReorderPolicyOperation:
                    return -20;

                case CreateHypertableOperation:
                    return 10;
                case AlterHypertableOperation:
                    return 15;

                case AddReorderPolicyOperation:
                case AlterReorderPolicyOperation:
                    return 20;

                case CreateContinuousAggregateOperation:
                    return 30;
                case AlterContinuousAggregateOperation:
                    return 40;

                case AddContinuousAggregatePolicyOperation:
                    return 45;

                case AddCompressionPolicyOperation:
                case AlterCompressionPolicyOperation:
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
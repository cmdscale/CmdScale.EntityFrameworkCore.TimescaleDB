using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
    public class TimescaleCSharpMigrationOperationGenerator(CSharpMigrationOperationGeneratorDependencies dependencies) : CSharpMigrationOperationGenerator(dependencies)
    {
        protected override void Generate(MigrationOperation operation, IndentedStringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(builder);

            HypertableOperationGenerator? hypertableOperationGenerator = null;
            ReorderPolicyOperationGenerator? reorderPolicyOperationGenerator = null;
            ContinuousAggregateOperationGenerator? continuousAggregateOperationGenerator = null;
            ContinuousAggregatePolicyOperationGenerator? continuousAggregatePolicyOperationGenerator = null;

            List<string> statements;
            bool suppressTransaction = false;

            switch (operation)
            {
                case CreateHypertableOperation create:
                    hypertableOperationGenerator ??= new(isDesignTime: true);
                    statements = hypertableOperationGenerator.Generate(create);
                    break;
                case AlterHypertableOperation alter:
                    hypertableOperationGenerator ??= new(isDesignTime: true);
                    statements = hypertableOperationGenerator.Generate(alter);
                    break;

                case AddReorderPolicyOperation addReorder:
                    reorderPolicyOperationGenerator ??= new(isDesignTime: true);
                    statements = reorderPolicyOperationGenerator.Generate(addReorder);
                    break;
                case AlterReorderPolicyOperation alterReorder:
                    reorderPolicyOperationGenerator ??= new(isDesignTime: true);
                    statements = reorderPolicyOperationGenerator.Generate(alterReorder);
                    break;
                case DropReorderPolicyOperation dropReorder:
                    reorderPolicyOperationGenerator ??= new(isDesignTime: true);
                    statements = reorderPolicyOperationGenerator.Generate(dropReorder);
                    break;

                case CreateContinuousAggregateOperation createContinuousAggregate:
                    continuousAggregateOperationGenerator ??= new(isDesignTime: true);
                    statements = continuousAggregateOperationGenerator.Generate(createContinuousAggregate);
                    suppressTransaction = true;
                    break;
                case AlterContinuousAggregateOperation alterContinuousAggregate:
                    continuousAggregateOperationGenerator ??= new(isDesignTime: true);
                    statements = continuousAggregateOperationGenerator.Generate(alterContinuousAggregate);
                    break;
                case DropContinuousAggregateOperation dropContinuousAggregate:
                    continuousAggregateOperationGenerator ??= new(isDesignTime: true);
                    statements = continuousAggregateOperationGenerator.Generate(dropContinuousAggregate);
                    break;

                case AddContinuousAggregatePolicyOperation addContinuousAggregatePolicy:
                    continuousAggregatePolicyOperationGenerator ??= new(isDesignTime: true);
                    statements = continuousAggregatePolicyOperationGenerator.Generate(addContinuousAggregatePolicy);
                    break;

                case RemoveContinuousAggregatePolicyOperation removeContinuousAggregatePolicy:
                    continuousAggregatePolicyOperationGenerator ??= new(isDesignTime: true);
                    statements = continuousAggregatePolicyOperationGenerator.Generate(removeContinuousAggregatePolicy);
                    break;

                default:
                    base.Generate(operation, builder);
                    return;
            }

            // Guard: if no statements were generated, output a no-op SQL comment to maintain valid C# syntax.
            if (statements.Count == 0)
            {
                builder.Append(".Sql(@\"-- No SQL generated for this operation\")");
                return;
            }

            SqlBuilderHelper.BuildQueryString(statements, builder, suppressTransaction);
        }

    }
}

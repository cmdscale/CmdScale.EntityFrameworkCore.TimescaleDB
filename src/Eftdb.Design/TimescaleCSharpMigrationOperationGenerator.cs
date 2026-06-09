using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
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

            HypertableCSharpGenerator? hypertableCSharpGenerator = null;
            ReorderPolicyCSharpGenerator? reorderPolicyCSharpGenerator = null;
            RetentionPolicyCSharpGenerator? retentionPolicyCSharpGenerator = null;
            ContinuousAggregateCSharpGenerator? continuousAggregateCSharpGenerator = null;
            ContinuousAggregatePolicyCSharpGenerator? continuousAggregatePolicyCSharpGenerator = null;

            switch (operation)
            {
                case CreateHypertableOperation create:
                    hypertableCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    hypertableCSharpGenerator.Generate(create, builder);
                    return;
                case AlterHypertableOperation alter:
                    hypertableCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    hypertableCSharpGenerator.Generate(alter, builder);
                    return;

                case AddReorderPolicyOperation addReorder:
                    reorderPolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    reorderPolicyCSharpGenerator.Generate(addReorder, builder);
                    return;
                case AlterReorderPolicyOperation alterReorder:
                    reorderPolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    reorderPolicyCSharpGenerator.Generate(alterReorder, builder);
                    return;
                case DropReorderPolicyOperation dropReorder:
                    reorderPolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    reorderPolicyCSharpGenerator.Generate(dropReorder, builder);
                    return;

                case AddRetentionPolicyOperation addRetention:
                    retentionPolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    retentionPolicyCSharpGenerator.Generate(addRetention, builder);
                    return;
                case AlterRetentionPolicyOperation alterRetention:
                    retentionPolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    retentionPolicyCSharpGenerator.Generate(alterRetention, builder);
                    return;
                case DropRetentionPolicyOperation dropRetention:
                    retentionPolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    retentionPolicyCSharpGenerator.Generate(dropRetention, builder);
                    return;

                case CreateContinuousAggregateOperation createContinuousAggregate:
                    continuousAggregateCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    continuousAggregateCSharpGenerator.Generate(createContinuousAggregate, builder);
                    return;
                case AlterContinuousAggregateOperation alterContinuousAggregate:
                    continuousAggregateCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    continuousAggregateCSharpGenerator.Generate(alterContinuousAggregate, builder);
                    return;
                case DropContinuousAggregateOperation dropContinuousAggregate:
                    continuousAggregateCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    continuousAggregateCSharpGenerator.Generate(dropContinuousAggregate, builder);
                    return;

                case AddContinuousAggregatePolicyOperation addContinuousAggregatePolicy:
                    continuousAggregatePolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    continuousAggregatePolicyCSharpGenerator.Generate(addContinuousAggregatePolicy, builder);
                    return;

                case RemoveContinuousAggregatePolicyOperation removeContinuousAggregatePolicy:
                    continuousAggregatePolicyCSharpGenerator ??= new(Dependencies.CSharpHelper);
                    continuousAggregatePolicyCSharpGenerator.Generate(removeContinuousAggregatePolicy, builder);
                    return;

                default:
                    base.Generate(operation, builder);
                    return;
            }
        }
    }
}

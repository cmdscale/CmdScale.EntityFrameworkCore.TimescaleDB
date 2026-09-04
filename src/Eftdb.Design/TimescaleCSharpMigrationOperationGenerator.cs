using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregatePolicy;
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

            switch (operation)
            {
                case CreateHypertableOperation create:
                    new HypertableCSharpGenerator(Dependencies.CSharpHelper).Generate(create, builder);
                    return;
                case AlterHypertableOperation alter:
                    new HypertableCSharpGenerator(Dependencies.CSharpHelper).Generate(alter, builder);
                    return;

                case AddReorderPolicyOperation addReorder:
                    new ReorderPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(addReorder, builder);
                    return;
                case AlterReorderPolicyOperation alterReorder:
                    new ReorderPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(alterReorder, builder);
                    return;
                case DropReorderPolicyOperation dropReorder:
                    new ReorderPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(dropReorder, builder);
                    return;

                case AddRetentionPolicyOperation addRetention:
                    new RetentionPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(addRetention, builder);
                    return;
                case AlterRetentionPolicyOperation alterRetention:
                    new RetentionPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(alterRetention, builder);
                    return;
                case DropRetentionPolicyOperation dropRetention:
                    new RetentionPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(dropRetention, builder);
                    return;

                case AddCompressionPolicyOperation addCompression:
                    new CompressionPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(addCompression, builder);
                    return;
                case AlterCompressionPolicyOperation alterCompression:
                    new CompressionPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(alterCompression, builder);
                    return;
                case DropCompressionPolicyOperation dropCompression:
                    new CompressionPolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(dropCompression, builder);
                    return;

                case CreateContinuousAggregateOperation createContinuousAggregate:
                    new ContinuousAggregateCSharpGenerator(Dependencies.CSharpHelper).Generate(createContinuousAggregate, builder);
                    return;
                case AlterContinuousAggregateOperation alterContinuousAggregate:
                    new ContinuousAggregateCSharpGenerator(Dependencies.CSharpHelper).Generate(alterContinuousAggregate, builder);
                    return;
                case DropContinuousAggregateOperation dropContinuousAggregate:
                    new ContinuousAggregateCSharpGenerator(Dependencies.CSharpHelper).Generate(dropContinuousAggregate, builder);
                    return;

                case AddContinuousAggregatePolicyOperation addContinuousAggregatePolicy:
                    new ContinuousAggregatePolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(addContinuousAggregatePolicy, builder);
                    return;

                case RemoveContinuousAggregatePolicyOperation removeContinuousAggregatePolicy:
                    new ContinuousAggregatePolicyCSharpGenerator(Dependencies.CSharpHelper).Generate(removeContinuousAggregatePolicy, builder);
                    return;

                default:
                    base.Generate(operation, builder);
                    return;
            }
        }
    }
}

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
            List<string> statements = [];

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

                default:
                    base.Generate(operation, builder);
                    break;
            }

            SqlBuilderHelper.BuildQueryString(statements, builder);            
        }

    }
}

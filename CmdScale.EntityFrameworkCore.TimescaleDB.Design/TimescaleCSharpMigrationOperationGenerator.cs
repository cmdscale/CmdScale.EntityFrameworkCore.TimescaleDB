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

            switch (operation)
            {
                case CreateHypertableOperation create:
                    HypertableOperationGenerator.Generate(create, builder);
                    break;
                case AlterHypertableOperation alter:
                    HypertableOperationGenerator.Generate(alter, builder);
                    break;

                case AddReorderPolicyOperation addReorder:
                    ReorderPolicyOperationGenerator.Generate(addReorder, builder);
                    break;
                case AlterReorderPolicyOperation alterReorder:
                    ReorderPolicyOperationGenerator.Generate(alterReorder, builder);
                    break;
                case DropReorderPolicyOperation dropReorder:
                    ReorderPolicyOperationGenerator.Generate(dropReorder, builder);
                    break;

            default:
                    base.Generate(operation, builder);
                    break;
            }
        }
        
    }
}
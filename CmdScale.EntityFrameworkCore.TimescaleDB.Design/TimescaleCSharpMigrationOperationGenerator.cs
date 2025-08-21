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
                    Generate(create, builder);
                    break;
                    
                default:
                    base.Generate(operation, builder);
                    break;
            }
        }

        private static void Generate(CreateHypertableOperation operation, IndentedStringBuilder builder)
        {
            builder.Append($".Sql(\"SELECT create_hypertable('\\\"{operation.TableName}\\\"', '{operation.TimeColumnName}');\");");
        }
    }
}
using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
#pragma warning disable EF1001
    public class TimescaleDbMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, INpgsqlSingletonOptions npgsqlSingletonOptions) : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
    {
        protected override void Generate(
            MigrationOperation operation,
            IModel? model,
            MigrationCommandListBuilder builder)
        {
            List<string> statements;
            HypertableOperationGenerator? hypertableOperationGenerator = null;
            ReorderPolicyOperationGenerator? reorderPolicyOperationGenerator = null;

            switch (operation)
            {
                case CreateHypertableOperation hypertableOperation:
                    hypertableOperationGenerator ??= new(isDesignTime: false);
                    statements = hypertableOperationGenerator.Generate(hypertableOperation);
                    break;

                case AlterHypertableOperation alterHypertableOperation:
                    hypertableOperationGenerator ??= new(isDesignTime: false);
                    statements = hypertableOperationGenerator.Generate(alterHypertableOperation);
                    break;

                case AlterReorderPolicyOperation alterReorderPolicyOperation:
                    reorderPolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = reorderPolicyOperationGenerator.Generate(alterReorderPolicyOperation);
                    break;

                case AddReorderPolicyOperation addReorderPolicyOperation:
                    reorderPolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = reorderPolicyOperationGenerator.Generate(addReorderPolicyOperation);
                    break;

                case DropReorderPolicyOperation dropReorderPolicyOperation:
                    reorderPolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = reorderPolicyOperationGenerator.Generate(dropReorderPolicyOperation);
                    break;

                default:
                    base.Generate(operation, model, builder);
                    return;
            }

            SqlBuilderHelper.BuildQueryString(statements, builder);

        }
    }
#pragma warning disable IDE0079
}


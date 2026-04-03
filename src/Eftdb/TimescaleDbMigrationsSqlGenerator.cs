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
            RetentionPolicyOperationGenerator? retentionPolicyOperationGenerator = null;
            ContinuousAggregateOperationGenerator? continuousAggregateOperationGenerator = null;
            ContinuousAggregatePolicyOperationGenerator? continuousAggregatePolicyOperationGenerator = null;
            bool suppressTransaction = false;

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

                case AddRetentionPolicyOperation addRetentionPolicyOperation:
                    retentionPolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = retentionPolicyOperationGenerator.Generate(addRetentionPolicyOperation);
                    break;

                case AlterRetentionPolicyOperation alterRetentionPolicyOperation:
                    retentionPolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = retentionPolicyOperationGenerator.Generate(alterRetentionPolicyOperation);
                    break;

                case DropRetentionPolicyOperation dropRetentionPolicyOperation:
                    retentionPolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = retentionPolicyOperationGenerator.Generate(dropRetentionPolicyOperation);
                    break;

                case CreateContinuousAggregateOperation createContinuousAggregateOperation:
                    continuousAggregateOperationGenerator ??= new(isDesignTime: false);
                    statements = continuousAggregateOperationGenerator.Generate(createContinuousAggregateOperation);
                    suppressTransaction = true;
                    break;

                case AlterContinuousAggregateOperation alterContinuousAggregateOperation:
                    continuousAggregateOperationGenerator ??= new(isDesignTime: false);
                    statements = continuousAggregateOperationGenerator.Generate(alterContinuousAggregateOperation);
                    break;

                case DropContinuousAggregateOperation dropContinuousAggregateOperation:
                    continuousAggregateOperationGenerator ??= new(isDesignTime: false);
                    statements = continuousAggregateOperationGenerator.Generate(dropContinuousAggregateOperation);
                    break;

                case AddContinuousAggregatePolicyOperation addContinuousAggregatePolicyOperation:
                    continuousAggregatePolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = continuousAggregatePolicyOperationGenerator.Generate(addContinuousAggregatePolicyOperation);
                    break;

                case RemoveContinuousAggregatePolicyOperation removeContinuousAggregatePolicyOperation:
                    continuousAggregatePolicyOperationGenerator ??= new(isDesignTime: false);
                    statements = continuousAggregatePolicyOperationGenerator.Generate(removeContinuousAggregatePolicyOperation);
                    break;

                default:
                    base.Generate(operation, model, builder);
                    return;
            }

            bool usePerform = Options.HasFlag(MigrationsSqlGenerationOptions.Idempotent);
            SqlBuilderHelper.BuildQueryString(statements, builder, suppressTransaction, usePerform);

        }

        /// <summary>
        /// Handles raw SQL operations from migration files (migrationBuilder.Sql calls).
        /// In idempotent mode, replaces SELECT with PERFORM because the SQL is wrapped
        /// in a PL/pgSQL DO block where bare SELECT fails with "query has no destination for result data".
        /// Skips replacement for DDL statements (CREATE, ALTER, DROP) where SELECT is part of the syntax.
        /// </summary>
        protected override void Generate(SqlOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            if (Options.HasFlag(MigrationsSqlGenerationOptions.Idempotent)
                && !IsDdlStatement(operation.Sql))
            {
                string sql = SqlBuilderHelper.ReplaceSelectWithPerformMultiLine(operation.Sql);
                builder.Append(sql);
                builder.EndCommand(suppressTransaction: operation.SuppressTransaction);
                return;
            }

            base.Generate(operation, model, builder);
        }

        private static bool IsDdlStatement(string sql)
        {
            string trimmed = sql.TrimStart();
            return trimmed.StartsWith("CREATE ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("ALTER ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("DROP ", StringComparison.OrdinalIgnoreCase);
        }
    }
#pragma warning disable IDE0079
}


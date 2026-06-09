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
            bool suppressTransaction = false;

            switch (operation)
            {
                case CreateHypertableOperation hypertableOperation:
                    statements = HypertableSqlGenerator.Generate(hypertableOperation);
                    break;

                case AlterHypertableOperation alterHypertableOperation:
                    statements = HypertableSqlGenerator.Generate(alterHypertableOperation);
                    break;

                case AlterReorderPolicyOperation alterReorderPolicyOperation:
                    statements = ReorderPolicySqlGenerator.Generate(alterReorderPolicyOperation);
                    break;

                case AddReorderPolicyOperation addReorderPolicyOperation:
                    statements = ReorderPolicySqlGenerator.Generate(addReorderPolicyOperation);
                    break;

                case DropReorderPolicyOperation dropReorderPolicyOperation:
                    statements = ReorderPolicySqlGenerator.Generate(dropReorderPolicyOperation);
                    break;

                case AddRetentionPolicyOperation addRetentionPolicyOperation:
                    statements = RetentionPolicySqlGenerator.Generate(addRetentionPolicyOperation);
                    break;

                case AlterRetentionPolicyOperation alterRetentionPolicyOperation:
                    statements = RetentionPolicySqlGenerator.Generate(alterRetentionPolicyOperation);
                    break;

                case DropRetentionPolicyOperation dropRetentionPolicyOperation:
                    statements = RetentionPolicySqlGenerator.Generate(dropRetentionPolicyOperation);
                    break;

                case CreateContinuousAggregateOperation createContinuousAggregateOperation:
                    statements = ContinuousAggregateSqlGenerator.Generate(createContinuousAggregateOperation);
                    suppressTransaction = true;
                    break;

                case AlterContinuousAggregateOperation alterContinuousAggregateOperation:
                    statements = ContinuousAggregateSqlGenerator.Generate(alterContinuousAggregateOperation);
                    break;

                case DropContinuousAggregateOperation dropContinuousAggregateOperation:
                    statements = ContinuousAggregateSqlGenerator.Generate(dropContinuousAggregateOperation);
                    break;

                case AddContinuousAggregatePolicyOperation addContinuousAggregatePolicyOperation:
                    statements = ContinuousAggregatePolicySqlGenerator.Generate(addContinuousAggregatePolicyOperation);
                    break;

                case RemoveContinuousAggregatePolicyOperation removeContinuousAggregatePolicyOperation:
                    statements = ContinuousAggregatePolicySqlGenerator.Generate(removeContinuousAggregatePolicyOperation);
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


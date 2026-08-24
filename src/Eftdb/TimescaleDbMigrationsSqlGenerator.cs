using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
#pragma warning disable EF1001
    internal class TimescaleDbMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        INpgsqlSingletonOptions npgsqlSingletonOptions,
        TimescaleDbOptions? timescaleDbOptions = null,
        IDiagnosticsLogger<DbLoggerCategory.Migrations>? migrationsLogger = null) : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
    {
        private static readonly string SkipCommentPrefix = SqlBuilderHelper.SkipComment("Skipping Community Edition feature");

        private readonly bool _useLegacyCompressionNames = timescaleDbOptions?.UseLegacyCompressionNames ?? false;
        private readonly bool _isApacheEdition = timescaleDbOptions?.IsApacheEdition ?? false;

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
                    statements = HypertableSqlGenerator.Generate(hypertableOperation, _useLegacyCompressionNames, _isApacheEdition);
                    break;

                case AlterHypertableOperation alterHypertableOperation:
                    statements = HypertableSqlGenerator.Generate(alterHypertableOperation, _useLegacyCompressionNames, _isApacheEdition);
                    break;

                case AlterReorderPolicyOperation alterReorderPolicyOperation:
                    statements = ReorderPolicySqlGenerator.Generate(alterReorderPolicyOperation, _isApacheEdition);
                    break;

                case AddReorderPolicyOperation addReorderPolicyOperation:
                    statements = ReorderPolicySqlGenerator.Generate(addReorderPolicyOperation, _isApacheEdition);
                    break;

                case DropReorderPolicyOperation dropReorderPolicyOperation:
                    statements = ReorderPolicySqlGenerator.Generate(dropReorderPolicyOperation, _isApacheEdition);
                    break;

                case AddRetentionPolicyOperation addRetentionPolicyOperation:
                    statements = RetentionPolicySqlGenerator.Generate(addRetentionPolicyOperation, _isApacheEdition);
                    break;

                case AlterRetentionPolicyOperation alterRetentionPolicyOperation:
                    statements = RetentionPolicySqlGenerator.Generate(alterRetentionPolicyOperation, _isApacheEdition);
                    break;

                case DropRetentionPolicyOperation dropRetentionPolicyOperation:
                    statements = RetentionPolicySqlGenerator.Generate(dropRetentionPolicyOperation, _isApacheEdition);
                    break;

                case AddCompressionPolicyOperation addCompressionPolicyOperation:
                    statements = CompressionPolicySqlGenerator.Generate(addCompressionPolicyOperation, _useLegacyCompressionNames, _isApacheEdition);
                    break;

                case AlterCompressionPolicyOperation alterCompressionPolicyOperation:
                    statements = CompressionPolicySqlGenerator.Generate(alterCompressionPolicyOperation, _useLegacyCompressionNames, _isApacheEdition);
                    break;

                case DropCompressionPolicyOperation dropCompressionPolicyOperation:
                    statements = CompressionPolicySqlGenerator.Generate(dropCompressionPolicyOperation, _useLegacyCompressionNames, _isApacheEdition);
                    break;

                case CreateContinuousAggregateOperation createContinuousAggregateOperation:
                    statements = ContinuousAggregateSqlGenerator.Generate(createContinuousAggregateOperation, _useLegacyCompressionNames, _isApacheEdition);
                    suppressTransaction = !_isApacheEdition;
                    break;

                case AlterContinuousAggregateOperation alterContinuousAggregateOperation:
                    statements = ContinuousAggregateSqlGenerator.Generate(alterContinuousAggregateOperation, _useLegacyCompressionNames, _isApacheEdition);
                    break;

                case DropContinuousAggregateOperation dropContinuousAggregateOperation:
                    statements = ContinuousAggregateSqlGenerator.Generate(dropContinuousAggregateOperation);
                    break;

                case AddContinuousAggregatePolicyOperation addContinuousAggregatePolicyOperation:
                    statements = ContinuousAggregatePolicySqlGenerator.Generate(addContinuousAggregatePolicyOperation, _isApacheEdition);
                    break;

                case RemoveContinuousAggregatePolicyOperation removeContinuousAggregatePolicyOperation:
                    statements = ContinuousAggregatePolicySqlGenerator.Generate(removeContinuousAggregatePolicyOperation, _isApacheEdition);
                    break;

                default:
                    base.Generate(operation, model, builder);
                    return;
            }

            LogSkippedCommunityFeatures(statements);

            bool usePerform = Options.HasFlag(MigrationsSqlGenerationOptions.Idempotent);
            SqlBuilderHelper.BuildQueryString(statements, builder, suppressTransaction, usePerform);

        }

        /// <summary>
        /// Surfaces Apache-edition skip comments as generation-time warnings so skipped
        /// Community-only features are visible in logs, not only in scripted SQL output.
        /// </summary>
        private void LogSkippedCommunityFeatures(List<string> statements)
        {
            if (!_isApacheEdition)
            {
                return;
            }

            ILogger logger = migrationsLogger?.Logger ?? Dependencies.Logger.Logger;
            foreach (string statement in statements)
            {
                if (statement.StartsWith(SkipCommentPrefix, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "{SkippedCommunityFeature}",
                        statement[SqlBuilderHelper.SkipCommentMarker.Length..]);
                }
            }
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


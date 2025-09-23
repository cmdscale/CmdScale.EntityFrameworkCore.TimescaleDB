using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Globalization;

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
                case AlterHypertableOperation alter:
                    Generate(alter, builder);
                    break;
                case AddReorderPolicyOperation addReorder:
                    Generate(addReorder, builder);
                    break;
                case AlterReorderPolicyOperation alterReorder:
                    Generate(alterReorder, builder);
                    break;
                case DropReorderPolicyOperation dropReorder:
                    Generate(dropReorder, builder);
                    break;

                default:
                    base.Generate(operation, builder);
                    break;
            }
        }

        private static void Generate(CreateHypertableOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements =
            [
                $"SELECT create_hypertable('\"\"{operation.TableName}\"\"', '{operation.TimeColumnName}');"
            ];

            // ChunkTimeInterval
            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
            {
                // Check if the interval is a plain number (e.g., for microseconds).
                if (long.TryParse(operation.ChunkTimeInterval, out _))
                {
                    // If it's a number, don't wrap it in quotes.
                    statements.Add($"SELECT set_chunk_time_interval('\"\"{operation.TableName}\"\"', {operation.ChunkTimeInterval}::bigint);");
                }
                else
                {
                    // If it's a string like '7 days', wrap it in quotes.
                    statements.Add($"SELECT set_chunk_time_interval('\"\"{operation.TableName}\"\"', INTERVAL '{operation.ChunkTimeInterval}');");
                }
            }

            // EnableCompression
            if (operation.EnableCompression)
            {
                statements.Add($"ALTER TABLE \"\"{operation.TableName}\"\" SET (timescaledb.compress = true);");
            }

            // ChunkSkipColumns
            if (operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0)
            {
                statements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in operation.ChunkSkipColumns)
                {
                    statements.Add($"SELECT enable_chunk_skipping('\"\"{operation.TableName}\"\"', '{column}');");
                }
            }

            // AdditionalDimensions
            if (operation.AdditionalDimensions != null && operation.AdditionalDimensions.Count > 0)
            {
                foreach (Dimension dimension in operation.AdditionalDimensions)
                {
                    if (dimension.Type == EDimensionType.Range)
                    {
                        statements.Add($"SELECT add_dimension('\"\"{operation.TableName}\"\"', by_range('{dimension.ColumnName}', INTERVAL '{dimension.Interval}'));");
                    }
                    else if (dimension.Type == EDimensionType.Hash)
                    {
                        statements.Add($"SELECT add_dimension('\"\"{operation.TableName}\"\"', by_hash('{dimension.ColumnName}', {dimension.NumberOfPartitions}));");
                    }
                }
            }

            BuildQueryString(statements, builder);
        }

        private static void Generate(AlterHypertableOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements = [];

            // Check for ChunkTimeInterval change
            if (operation.ChunkTimeInterval != operation.OldChunkTimeInterval)
            {
                if (operation.ChunkTimeInterval != operation.OldChunkTimeInterval)
                {
                    // Check if the interval is a plain number (e.g., for microseconds).
                    if (long.TryParse(operation.ChunkTimeInterval, out _))
                    {
                        // If it's a number, don't wrap it in quotes.
                        statements.Add($"SELECT set_chunk_time_interval('\"\"{operation.TableName}\"\"', {operation.ChunkTimeInterval}::bigint);");
                    }
                    else
                    {
                        // If it's a string like '7 days', wrap it in quotes.
                        statements.Add($"SELECT set_chunk_time_interval('\"\"{operation.TableName}\"\"', INTERVAL '{operation.ChunkTimeInterval}');");
                    }
                }
            }

            // Check for EnableCompression change
            if (operation.EnableCompression != operation.OldEnableCompression)
            {
                string compressionValue = operation.EnableCompression.ToString().ToLower();
                statements.Add($"ALTER TABLE \"\"{operation.TableName}\"\" SET (timescaledb.compress = {compressionValue});");
            }

            // Handle ChunkSkipColumns
            IReadOnlyList<string> newColumns = operation.ChunkSkipColumns ?? [];
            IReadOnlyList<string> oldColumns = operation.OldChunkSkipColumns ?? [];
            List<string> addedColumns = [.. newColumns.Except(oldColumns)];

            if (addedColumns.Count != 0)
            {
                statements.Add("SET timescaledb.enable_chunk_skipping = 'ON';");

                foreach (string column in addedColumns)
                {
                    statements.Add($"SELECT enable_chunk_skipping('\"\"{operation.TableName}\"\"', '{column}');");
                }
            }

            List<string> removedColumns = [.. oldColumns.Except(newColumns)];
            if (removedColumns.Count != 0)
            {
                foreach (string column in removedColumns)
                {
                    statements.Add($"SELECT disable_chunk_skipping('\"\"{operation.TableName}\"\"', '{column}');");
                }
            }

            BuildQueryString(statements, builder);
        }

        private static void Generate(AddReorderPolicyOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements =
            [
                BuildAddReorderPolicySql(operation.TableName, operation.IndexName, operation.InitialStart)
            ];

            BuildQueryString(statements, builder);
        }

        private static void Generate(AlterReorderPolicyOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements =
            [
                // Drop the existing policy
                $"SELECT remove_reorder_policy('\"\"{operation.TableName}\"\"', if_exists => true);",

                // Add the new policy with the updated settings
                BuildAddReorderPolicySql(operation.TableName, operation.IndexName, operation.InitialStart)
            ];
            BuildQueryString(statements, builder);
        }

        private static void Generate(DropReorderPolicyOperation operation, IndentedStringBuilder builder)
        {
            List<string> statements =
            [
                $"SELECT remove_reorder_policy('\"\"{operation.TableName}\"\"', if_exists => true);"
            ];
            BuildQueryString(statements, builder);
        }

        private static string BuildAddReorderPolicySql(string tableName, string indexName, DateTime? initialStart)
        {
            string baseSql = $"SELECT add_reorder_policy('\"\"{tableName}\"\"', '{indexName}'";

            List<string> optionalArgs = [];

            // Add optional arguments if they are provided
            if (initialStart.HasValue)
            {
                // Use ISO 8601 format for timestamps to avoid ambiguity
                string timestamp = initialStart.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                optionalArgs.Add($"initial_start => '{timestamp}'");
            }

            if (optionalArgs.Count > 0)
            {
                baseSql += $", {string.Join(", ", optionalArgs)}";
            }

            baseSql += ");";
            return baseSql;
        }

        private static void BuildQueryString(List<string> statements, IndentedStringBuilder builder)
        {
            if (statements.Count > 0)
            {
                builder.AppendLine(".Sql(@\"");
                using (builder.Indent())
                {
                    foreach (string statement in statements)
                    {
                        builder.AppendLine(statement);
                    }
                }
                builder.Append("\")");
            }
        }
    }
}
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
                case AlterHypertableOperation alter:
                    Generate(alter, builder);
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
                statements.Add($"SELECT set_chunk_time_interval('\"\"{operation.TableName}\"\"', {operation.ChunkTimeInterval});");
            }

            // EnableCompression
            if (operation.EnableCompression)
            {
                statements.Add($"ALTER TABLE \"\"{operation.TableName}\"\" SET (timescaledb.compress = true);");
            }

            // ChunkSkipColumns
            if (operation.ChunkSkipColumns != null && operation.ChunkSkipColumns.Count > 0)
            {
                foreach (string column in operation.ChunkSkipColumns)
                {
                    statements.Add($"SELECT enable_chunk_skipping('\"\"{operation.TableName}\"\"', '{column}');");
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
                    statements.Add($"SELECT set_chunk_time_interval('\"\"{operation.TableName}\"\"', {operation.ChunkTimeInterval});");
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
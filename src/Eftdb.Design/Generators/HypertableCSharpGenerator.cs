using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Emits typed migrationBuilder C# calls into a migration file.
    /// </summary>
    public class HypertableCSharpGenerator(ICSharpHelper code)
    {
        private readonly ICSharpHelper code = code;

        public void Generate(CreateHypertableOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "CreateHypertable");

            call.Arg("tableName", code.Literal(operation.TableName));
            call.Arg("timeColumnName", code.Literal(operation.TimeColumnName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
                call.Arg("chunkTimeInterval", code.Literal(operation.ChunkTimeInterval));

            if (operation.EnableCompression)
                call.Arg("enableCompression", code.Literal(true));

            if (operation.MigrateData)
                call.Arg("migrateData", code.Literal(true));

            if (operation.ChunkSkipColumns is { Count: > 0 })
                call.Arg("chunkSkipColumns", CSharpGeneratorHelper.LiteralStringList(code, operation.ChunkSkipColumns));

            if (operation.AdditionalDimensions is { Count: > 0 })
                call.Arg("additionalDimensions", b => AppendDimensionList(b, operation.AdditionalDimensions));

            if (operation.CompressionSegmentBy is { Count: > 0 })
                call.Arg("compressionSegmentBy", CSharpGeneratorHelper.LiteralStringList(code, operation.CompressionSegmentBy));

            if (operation.CompressionOrderBy is { Count: > 0 })
                call.Arg("compressionOrderBy", CSharpGeneratorHelper.LiteralStringList(code, operation.CompressionOrderBy));
        }

        public void Generate(AlterHypertableOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AlterHypertable");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.ChunkTimeInterval))
                call.Arg("chunkTimeInterval", code.Literal(operation.ChunkTimeInterval));

            if (operation.EnableCompression)
                call.Arg("enableCompression", code.Literal(true));

            if (operation.ChunkSkipColumns is { Count: > 0 })
                call.Arg("chunkSkipColumns", CSharpGeneratorHelper.LiteralStringList(code, operation.ChunkSkipColumns));

            if (operation.AdditionalDimensions is { Count: > 0 })
                call.Arg("additionalDimensions", b => AppendDimensionList(b, operation.AdditionalDimensions));

            if (operation.CompressionSegmentBy is { Count: > 0 })
                call.Arg("compressionSegmentBy", CSharpGeneratorHelper.LiteralStringList(code, operation.CompressionSegmentBy));

            if (operation.CompressionOrderBy is { Count: > 0 })
                call.Arg("compressionOrderBy", CSharpGeneratorHelper.LiteralStringList(code, operation.CompressionOrderBy));

            // Old* values — emitted for Down() reversibility, only when non-default.
            if (!string.IsNullOrEmpty(operation.OldChunkTimeInterval))
                call.Arg("oldChunkTimeInterval", code.Literal(operation.OldChunkTimeInterval));

            if (operation.OldEnableCompression)
                call.Arg("oldEnableCompression", code.Literal(true));

            if (operation.OldChunkSkipColumns is { Count: > 0 })
                call.Arg("oldChunkSkipColumns", CSharpGeneratorHelper.LiteralStringList(code, operation.OldChunkSkipColumns));

            if (operation.OldAdditionalDimensions is { Count: > 0 })
                call.Arg("oldAdditionalDimensions", b => AppendDimensionList(b, operation.OldAdditionalDimensions));

            if (operation.OldCompressionSegmentBy is { Count: > 0 })
                call.Arg("oldCompressionSegmentBy", CSharpGeneratorHelper.LiteralStringList(code, operation.OldCompressionSegmentBy));

            if (operation.OldCompressionOrderBy is { Count: > 0 })
                call.Arg("oldCompressionOrderBy", CSharpGeneratorHelper.LiteralStringList(code, operation.OldCompressionOrderBy));
        }

        // Writes the dimension list directly into the builder so each entry is on its own
        // line at the correct indent level.
        private void AppendDimensionList(IndentedStringBuilder builder, IReadOnlyList<Dimension> dimensions)
        {
            // Migration files only import Microsoft.EntityFrameworkCore.Migrations.
            // Fully qualify Dimension so generated code compiles without extra usings.
            string typeRef = typeof(Dimension).FullName!;

            builder.AppendLine("[");
            using (builder.Indent())
            {
                for (int i = 0; i < dimensions.Count; i++)
                {
                    Dimension d = dimensions[i];
                    string entry = d.Type == EDimensionType.Hash
                        ? CSharpGeneratorHelper.StaticCall(code, typeRef, nameof(Dimension.CreateHash), d.ColumnName, d.NumberOfPartitions ?? 0)
                        : CSharpGeneratorHelper.StaticCall(code, typeRef, nameof(Dimension.CreateRange), d.ColumnName, d.Interval ?? string.Empty);

                    builder.AppendLine(i < dimensions.Count - 1 ? entry + "," : entry);
                }
            }
            builder.Append("]");
        }
    }
}

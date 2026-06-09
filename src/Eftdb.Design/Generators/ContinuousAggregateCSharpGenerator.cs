using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Emits typed migrationBuilder C# calls into a migration file.
    /// </summary>
    public class ContinuousAggregateCSharpGenerator(ICSharpHelper code)
    {
        private readonly ICSharpHelper code = code;

        public void Generate(CreateContinuousAggregateOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "CreateContinuousAggregate");

            call.Arg("materializedViewName", code.Literal(operation.MaterializedViewName));
            call.Arg("parentName", code.Literal(operation.ParentName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.ChunkInterval))
                call.Arg("chunkInterval", code.Literal(operation.ChunkInterval));

            if (operation.WithNoData)
                call.Arg("withNoData", code.Literal(true));

            if (operation.CreateGroupIndexes)
                call.Arg("createGroupIndexes", code.Literal(true));

            if (operation.MaterializedOnly)
                call.Arg("materializedOnly", code.Literal(true));

            if (!string.IsNullOrEmpty(operation.TimeBucketWidth))
                call.Arg("timeBucketWidth", code.Literal(operation.TimeBucketWidth));

            if (!string.IsNullOrEmpty(operation.TimeBucketSourceColumn))
                call.Arg("timeBucketSourceColumn", code.Literal(operation.TimeBucketSourceColumn));

            // timeBucketGroupBy defaults to true — only emit when explicitly disabled.
            if (!operation.TimeBucketGroupBy)
                call.Arg("timeBucketGroupBy", code.Literal(false));

            if (operation.AggregateFunctions is { Count: > 0 })
                call.Arg("aggregateFunctions", b => AppendAggregateFunctionList(b, operation.AggregateFunctions));

            if (operation.GroupByColumns is { Count: > 0 })
                call.Arg("groupByColumns", CSharpGeneratorHelper.LiteralStringList(code, operation.GroupByColumns));

            if (!string.IsNullOrEmpty(operation.WhereClause))
                call.Arg("whereClause", code.Literal(operation.WhereClause));

            if (!string.IsNullOrEmpty(operation.ViewDefinition))
                call.Arg("viewDefinition", code.Literal(operation.ViewDefinition));
        }

        public void Generate(AlterContinuousAggregateOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AlterContinuousAggregate");

            call.Arg("materializedViewName", code.Literal(operation.MaterializedViewName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.ChunkInterval))
                call.Arg("chunkInterval", code.Literal(operation.ChunkInterval));

            if (operation.CreateGroupIndexes)
                call.Arg("createGroupIndexes", code.Literal(true));

            if (operation.MaterializedOnly)
                call.Arg("materializedOnly", code.Literal(true));

            // Old* values — emitted for Down() reversibility, only when non-default.
            if (!string.IsNullOrEmpty(operation.OldChunkInterval))
                call.Arg("oldChunkInterval", code.Literal(operation.OldChunkInterval));

            if (operation.OldCreateGroupIndexes)
                call.Arg("oldCreateGroupIndexes", code.Literal(true));

            if (operation.OldMaterializedOnly)
                call.Arg("oldMaterializedOnly", code.Literal(true));
        }

        public void Generate(DropContinuousAggregateOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "DropContinuousAggregate");

            call.Arg("materializedViewName", code.Literal(operation.MaterializedViewName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));
        }

        // Writes the aggregate functions as a collection expression
        private void AppendAggregateFunctionList(IndentedStringBuilder builder, IReadOnlyList<string> aggregateFunctions)
        {
            string typeRef = typeof(ContinuousAggregateFunction).FullName!;
            string enumRef = typeof(EAggregateFunction).FullName!;

            List<string> entries = [];
            foreach (string aggregateFunction in aggregateFunctions)
            {
                string[] parts = aggregateFunction.Split(':');
                if (parts.Length != 3)
                {
                    continue;
                }

                entries.Add($"new {typeRef}({code.Literal(parts[0])}, {enumRef}.{parts[1]}, {code.Literal(parts[2])})");
            }

            builder.AppendLine("[");
            using (builder.Indent())
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    builder.AppendLine(i < entries.Count - 1 ? entries[i] + "," : entries[i]);
                }
            }
            builder.Append("]");
        }
    }
}

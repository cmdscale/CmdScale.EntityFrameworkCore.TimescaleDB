using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ContinuousAggregatePolicy
{
    /// <summary>
    /// Emits typed migrationBuilder C# calls into a migration file.
    /// </summary>
    internal class ContinuousAggregatePolicyCSharpGenerator(ICSharpHelper code)
    {
        private readonly ICSharpHelper code = code;

        public void Generate(AddContinuousAggregatePolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AddContinuousAggregatePolicy");

            call.Arg("materializedViewName", code.Literal(operation.MaterializedViewName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.StartOffset))
                call.Arg("startOffset", code.Literal(operation.StartOffset));

            if (!string.IsNullOrEmpty(operation.EndOffset))
                call.Arg("endOffset", code.Literal(operation.EndOffset));

            if (!string.IsNullOrEmpty(operation.ScheduleInterval))
                call.Arg("scheduleInterval", code.Literal(operation.ScheduleInterval));

            if (operation.InitialStart.HasValue)
                call.Arg("initialStart", code.Literal(operation.InitialStart.Value));

            if (operation.IfNotExists)
                call.Arg("ifNotExists", code.Literal(true));

            if (operation.IncludeTieredData.HasValue)
                call.Arg("includeTieredData", code.Literal(operation.IncludeTieredData.Value));

            if (operation.BucketsPerBatch != 1)
                call.Arg("bucketsPerBatch", code.Literal(operation.BucketsPerBatch));

            if (operation.MaxBatchesPerExecution != 0)
                call.Arg("maxBatchesPerExecution", code.Literal(operation.MaxBatchesPerExecution));

            // refreshNewestFirst defaults to true — only emit when explicitly disabled.
            if (!operation.RefreshNewestFirst)
                call.Arg("refreshNewestFirst", code.Literal(false));
        }

        public void Generate(RemoveContinuousAggregatePolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "RemoveContinuousAggregatePolicy");

            call.Arg("materializedViewName", code.Literal(operation.MaterializedViewName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (operation.IfExists)
                call.Arg("ifExists", code.Literal(true));
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.RetentionPolicy
{
    /// <summary>
    /// Emits typed migrationBuilder C# calls into a migration file.
    /// </summary>
    internal class RetentionPolicyCSharpGenerator(ICSharpHelper code)
    {
        private readonly ICSharpHelper code = code;

        public void Generate(AddRetentionPolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AddRetentionPolicy");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.DropAfter))
                call.Arg("dropAfter", code.Literal(operation.DropAfter));

            if (!string.IsNullOrEmpty(operation.DropCreatedBefore))
                call.Arg("dropCreatedBefore", code.Literal(operation.DropCreatedBefore));

            if (operation.InitialStart.HasValue)
                call.Arg("initialStart", code.Literal(operation.InitialStart.Value));

            if (!string.IsNullOrEmpty(operation.ScheduleInterval))
                call.Arg("scheduleInterval", code.Literal(operation.ScheduleInterval));

            if (!string.IsNullOrEmpty(operation.MaxRuntime))
                call.Arg("maxRuntime", code.Literal(operation.MaxRuntime));

            if (operation.MaxRetries.HasValue)
                call.Arg("maxRetries", code.Literal(operation.MaxRetries.Value));

            if (!string.IsNullOrEmpty(operation.RetryPeriod))
                call.Arg("retryPeriod", code.Literal(operation.RetryPeriod));
        }

        public void Generate(AlterRetentionPolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AlterRetentionPolicy");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.DropAfter))
                call.Arg("dropAfter", code.Literal(operation.DropAfter));

            if (!string.IsNullOrEmpty(operation.DropCreatedBefore))
                call.Arg("dropCreatedBefore", code.Literal(operation.DropCreatedBefore));

            if (operation.InitialStart.HasValue)
                call.Arg("initialStart", code.Literal(operation.InitialStart.Value));

            if (!string.IsNullOrEmpty(operation.ScheduleInterval))
                call.Arg("scheduleInterval", code.Literal(operation.ScheduleInterval));

            if (!string.IsNullOrEmpty(operation.MaxRuntime))
                call.Arg("maxRuntime", code.Literal(operation.MaxRuntime));

            if (operation.MaxRetries.HasValue)
                call.Arg("maxRetries", code.Literal(operation.MaxRetries.Value));

            if (!string.IsNullOrEmpty(operation.RetryPeriod))
                call.Arg("retryPeriod", code.Literal(operation.RetryPeriod));

            // Old* values — emitted for Down() reversibility, only when non-default.
            if (!string.IsNullOrEmpty(operation.OldDropAfter))
                call.Arg("oldDropAfter", code.Literal(operation.OldDropAfter));

            if (!string.IsNullOrEmpty(operation.OldDropCreatedBefore))
                call.Arg("oldDropCreatedBefore", code.Literal(operation.OldDropCreatedBefore));

            if (operation.OldInitialStart.HasValue)
                call.Arg("oldInitialStart", code.Literal(operation.OldInitialStart.Value));

            if (!string.IsNullOrEmpty(operation.OldScheduleInterval))
                call.Arg("oldScheduleInterval", code.Literal(operation.OldScheduleInterval));

            if (!string.IsNullOrEmpty(operation.OldMaxRuntime))
                call.Arg("oldMaxRuntime", code.Literal(operation.OldMaxRuntime));

            if (operation.OldMaxRetries.HasValue)
                call.Arg("oldMaxRetries", code.Literal(operation.OldMaxRetries.Value));

            if (!string.IsNullOrEmpty(operation.OldRetryPeriod))
                call.Arg("oldRetryPeriod", code.Literal(operation.OldRetryPeriod));
        }

        public void Generate(DropRetentionPolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "DropRetentionPolicy");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));
        }
    }
}

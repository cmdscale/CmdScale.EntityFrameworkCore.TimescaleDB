using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Emits typed migrationBuilder C# calls into a migration file.
    /// </summary>
    public class CompressionPolicyCSharpGenerator(ICSharpHelper code)
    {
        private readonly ICSharpHelper code = code;

        public void Generate(AddCompressionPolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AddCompressionPolicy");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.After))
                call.Arg("after", code.Literal(operation.After));

            if (!string.IsNullOrEmpty(operation.CreatedBefore))
                call.Arg("createdBefore", code.Literal(operation.CreatedBefore));

            if (!string.IsNullOrEmpty(operation.ScheduleInterval))
                call.Arg("scheduleInterval", code.Literal(operation.ScheduleInterval));

            if (operation.InitialStart.HasValue)
                call.Arg("initialStart", code.Literal(operation.InitialStart.Value));

            if (!string.IsNullOrEmpty(operation.Timezone))
                call.Arg("timezone", code.Literal(operation.Timezone));

            if (operation.IfNotExists.HasValue)
                call.Arg("ifNotExists", code.Literal(operation.IfNotExists.Value));
        }

        public void Generate(AlterCompressionPolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "AlterCompressionPolicy");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));

            if (!string.IsNullOrEmpty(operation.After))
                call.Arg("after", code.Literal(operation.After));

            if (!string.IsNullOrEmpty(operation.CreatedBefore))
                call.Arg("createdBefore", code.Literal(operation.CreatedBefore));

            if (!string.IsNullOrEmpty(operation.ScheduleInterval))
                call.Arg("scheduleInterval", code.Literal(operation.ScheduleInterval));

            if (operation.InitialStart.HasValue)
                call.Arg("initialStart", code.Literal(operation.InitialStart.Value));

            if (!string.IsNullOrEmpty(operation.Timezone))
                call.Arg("timezone", code.Literal(operation.Timezone));

            if (operation.IfNotExists.HasValue)
                call.Arg("ifNotExists", code.Literal(operation.IfNotExists.Value));

            if (!string.IsNullOrEmpty(operation.OldAfter))
                call.Arg("oldAfter", code.Literal(operation.OldAfter));

            if (!string.IsNullOrEmpty(operation.OldCreatedBefore))
                call.Arg("oldCreatedBefore", code.Literal(operation.OldCreatedBefore));

            if (!string.IsNullOrEmpty(operation.OldScheduleInterval))
                call.Arg("oldScheduleInterval", code.Literal(operation.OldScheduleInterval));

            if (operation.OldInitialStart.HasValue)
                call.Arg("oldInitialStart", code.Literal(operation.OldInitialStart.Value));

            if (!string.IsNullOrEmpty(operation.OldTimezone))
                call.Arg("oldTimezone", code.Literal(operation.OldTimezone));

            if (operation.OldIfNotExists.HasValue)
                call.Arg("oldIfNotExists", code.Literal(operation.OldIfNotExists.Value));
        }

        public void Generate(DropCompressionPolicyOperation operation, IndentedStringBuilder builder)
        {
            using MigrationCallWriter call = new(builder, "DropCompressionPolicy");

            call.Arg("tableName", code.Literal(operation.TableName));

            if (!string.IsNullOrEmpty(operation.Schema))
                call.Arg("schema", code.Literal(operation.Schema));
        }
    }
}

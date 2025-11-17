using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    public class ReorderPolicyOperationGeneratorTests
    {
        /// <summary>
        /// A helper to run the generator and capture its string output.
        /// </summary>
        private static string GetGeneratedCode(dynamic operation)
        {
            IndentedStringBuilder builder = new();
            ReorderPolicyOperationGenerator generator = new(true);
            List<string> statements = generator.Generate(operation);
            SqlBuilderHelper.BuildQueryString(statements, builder);
            return builder.ToString();
        }

        [Fact]
        public void Generate_Add_with_minimal_details_creates_only_add_policy_sql()
        {
            // Arrange
            AddReorderPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                IndexName = "IX_TestTable_Time"
            };

            string expected = @".Sql(@""
                SELECT add_reorder_policy('public.""""TestTable""""', 'IX_TestTable_Time');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Add_with_non_default_schedule_creates_add_and_alter_sql()
        {
            // Arrange
            DateTime testDate = new(2025, 10, 20, 12, 30, 0, DateTimeKind.Utc);
            AddReorderPolicyOperation operation = new()
            {
                Schema = "custom",
                TableName = "TestTable",
                IndexName = "IX_TestTable_Time",
                InitialStart = testDate,
                ScheduleInterval = "2 days",
                MaxRuntime = "1 hour",
                MaxRetries = 5,
                RetryPeriod = "10 minutes"
            };

            string expected = @".Sql(@""
                SELECT add_reorder_policy('custom.""""TestTable""""', 'IX_TestTable_Time', initial_start => '2025-10-20T12:30:00.0000000Z');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days', max_runtime => INTERVAL '1 hour', max_retries => 5, retry_period => INTERVAL '10 minutes')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'custom' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        // --- Tests for DropReorderPolicyOperation ---

        [Fact]
        public void Generate_Drop_creates_correct_remove_policy_sql()
        {
            // Arrange
            DropReorderPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable"
            };

            string expected = @".Sql(@""
                SELECT remove_reorder_policy('public.""""TestTable""""', if_exists => true);
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        // --- Tests for AlterReorderPolicyOperation ---

        [Fact]
        public void Generate_Alter_when_only_job_settings_change_creates_only_alter_job_sql()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                Schema = "metrics",
                TableName = "TestTable",
                // Fundamental properties are the same
                IndexName = "IX_TestTable_Time",
                OldIndexName = "IX_TestTable_Time",
                InitialStart = null,
                OldInitialStart = null,
                // Job properties have changed
                ScheduleInterval = "2 days",
                OldScheduleInterval = "1 day"
            };

            string expected = @".Sql(@""
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'metrics' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_when_fundamental_property_changes_creates_drop_and_add_sql()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                Schema = "logs",
                TableName = "TestTable",
                IndexName = "IX_New_Name",
                OldIndexName = "IX_Old_Name",
                ScheduleInterval = "2 days",
                OldScheduleInterval = "2 days"
            };

            string expected = @".Sql(@""
                SELECT remove_reorder_policy('logs.""""TestTable""""', if_exists => true);
                SELECT add_reorder_policy('logs.""""TestTable""""', 'IX_New_Name');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'logs' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_when_both_fundamental_and_job_settings_change_creates_full_sequence()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                IndexName = "IX_New_Name",
                OldIndexName = "IX_Old_Name",
                ScheduleInterval = "2 days",
                OldScheduleInterval = "1 day",
                MaxRetries = 5,
                OldMaxRetries = -1,
                RetryPeriod = "10 minutes",
                OldRetryPeriod = "10 minutes"
            };

            string expected = @".Sql(@""
                SELECT remove_reorder_policy('public.""""TestTable""""', if_exists => true);
                SELECT add_reorder_policy('public.""""TestTable""""', 'IX_New_Name');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days', max_retries => 5, retry_period => INTERVAL '10 minutes')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }
    }
}
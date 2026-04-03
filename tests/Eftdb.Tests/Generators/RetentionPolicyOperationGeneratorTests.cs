using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    public class RetentionPolicyOperationGeneratorTests
    {
        /// <summary>
        /// A helper to run the generator and capture its string output.
        /// </summary>
        private static string GetGeneratedCode(dynamic operation)
        {
            IndentedStringBuilder builder = new();
            RetentionPolicyOperationGenerator generator = new(true);
            List<string> statements = generator.Generate(operation);
            SqlBuilderHelper.BuildQueryString(statements, builder);
            return builder.ToString();
        }

        // --- Tests for AddRetentionPolicyOperation ---

        #region Generate_Add_DropAfter_with_minimal_config_creates_only_add_policy_sql

        [Fact]
        public void Generate_Add_DropAfter_with_minimal_config_creates_only_add_policy_sql()
        {
            // Arrange
            AddRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "7 days"
            };

            string expected = @".Sql(@""
                SELECT add_retention_policy('public.""""TestTable""""', drop_after => INTERVAL '7 days');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Add_DropCreatedBefore_creates_add_policy_without_alter_job

        [Fact]
        public void Generate_Add_DropCreatedBefore_creates_add_policy_without_alter_job()
        {
            // Arrange
            AddRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropCreatedBefore = "30 days",
                ScheduleInterval = "1 day",
                MaxRetries = 5
            };

            // DropCreatedBefore policies must not emit alter_job due to TimescaleDB bug #9446.
            // Job settings are intentionally ignored.
            string expected = @".Sql(@""
                SELECT add_retention_policy('public.""""TestTable""""', drop_created_before => INTERVAL '30 days');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Add_with_InitialStart_includes_iso_8601_timestamp

        [Fact]
        public void Generate_Add_with_InitialStart_includes_iso_8601_timestamp()
        {
            // Arrange
            DateTime testDate = new(2025, 10, 20, 12, 30, 0, DateTimeKind.Utc);
            AddRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "7 days",
                InitialStart = testDate
            };

            string expected = @".Sql(@""
                SELECT add_retention_policy('public.""""TestTable""""', drop_after => INTERVAL '7 days', initial_start => '2025-10-20T12:30:00.0000000Z');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Add_DropAfter_with_all_job_settings_creates_add_and_alter_job_sql

        [Fact]
        public void Generate_Add_DropAfter_with_all_job_settings_creates_add_and_alter_job_sql()
        {
            // Arrange
            AddRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "7 days",
                ScheduleInterval = "2 days",
                MaxRuntime = "1 hour",
                MaxRetries = 5,
                RetryPeriod = "10 minutes"
            };

            string expected = @".Sql(@""
                SELECT add_retention_policy('public.""""TestTable""""', drop_after => INTERVAL '7 days');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days', max_runtime => INTERVAL '1 hour', max_retries => 5, retry_period => INTERVAL '10 minutes')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Add_DropCreatedBefore_with_job_settings_still_omits_alter_job

        [Fact]
        public void Generate_Add_DropCreatedBefore_with_job_settings_still_omits_alter_job()
        {
            // Arrange
            AddRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropCreatedBefore = "30 days",
                ScheduleInterval = "2 days",
                MaxRuntime = "1 hour",
                MaxRetries = 5,
                RetryPeriod = "10 minutes"
            };

            // Even with all job settings specified, alter_job is omitted for DropCreatedBefore
            // due to TimescaleDB bug #9446 workaround.
            string expected = @".Sql(@""
                SELECT add_retention_policy('public.""""TestTable""""', drop_created_before => INTERVAL '30 days');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        // --- Tests for AlterRetentionPolicyOperation ---

        #region Generate_Alter_when_only_job_settings_change_creates_only_alter_job_sql

        [Fact]
        public void Generate_Alter_when_only_job_settings_change_creates_only_alter_job_sql()
        {
            // Arrange
            AlterRetentionPolicyOperation operation = new()
            {
                Schema = "metrics",
                TableName = "TestTable",
                DropAfter = "7 days",
                OldDropAfter = "7 days",
                DropCreatedBefore = null,
                OldDropCreatedBefore = null,
                InitialStart = null,
                OldInitialStart = null,
                ScheduleInterval = "2 days",
                OldScheduleInterval = "1 day"
            };

            string expected = @".Sql(@""
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'metrics' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Alter_when_DropAfter_changes_creates_remove_add_and_alter_job_sql

        [Fact]
        public void Generate_Alter_when_DropAfter_changes_creates_remove_add_and_alter_job_sql()
        {
            // Arrange
            AlterRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "14 days",        // <-- Changed from "7 days"
                OldDropAfter = "7 days",
                ScheduleInterval = "1 day",
                OldScheduleInterval = "1 day"
            };

            string expected = @".Sql(@""
                SELECT remove_retention_policy('public.""""TestTable""""', if_exists => true);
                SELECT add_retention_policy('public.""""TestTable""""', drop_after => INTERVAL '14 days');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '1 day')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Alter_changed_to_DropCreatedBefore_creates_remove_and_add_without_alter_job

        [Fact]
        public void Generate_Alter_changed_to_DropCreatedBefore_creates_remove_and_add_without_alter_job()
        {
            // Arrange
            AlterRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = null,                     // <-- Changed from "7 days"
                OldDropAfter = "7 days",
                DropCreatedBefore = "30 days",        // <-- Changed from null
                OldDropCreatedBefore = null,
                ScheduleInterval = "1 day",
                OldScheduleInterval = "1 day"
            };

            // During Alter recreation, alter_job is still emitted to reapply existing job settings.
            // The DropCreatedBefore workaround only applies to the Add path.
            string expected = @".Sql(@""
                SELECT remove_retention_policy('public.""""TestTable""""', if_exists => true);
                SELECT add_retention_policy('public.""""TestTable""""', drop_created_before => INTERVAL '30 days');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Alter_when_InitialStart_changes_creates_remove_add_and_alter_job_sql

        [Fact]
        public void Generate_Alter_when_InitialStart_changes_creates_remove_add_and_alter_job_sql()
        {
            // Arrange
            DateTime oldDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime newDate = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            AlterRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "7 days",
                OldDropAfter = "7 days",
                InitialStart = newDate,               // <-- Changed from oldDate
                OldInitialStart = oldDate,
                ScheduleInterval = "1 day",
                OldScheduleInterval = "1 day"
            };

            string expected = @".Sql(@""
                SELECT remove_retention_policy('public.""""TestTable""""', if_exists => true);
                SELECT add_retention_policy('public.""""TestTable""""', drop_after => INTERVAL '7 days', initial_start => '2025-06-15T12:00:00.0000000Z');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '1 day')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        // --- Tests for DropRetentionPolicyOperation ---

        #region Generate_Drop_creates_correct_remove_policy_sql

        [Fact]
        public void Generate_Drop_creates_correct_remove_policy_sql()
        {
            // Arrange
            DropRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable"
            };

            string expected = @".Sql(@""
                SELECT remove_retention_policy('public.""""TestTable""""', if_exists => true);
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Drop_with_custom_schema_uses_correct_regclass_quoting

        [Fact]
        public void Generate_Drop_with_custom_schema_uses_correct_regclass_quoting()
        {
            // Arrange
            DropRetentionPolicyOperation operation = new()
            {
                Schema = "analytics",
                TableName = "EventLogs"
            };

            string expected = @".Sql(@""
                SELECT remove_retention_policy('analytics.""""EventLogs""""', if_exists => true);
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region Generate_Alter_when_both_fundamental_and_job_settings_change_creates_full_sequence

        [Fact]
        public void Generate_Alter_when_both_fundamental_and_job_settings_change_creates_full_sequence()
        {
            // Arrange
            AlterRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "14 days",                // <-- Changed from "7 days"
                OldDropAfter = "7 days",
                ScheduleInterval = "2 days",          // <-- Changed from "1 day"
                OldScheduleInterval = "1 day",
                MaxRetries = 5,                       // <-- Changed from default
                OldMaxRetries = -1,
                RetryPeriod = "10 minutes",
                OldRetryPeriod = "10 minutes"
            };

            string expected = @".Sql(@""
                SELECT remove_retention_policy('public.""""TestTable""""', if_exists => true);
                SELECT add_retention_policy('public.""""TestTable""""', drop_after => INTERVAL '14 days');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days', max_retries => 5, retry_period => INTERVAL '10 minutes')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable';
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        // --- Tests for runtime quoting (isDesignTime=false) ---

        private static List<string> GetRuntimeStatements(dynamic operation)
        {
            RetentionPolicyOperationGenerator generator = new(isDesignTime: false);
            return generator.Generate(operation);
        }

        #region Generate_Add_DropAfter_with_runtime_quoting_uses_single_quotes

        [Fact]
        public void Generate_Add_DropAfter_with_runtime_quoting_uses_single_quotes()
        {
            // Arrange
            AddRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "7 days"
            };

            // Act
            List<string> statements = GetRuntimeStatements(operation);

            // Assert
            Assert.Single(statements);
            Assert.Contains("'public.\"TestTable\"'", statements[0]);
        }

        #endregion

        #region Generate_Drop_with_runtime_quoting_uses_single_quotes

        [Fact]
        public void Generate_Drop_with_runtime_quoting_uses_single_quotes()
        {
            // Arrange
            DropRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable"
            };

            // Act
            List<string> statements = GetRuntimeStatements(operation);

            // Assert
            Assert.Single(statements);
            Assert.Contains("'public.\"TestTable\"'", statements[0]);
        }

        #endregion

        // --- Tests for alter no-change path ---

        #region Generate_Alter_when_no_changes_returns_empty_list

        [Fact]
        public void Generate_Alter_when_no_changes_returns_empty_list()
        {
            // Arrange
            AlterRetentionPolicyOperation operation = new()
            {
                Schema = "public",
                TableName = "TestTable",
                DropAfter = "7 days",
                OldDropAfter = "7 days",
                DropCreatedBefore = null,
                OldDropCreatedBefore = null,
                InitialStart = null,
                OldInitialStart = null,
                ScheduleInterval = "1 day",
                OldScheduleInterval = "1 day",
                MaxRuntime = "00:00:00",
                OldMaxRuntime = "00:00:00",
                MaxRetries = -1,
                OldMaxRetries = -1,
                RetryPeriod = "1 day",
                OldRetryPeriod = "1 day"
            };

            // Act
            RetentionPolicyOperationGenerator generator = new(true);
            List<string> result = generator.Generate(operation);

            // Assert
            Assert.Empty(result);
        }

        #endregion
    }
}

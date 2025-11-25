using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    /// <summary>
    /// Comprehensive tests for ReorderPolicyOperationGenerator validating design-time and runtime
    /// SQL generation according to TimescaleDB requirements.
    /// </summary>
    public class ReorderPolicyOperationGeneratorComprehensiveTests
    {
        /// <summary>
        /// Helper to run the generator and capture design-time C# code output.
        /// </summary>
        private static string GetDesignTimeCode(dynamic operation)
        {
            IndentedStringBuilder builder = new();
            ReorderPolicyOperationGenerator generator = new(isDesignTime: true);
            List<string> statements = generator.Generate(operation);
            SqlBuilderHelper.BuildQueryString(statements, builder);
            return builder.ToString();
        }

        /// <summary>
        /// Helper to run the generator and capture runtime SQL output.
        /// </summary>
        private static List<string> GetRuntimeSqlStatements(dynamic operation)
        {
            ReorderPolicyOperationGenerator generator = new(isDesignTime: false);
            return generator.Generate(operation);
        }

        /// <summary>
        /// Helper to get combined SQL output as single string.
        /// </summary>
        private static string GetRuntimeSql(dynamic operation)
        {
            List<string> statements = GetRuntimeSqlStatements(operation);
            return string.Join("\n", statements);
        }

        #region AddReorderPolicyOperation - Design Time Tests

        [Fact]
        public void DesignTime_Add_MinimalPolicy_GeneratesOnlyAddReorderPolicy()
        {
            // Arrange - Minimal reorder policy with only required fields (no alter_job when using defaults)
            AddReorderPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                IndexName = "metrics_time_idx"
            };

            string expected = @".Sql(@""
                SELECT add_reorder_policy('public.""""metrics""""', 'metrics_time_idx');
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Should only generate add_reorder_policy, not alter_job
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Add_WithAllOptions_GeneratesCorrectCode()
        {
            // Arrange - Policy with all optional parameters
            DateTime initialStart = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            AddReorderPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "analytics",
                IndexName = "sensor_data_time_device_idx",
                InitialStart = initialStart,
                ScheduleInterval = "6 hours",
                MaxRuntime = "30 minutes",
                MaxRetries = 5,
                RetryPeriod = "2 minutes"
            };

            string expected = $@".Sql(@""
                SELECT add_reorder_policy('analytics.""""sensor_data""""', 'sensor_data_time_device_idx', initial_start => '{initialStart:yyyy-MM-ddTHH:mm:ss.fffffffZ}');
                SELECT alter_job(job_id, schedule_interval => INTERVAL '6 hours', max_runtime => INTERVAL '30 minutes', max_retries => 5, retry_period => INTERVAL '2 minutes')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'analytics' AND hypertable_name = 'sensor_data';
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Add_WithUnlimitedRetries_UsesNegativeOne()
        {
            // Arrange - MaxRetries = -1 means unlimited (TimescaleDB convention)
            AddReorderPolicyOperation operation = new()
            {
                TableName = "important_data",
                Schema = "public",
                IndexName = "time_idx",
                MaxRetries = -1
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Should use max_retries => -1 for unlimited
            Assert.Contains("max_retries => -1", result);
        }

        [Fact]
        public void DesignTime_Add_WithNoMaxRuntime_UsesZeroInterval()
        {
            // Arrange - MaxRuntime = "00:00:00" means no limit (TimescaleDB convention)
            AddReorderPolicyOperation operation = new()
            {
                TableName = "data",
                Schema = "public",
                IndexName = "idx",
                MaxRuntime = "00:00:00"
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Should use INTERVAL '00:00:00' for no limit
            Assert.Contains("INTERVAL '00:00:00'", result);
        }

        [Fact]
        public void DesignTime_Add_WithInitialStart_FormatsAsISO8601()
        {
            // Arrange
            DateTime testDate = new(2025, 3, 15, 10, 30, 45, 123, DateTimeKind.Utc);
            AddReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                IndexName = "idx",
                InitialStart = testDate
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Should use ISO 8601 format with microseconds and Z suffix
            Assert.Contains("2025-03-15T10:30:45", result);
            Assert.Contains("Z", result);
        }

        #endregion

        #region AddReorderPolicyOperation - Runtime Tests

        [Fact]
        public void Runtime_Add_MinimalPolicy_GeneratesOnlyAddReorderPolicy()
        {
            // Arrange - Minimal policy without custom scheduling
            AddReorderPolicyOperation operation = new()
            {
                TableName = "simple_table",
                Schema = "public",
                IndexName = "simple_idx"
            };

            // Act
            List<string> statements = GetRuntimeSqlStatements(operation);

            // Assert - Should only generate add_reorder_policy (uses TimescaleDB defaults)
            Assert.Single(statements);
            Assert.Contains("SELECT add_reorder_policy('public.\"simple_table\"', 'simple_idx')", statements[0]);
            Assert.DoesNotContain("alter_job", statements[0]);
        }

        [Fact]
        public void Runtime_Add_WithoutInitialStart_OmitsParameter()
        {
            // Arrange - No InitialStart specified
            AddReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                IndexName = "idx"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Should not include initial_start parameter
            Assert.DoesNotContain("initial_start =>", result);
        }

        [Fact]
        public void Runtime_Add_WithCustomSchedule_QueriesCorrectJobView()
        {
            // Arrange - Add custom schedule to trigger alter_job generation
            AddReorderPolicyOperation operation = new()
            {
                TableName = "my_table",
                Schema = "my_schema",
                IndexName = "my_idx",
                ScheduleInterval = "6 hours" // Non-default triggers alter_job
            };

            // Act
            List<string> statements = GetRuntimeSqlStatements(operation);
            string alterJobSql = statements[1]; // alter_job is the second statement

            // Assert - Must query timescaledb_information.jobs to find job_id
            Assert.Equal(2, statements.Count);
            Assert.Contains("timescaledb_information.jobs", alterJobSql);
            Assert.Contains("proc_name = 'policy_reorder'", alterJobSql);
            Assert.Contains("hypertable_schema = 'my_schema'", alterJobSql);
            Assert.Contains("hypertable_name = 'my_table'", alterJobSql);
        }

        #endregion

        #region AlterReorderPolicyOperation - Design Time Tests

        [Fact]
        public void DesignTime_Alter_ScheduleInterval_GeneratesCorrectCode()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                ScheduleInterval = "12 hours",
                OldScheduleInterval = "1 day"
            };

            string expected = @".Sql(@""
                SELECT alter_job(job_id, schedule_interval => INTERVAL '12 hours')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'public' AND hypertable_name = 'metrics';
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_MaxRuntime_GeneratesCorrectCode()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "data",
                Schema = "public",
                MaxRuntime = "1 hour",
                OldMaxRuntime = "30 minutes"
            };

            string expected = @".Sql(@""
                SELECT alter_job(job_id, max_runtime => INTERVAL '1 hour')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'public' AND hypertable_name = 'data';
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_MaxRetries_GeneratesCorrectCode()
        {
            // Arrange - Changing from unlimited (-1) to limited (3)
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                MaxRetries = 3,
                OldMaxRetries = -1
            };

            string expected = @".Sql(@""
                SELECT alter_job(job_id, max_retries => 3)
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'public' AND hypertable_name = 'test';
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_RetryPeriod_GeneratesCorrectCode()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                RetryPeriod = "10 minutes",
                OldRetryPeriod = "5 minutes"
            };

            string expected = @".Sql(@""
                SELECT alter_job(job_id, retry_period => INTERVAL '10 minutes')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_reorder' AND hypertable_schema = 'public' AND hypertable_name = 'test';
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_MultipleProperties_GeneratesCombinedStatement()
        {
            // Arrange - Altering multiple job properties at once
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "complex",
                Schema = "public",
                ScheduleInterval = "2 days",
                OldScheduleInterval = "1 day",
                MaxRuntime = "2 hours",
                OldMaxRuntime = "1 hour",
                MaxRetries = 10,
                OldMaxRetries = 5
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - All changes should be in one alter_job call
            Assert.Contains("schedule_interval => INTERVAL '2 days'", result);
            Assert.Contains("max_runtime => INTERVAL '2 hours'", result);
            Assert.Contains("max_retries => 10", result);
        }

        [Fact]
        public void Alter_NoChanges_GeneratesNoSQL()
        {
            // Arrange - Nothing changed
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "unchanged",
                Schema = "public",
                ScheduleInterval = "1 day",
                OldScheduleInterval = "1 day",
                MaxRetries = 3,
                OldMaxRetries = 3
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Should generate empty result
            Assert.Empty(result.Trim());
        }

        #endregion

        #region AlterReorderPolicyOperation - Runtime Tests

        [Fact]
        public void Runtime_Alter_SingleProperty_GeneratesCorrectSQL()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "analytics",
                ScheduleInterval = "4 hours",
                OldScheduleInterval = "6 hours"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("SELECT alter_job(job_id, schedule_interval => INTERVAL '4 hours')", result);
            Assert.Contains("FROM timescaledb_information.jobs", result);
        }

        [Fact]
        public void Runtime_Alter_ChangingToUnlimitedRetries_UsesNegativeOne()
        {
            // Arrange - Changing to unlimited retries
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                MaxRetries = -1,
                OldMaxRetries = 5
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("max_retries => -1", result);
        }

        #endregion

        #region DropReorderPolicyOperation - Design Time Tests

        [Fact]
        public void DesignTime_Drop_GeneratesCorrectCode()
        {
            // Arrange
            DropReorderPolicyOperation operation = new()
            {
                TableName = "old_table",
                Schema = "public"
            };

            string expected = @".Sql(@""
                SELECT remove_reorder_policy('public.""""old_table""""', if_exists => true);
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Drop_WithCustomSchema_GeneratesCorrectCode()
        {
            // Arrange
            DropReorderPolicyOperation operation = new()
            {
                TableName = "analytics_data",
                Schema = "analytics"
            };

            string expected = @".Sql(@""
                SELECT remove_reorder_policy('analytics.""""analytics_data""""', if_exists => true);
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region DropReorderPolicyOperation - Runtime Tests

        [Fact]
        public void Runtime_Drop_GeneratesCorrectSQL()
        {
            // Arrange
            DropReorderPolicyOperation operation = new()
            {
                TableName = "remove_this",
                Schema = "public"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Runtime uses single quotes
            Assert.Contains("SELECT remove_reorder_policy('public.\"remove_this\"', if_exists => true)", result);
            Assert.EndsWith(";", result.Trim());
        }

        [Fact]
        public void Runtime_Drop_AlwaysUsesIfExists()
        {
            // Arrange
            DropReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - if_exists => true prevents errors
            Assert.Contains("if_exists => true", result);
        }

        #endregion

        #region TimescaleDB Constraint Validation Tests

        [Fact]
        public void Add_DefaultValues_MatchTimescaleDBDefaults()
        {
            // Arrange - Using default values from DefaultValues.cs
            AddReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                IndexName = "idx",
                ScheduleInterval = "1 day",      // Default
                MaxRuntime = "00:00:00",         // Default (no limit)
                MaxRetries = -1,                 // Default (unlimited)
                RetryPeriod = "00:05:00"         // Default (5 minutes)
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Should match TimescaleDB defaults
            Assert.Contains("schedule_interval => INTERVAL '1 day'", result);
            Assert.Contains("max_runtime => INTERVAL '00:00:00'", result);
            Assert.Contains("max_retries => -1", result);
            Assert.Contains("retry_period => INTERVAL '00:05:00'", result);
        }

        [Fact]
        public void Add_WithCustomSchedule_RequiresAlterJob_AfterAddReorderPolicy()
        {
            // Arrange - Provide custom schedule to trigger alter_job
            AddReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                IndexName = "idx",
                ScheduleInterval = "12 hours" // Triggers alter_job
            };

            // Act
            List<string> statements = GetRuntimeSqlStatements(operation);

            // Assert - add_reorder_policy must come before alter_job
            Assert.Equal(2, statements.Count);
            Assert.Contains("add_reorder_policy", statements[0]);
            Assert.Contains("alter_job", statements[1]);
        }

        [Fact]
        public void Alter_QueriesJobByTableAndSchema()
        {
            // Arrange
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "specific_table",
                Schema = "specific_schema",
                ScheduleInterval = "2 hours",
                OldScheduleInterval = "1 hour"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Must identify correct job by schema and table name
            Assert.Contains("hypertable_schema = 'specific_schema'", result);
            Assert.Contains("hypertable_name = 'specific_table'", result);
            Assert.Contains("proc_name = 'policy_reorder'", result);
        }

        [Fact]
        public void Add_IntervalFormat_AcceptsVariousFormats()
        {
            // Arrange - TimescaleDB accepts various interval formats
            AddReorderPolicyOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                IndexName = "idx",
                ScheduleInterval = "2 days",
                MaxRuntime = "30 minutes",
                RetryPeriod = "1 hour"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - All interval formats should work
            Assert.Contains("INTERVAL '2 days'", result);
            Assert.Contains("INTERVAL '30 minutes'", result);
            Assert.Contains("INTERVAL '1 hour'", result);
        }

        [Fact]
        public void Drop_SafeOperation_UsesIfExists()
        {
            // Arrange - Drop should be safe operation
            DropReorderPolicyOperation operation = new()
            {
                TableName = "maybe_has_policy",
                Schema = "public"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - if_exists prevents errors if policy doesn't exist
            Assert.Contains("if_exists => true", result);
        }

        #endregion
    }
}

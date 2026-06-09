using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    public class PolicyJobSqlBuilderTests
    {
        // --- BuildJobClauses ---

        #region BuildJobClauses_AllProvided_EmitsFourClauses

        [Fact]
        public void BuildJobClauses_AllProvided_EmitsFourClauses()
        {
            // Arrange
            string scheduleInterval = "2 days";
            string maxRuntime = "1 hour";
            int maxRetries = 5;
            string retryPeriod = "10 minutes";

            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildJobClauses(scheduleInterval, maxRuntime, maxRetries, retryPeriod);

            // Assert
            Assert.Equal(4, clauses.Count);
            Assert.Contains("schedule_interval => INTERVAL '2 days'", clauses);
            Assert.Contains("max_runtime => INTERVAL '1 hour'", clauses);
            Assert.Contains("max_retries => 5", clauses);
            Assert.Contains("retry_period => INTERVAL '10 minutes'", clauses);
        }

        #endregion

        #region BuildJobClauses_AllNull_ReturnsEmptyList

        [Fact]
        public void BuildJobClauses_AllNull_ReturnsEmptyList()
        {
            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildJobClauses(null, null, null, null);

            // Assert
            Assert.Empty(clauses);
        }

        #endregion

        #region BuildJobClauses_WhitespaceStrings_AreSkipped

        [Fact]
        public void BuildJobClauses_WhitespaceStrings_AreSkipped()
        {
            // Arrange
            string scheduleInterval = "   ";
            string maxRuntime = "";
            string retryPeriod = "\t";

            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildJobClauses(scheduleInterval, maxRuntime, null, retryPeriod);

            // Assert
            Assert.Empty(clauses);
        }

        #endregion

        #region BuildJobClauses_MaxRetriesZero_IsEmitted

        [Fact]
        public void BuildJobClauses_MaxRetriesZero_IsEmitted()
        {
            // Arrange — 0 is a valid value distinct from null; uses a null check, not truthiness
            int maxRetries = 0;

            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildJobClauses(null, null, maxRetries, null);

            // Assert
            Assert.Single(clauses);
            Assert.Equal("max_retries => 0", clauses[0]);
        }

        #endregion

        #region BuildJobClauses_MaxRetries_EmittedAsBareInt

        [Fact]
        public void BuildJobClauses_MaxRetries_EmittedAsBareInt()
        {
            // Arrange
            int maxRetries = 7;

            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildJobClauses(null, null, maxRetries, null);

            // Assert — bare int, not wrapped in INTERVAL or quotes
            Assert.Single(clauses);
            Assert.Equal("max_retries => 7", clauses[0]);
            Assert.DoesNotContain("INTERVAL", clauses[0]);
        }

        #endregion

        // --- BuildChangedJobClauses ---

        #region BuildChangedJobClauses_OnlyDifferingValuesProduceClauses

        [Fact]
        public void BuildChangedJobClauses_OnlyDifferingValuesProduceClauses()
        {
            // Arrange — only scheduleInterval changes
            List<string> clauses = PolicyJobSqlBuilder.BuildChangedJobClauses(
                scheduleInterval: "2 days", oldScheduleInterval: "1 day",
                maxRuntime: "1 hour", oldMaxRuntime: "1 hour",
                maxRetries: 5, oldMaxRetries: 5,
                retryPeriod: "10 minutes", oldRetryPeriod: "10 minutes");

            // Assert
            Assert.Single(clauses);
            Assert.Equal("schedule_interval => INTERVAL '2 days'", clauses[0]);
        }

        #endregion

        #region BuildChangedJobClauses_MaxRetriesUnchanged_NoClause

        [Fact]
        public void BuildChangedJobClauses_MaxRetriesUnchanged_NoClause()
        {
            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildChangedJobClauses(
                scheduleInterval: null, oldScheduleInterval: null,
                maxRuntime: null, oldMaxRuntime: null,
                maxRetries: 5, oldMaxRetries: 5,
                retryPeriod: null, oldRetryPeriod: null);

            // Assert
            Assert.Empty(clauses);
        }

        #endregion

        #region BuildChangedJobClauses_MaxRetriesChanged_EmitsClause

        [Fact]
        public void BuildChangedJobClauses_MaxRetriesChanged_EmitsClause()
        {
            // Act
            List<string> clauses = PolicyJobSqlBuilder.BuildChangedJobClauses(
                scheduleInterval: null, oldScheduleInterval: null,
                maxRuntime: null, oldMaxRuntime: null,
                maxRetries: 5, oldMaxRetries: 3,
                retryPeriod: null, oldRetryPeriod: null);

            // Assert
            Assert.Single(clauses);
            Assert.Equal("max_retries => 5", clauses[0]);
        }

        #endregion

        #region BuildChangedJobClauses_NewValueNullWhileOldSet_NoClause

        [Fact]
        public void BuildChangedJobClauses_NewValueNullWhileOldSet_NoClause()
        {
            // Arrange — a value cannot be cleared via alter_job: a null/whitespace new value
            // while the old value is set produces no clause (documented limitation).
            List<string> clauses = PolicyJobSqlBuilder.BuildChangedJobClauses(
                scheduleInterval: null, oldScheduleInterval: "1 day",
                maxRuntime: "  ", oldMaxRuntime: "1 hour",
                maxRetries: null, oldMaxRetries: 5,
                retryPeriod: "", oldRetryPeriod: "10 minutes");

            // Assert
            Assert.Empty(clauses);
        }

        #endregion

        // --- BuildAlterJobSql ---

        #region BuildAlterJobSql_ProducesExpectedStatement

        [Fact]
        public void BuildAlterJobSql_ProducesExpectedStatement()
        {
            // Arrange
            List<string> clauses = ["schedule_interval => INTERVAL '2 days'", "max_retries => 5"];

            // Act
            string sql = PolicyJobSqlBuilder.BuildAlterJobSql("TestTable", "public", "policy_retention", clauses);

            // Assert
            Assert.Contains("SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days', max_retries => 5)", sql);
            Assert.Contains("FROM timescaledb_information.jobs", sql);
            Assert.Contains("WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable'", sql);
        }

        #endregion

        #region BuildAlterJobSql_ResultIsTrimmed

        [Fact]
        public void BuildAlterJobSql_ResultIsTrimmed()
        {
            // Arrange
            List<string> clauses = ["max_retries => 5"];

            // Act
            string sql = PolicyJobSqlBuilder.BuildAlterJobSql("TestTable", "public", "policy_retention", clauses);

            // Assert — no leading/trailing whitespace
            Assert.Equal(sql, sql.Trim());
            Assert.StartsWith("SELECT alter_job", sql);
        }

        #endregion
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    /// <summary>
    /// Builds the SQL shared by TimescaleDB automation policies whose
    /// scheduling is tuned through the common <c>alter_job</c> function.
    /// </summary>
    internal static class PolicyJobSqlBuilder
    {
        /// <summary>
        /// Builds alter_job tuning clauses for a newly added policy, including every value
        /// that was explicitly provided.
        /// </summary>
        public static List<string> BuildJobClauses(string? scheduleInterval, string? maxRuntime, int? maxRetries, string? retryPeriod)
        {
            List<string> clauses = [];

            if (!string.IsNullOrWhiteSpace(scheduleInterval))
                clauses.Add($"schedule_interval => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(scheduleInterval)}'");

            if (!string.IsNullOrWhiteSpace(maxRuntime))
                clauses.Add($"max_runtime => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(maxRuntime)}'");

            if (maxRetries != null)
                clauses.Add($"max_retries => {maxRetries}");

            if (!string.IsNullOrWhiteSpace(retryPeriod))
                clauses.Add($"retry_period => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(retryPeriod)}'");

            return clauses;
        }

        /// <summary>
        /// Builds alter_job tuning clauses for only the values that changed relative to the
        /// previous policy state.
        /// </summary>
        public static List<string> BuildChangedJobClauses(
            string? scheduleInterval, string? oldScheduleInterval,
            string? maxRuntime, string? oldMaxRuntime,
            int? maxRetries, int? oldMaxRetries,
            string? retryPeriod, string? oldRetryPeriod)
        {
            List<string> clauses = [];

            if (!string.IsNullOrWhiteSpace(scheduleInterval) && scheduleInterval != oldScheduleInterval)
                clauses.Add($"schedule_interval => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(scheduleInterval)}'");

            if (!string.IsNullOrWhiteSpace(maxRuntime) && maxRuntime != oldMaxRuntime)
                clauses.Add($"max_runtime => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(maxRuntime)}'");

            if (maxRetries != null && maxRetries != oldMaxRetries)
                clauses.Add($"max_retries => {maxRetries}");

            if (!string.IsNullOrWhiteSpace(retryPeriod) && retryPeriod != oldRetryPeriod)
                clauses.Add($"retry_period => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(retryPeriod)}'");

            return clauses;
        }

        /// <summary>
        /// Builds the alter_job statement that tunes the policy job identified by
        /// <paramref name="procName"/> for the given table.
        /// </summary>
        public static string BuildAlterJobSql(string tableName, string schema, string procName, IEnumerable<string> clauses)
        {
            string escapedProcName = SqlBuilderHelper.EscapeStringLiteral(procName);
            string escapedSchema = SqlBuilderHelper.EscapeStringLiteral(schema);
            string escapedTableName = SqlBuilderHelper.EscapeStringLiteral(tableName);

            return $@"
                SELECT alter_job(job_id, {string.Join(", ", clauses)})
                FROM timescaledb_information.jobs
                WHERE proc_name = '{escapedProcName}' AND hypertable_schema = '{escapedSchema}' AND hypertable_name = '{escapedTableName}';".Trim();
        }
    }
}

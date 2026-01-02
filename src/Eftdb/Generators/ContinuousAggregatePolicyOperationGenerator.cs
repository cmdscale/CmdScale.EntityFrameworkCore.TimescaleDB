using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using System.Globalization;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    /// <summary>
    /// Generates SQL for continuous aggregate refresh policy operations.
    /// </summary>
    public class ContinuousAggregatePolicyOperationGenerator
    {
        private readonly string quoteString = "\"";
        private readonly SqlBuilderHelper sqlHelper;

        /// <summary>
        /// Initializes a new instance of the ContinuousAggregatePolicyOperationGenerator class.
        /// </summary>
        /// <param name="isDesignTime">Whether this generator is being used at design-time (for C# code generation) or runtime (for SQL execution).</param>
        public ContinuousAggregatePolicyOperationGenerator(bool isDesignTime = false)
        {
            if (isDesignTime)
            {
                quoteString = "\"\"";
            }

            sqlHelper = new SqlBuilderHelper(quoteString);
        }

        /// <summary>
        /// Generates SQL statements for adding a continuous aggregate refresh policy.
        /// </summary>
        /// <param name="operation">The add policy operation.</param>
        /// <returns>A list of SQL statements to execute.</returns>
        public List<string> Generate(AddContinuousAggregatePolicyOperation operation)
        {
            string qualifiedViewName = sqlHelper.Regclass(operation.MaterializedViewName, operation.Schema);

            List<string> arguments = [];

            // Required parameters
            arguments.Add(qualifiedViewName);

            // start_offset - NULL means earliest data
            if (operation.StartOffset == null)
            {
                arguments.Add("start_offset => NULL");
            }
            else if (int.TryParse(operation.StartOffset, out _))
            {
                // Integer-based time column
                arguments.Add($"start_offset => {operation.StartOffset}");
            }
            else
            {
                // Interval string
                arguments.Add($"start_offset => INTERVAL '{operation.StartOffset}'");
            }

            // end_offset - NULL means latest data
            if (operation.EndOffset == null)
            {
                arguments.Add("end_offset => NULL");
            }
            else if (int.TryParse(operation.EndOffset, out _))
            {
                // Integer-based time column
                arguments.Add($"end_offset => {operation.EndOffset}");
            }
            else
            {
                // Interval string
                arguments.Add($"end_offset => INTERVAL '{operation.EndOffset}'");
            }

            // Optional parameters - only add if they differ from defaults
            if (!string.IsNullOrWhiteSpace(operation.ScheduleInterval))
            {
                arguments.Add($"schedule_interval => INTERVAL '{operation.ScheduleInterval}'");
            }

            if (operation.IfNotExists)
            {
                arguments.Add($"if_not_exists => {operation.IfNotExists.ToString().ToLowerInvariant()}");
            }

            if (!string.IsNullOrWhiteSpace(operation.Timezone))
            {
                arguments.Add($"timezone => '{operation.Timezone}'");
            }

            if (operation.IncludeTieredData.HasValue)
            {
                arguments.Add($"include_tiered_data => {operation.IncludeTieredData.Value.ToString().ToLowerInvariant()}");
            }

            if (operation.BucketsPerBatch != 1)
            {
                arguments.Add($"buckets_per_batch => {operation.BucketsPerBatch}");
            }

            if (operation.MaxBatchesPerExecution != 0)
            {
                arguments.Add($"max_batches_per_execution => {operation.MaxBatchesPerExecution}");
            }

            if (!operation.RefreshNewestFirst)
            {
                arguments.Add($"refresh_newest_first => {operation.RefreshNewestFirst.ToString().ToLowerInvariant()}");
            }

            if (operation.InitialStart.HasValue)
            {
                // Use ISO 8601 format for timestamps to avoid ambiguity
                string timestamp = operation.InitialStart.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                arguments.Add($"initial_start => '{timestamp}'");
            }

            string sql = $"SELECT add_continuous_aggregate_policy({string.Join(", ", arguments)});";

            return [sql];
        }

        /// <summary>
        /// Generates SQL statements for removing a continuous aggregate refresh policy.
        /// </summary>
        /// <param name="operation">The remove policy operation.</param>
        /// <returns>A list of SQL statements to execute.</returns>
        public List<string> Generate(RemoveContinuousAggregatePolicyOperation operation)
        {
            string qualifiedViewName = sqlHelper.Regclass(operation.MaterializedViewName, operation.Schema);

            List<string> arguments = [qualifiedViewName];

            if (operation.IfExists)
            {
                arguments.Add($"if_exists => {operation.IfExists.ToString().ToLowerInvariant()}");
            }

            string sql = $"SELECT remove_continuous_aggregate_policy({string.Join(", ", arguments)});";

            return [sql];
        }
    }
}

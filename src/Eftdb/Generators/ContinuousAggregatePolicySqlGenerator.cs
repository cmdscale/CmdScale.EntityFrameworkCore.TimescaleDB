using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Generators
{
    /// <summary>
    /// Generates SQL for continuous aggregate refresh policy operations.
    /// </summary>
    public class ContinuousAggregatePolicySqlGenerator
    {
        /// <summary>
        /// Generates SQL statements for adding a continuous aggregate refresh policy.
        /// </summary>
        /// <param name="operation">The add policy operation.</param>
        /// <returns>A list of SQL statements to execute.</returns>
        public static List<string> Generate(AddContinuousAggregatePolicyOperation operation)
        {
            string qualifiedViewName = SqlBuilderHelper.Regclass(operation.MaterializedViewName, operation.Schema);

            List<string> arguments = [];

            // Required parameters
            arguments.Add(qualifiedViewName);

            // start_offset - NULL means earliest data
            if (operation.StartOffset == null)
            {
                arguments.Add("start_offset => NULL");
            }
            else
            {
                arguments.Add($"start_offset => {SqlBuilderHelper.IntervalOrBigint(operation.StartOffset)}");
            }

            // end_offset - NULL means latest data
            if (operation.EndOffset == null)
            {
                arguments.Add("end_offset => NULL");
            }
            else
            {
                arguments.Add($"end_offset => {SqlBuilderHelper.IntervalOrBigint(operation.EndOffset)}");
            }

            // Optional parameters - only add if they differ from defaults
            if (!string.IsNullOrWhiteSpace(operation.ScheduleInterval))
            {
                arguments.Add($"schedule_interval => INTERVAL '{SqlBuilderHelper.EscapeStringLiteral(operation.ScheduleInterval)}'");
            }

            if (operation.IfNotExists)
            {
                arguments.Add($"if_not_exists => {operation.IfNotExists.ToString().ToLowerInvariant()}");
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
                arguments.Add($"initial_start => '{SqlBuilderHelper.FormatTimestamp(operation.InitialStart.Value)}'");
            }

            string sql = $"SELECT add_continuous_aggregate_policy({string.Join(", ", arguments)});";

            return [sql];
        }

        /// <summary>
        /// Generates SQL statements for removing a continuous aggregate refresh policy.
        /// </summary>
        /// <param name="operation">The remove policy operation.</param>
        /// <returns>A list of SQL statements to execute.</returns>
        public static List<string> Generate(RemoveContinuousAggregatePolicyOperation operation)
        {
            string qualifiedViewName = SqlBuilderHelper.Regclass(operation.MaterializedViewName, operation.Schema);

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

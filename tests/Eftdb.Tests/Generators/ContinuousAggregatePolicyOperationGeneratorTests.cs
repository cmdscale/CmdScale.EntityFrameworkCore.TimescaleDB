using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators;

public class ContinuousAggregatePolicyOperationGeneratorTests
{
    /// <summary>
    /// Helper to run the generator and capture its string output for design-time (migration code generation).
    /// </summary>
    private static string GetGeneratedCode(dynamic operation)
    {
        IndentedStringBuilder builder = new();
        ContinuousAggregatePolicyOperationGenerator generator = new(true);
        List<string> statements = generator.Generate(operation);
        SqlBuilderHelper.BuildQueryString(statements, builder);
        return builder.ToString();
    }

    /// <summary>
    /// Helper to run the generator for runtime SQL execution.
    /// </summary>
    private static string GetRuntimeSql(dynamic operation)
    {
        IndentedStringBuilder builder = new();
        ContinuousAggregatePolicyOperationGenerator generator = new(false);
        List<string> statements = generator.Generate(operation);
        SqlBuilderHelper.BuildQueryString(statements, builder);
        return builder.ToString();
    }

    [Fact]
    public void Generate_Add_With_All_Parameters()
    {
        // Arrange
        DateTime testDate = new(2025, 12, 15, 3, 0, 0, DateTimeKind.Utc);
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour",
            ScheduleInterval = "1 hour",
            InitialStart = testDate,
            IfNotExists = true,
            IncludeTieredData = true,
            BucketsPerBatch = 5,
            MaxBatchesPerExecution = 10,
            RefreshNewestFirst = false
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('public.""""hourly_metrics""""', start_offset => INTERVAL '1 month', end_offset => INTERVAL '1 hour', schedule_interval => INTERVAL '1 hour', if_not_exists => true, include_tiered_data => true, buckets_per_batch => 5, max_batches_per_execution => 10, refresh_newest_first => false, initial_start => '2025-12-15T03:00:00.0000000Z');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Generate_Add_With_Minimal_Parameters()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour"
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('public.""""hourly_metrics""""', start_offset => INTERVAL '1 month', end_offset => INTERVAL '1 hour');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Generate_Add_With_Null_Offsets()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = null,
            EndOffset = null,
            ScheduleInterval = "1 hour"
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('public.""""hourly_metrics""""', start_offset => NULL, end_offset => NULL, schedule_interval => INTERVAL '1 hour');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Generate_Add_With_Integer_Offsets()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "sensor_data_hourly",
            StartOffset = "100000",
            EndOffset = "1000",
            ScheduleInterval = "1 hour"
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('public.""""sensor_data_hourly""""', start_offset => 100000, end_offset => 1000, schedule_interval => INTERVAL '1 hour');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Generate_Remove_Policy()
    {
        // Arrange
        RemoveContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            IfExists = false
        };

        string expected = @".Sql(@""
            SELECT remove_continuous_aggregate_policy('public.""""hourly_metrics""""');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Generate_Remove_Policy_With_IfExists()
    {
        // Arrange
        RemoveContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            IfExists = true
        };

        string expected = @".Sql(@""
            SELECT remove_continuous_aggregate_policy('public.""""hourly_metrics""""', if_exists => true);
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Use_Correct_Quotes_For_Runtime()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour"
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('public.""hourly_metrics""', start_offset => INTERVAL '1 month', end_offset => INTERVAL '1 hour');
        "")";

        // Act
        string result = GetRuntimeSql(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Use_Correct_Quotes_For_DesignTime()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour"
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('public.""""hourly_metrics""""', start_offset => INTERVAL '1 month', end_offset => INTERVAL '1 hour');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Include_Schema_In_Regclass()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "analytics",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour"
        };

        string expected = @".Sql(@""
            SELECT add_continuous_aggregate_policy('analytics.""""hourly_metrics""""', start_offset => INTERVAL '1 month', end_offset => INTERVAL '1 hour');
        "")";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    [Fact]
    public void Format_InitialStart_As_ISO8601()
    {
        // Arrange
        DateTime testDate = new(2025, 12, 15, 3, 30, 45, DateTimeKind.Utc);
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour",
            InitialStart = testDate
        };

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Contains("initial_start => '2025-12-15T03:30:45.0000000Z'", result);
    }

    [Fact]
    public void Omit_Default_Values()
    {
        // Arrange
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour",
            ScheduleInterval = "1 hour",
            IfNotExists = false, // Default value
            BucketsPerBatch = 1, // Default value
            MaxBatchesPerExecution = 0, // Default value
            RefreshNewestFirst = true // Default value
        };

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.DoesNotContain("if_not_exists", result);
        Assert.DoesNotContain("buckets_per_batch", result);
        Assert.DoesNotContain("max_batches_per_execution", result);
        Assert.DoesNotContain("refresh_newest_first", result);
    }

    [Fact]
    public void Include_All_Optional_Parameters()
    {
        // Arrange
        DateTime testDate = new(2025, 12, 15, 3, 0, 0, DateTimeKind.Utc);
        AddContinuousAggregatePolicyOperation operation = new()
        {
            Schema = "public",
            MaterializedViewName = "hourly_metrics",
            StartOffset = "1 month",
            EndOffset = "1 hour",
            ScheduleInterval = "2 hours",
            InitialStart = testDate,
            IfNotExists = true,
            IncludeTieredData = false,
            BucketsPerBatch = 3,
            MaxBatchesPerExecution = 5,
            RefreshNewestFirst = false
        };

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Contains("schedule_interval => INTERVAL '2 hours'", result);
        Assert.Contains("if_not_exists => true", result);
        Assert.Contains("include_tiered_data => false", result);
        Assert.Contains("buckets_per_batch => 3", result);
        Assert.Contains("max_batches_per_execution => 5", result);
        Assert.Contains("refresh_newest_first => false", result);
        Assert.Contains("initial_start => '2025-12-15T03:00:00.0000000Z'", result);
    }
}

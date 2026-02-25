using CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests;

public class TimeBucketQueryTests(TimescaleQueryFixture fixture) : IClassFixture<TimescaleQueryFixture>
{
    #region Should_Generate_TimeBucket_DateTime_In_Select

    [Fact]
    public async Task Should_Generate_TimeBucket_DateTime_In_Select()
    {
        await using TimescaleQueryFixture.QueryTestContext context = fixture.CreateContext();

        TimeSpan bucket = TimeSpan.FromMinutes(5);
        _ = await context.Metrics
            .Select(m => EF.Functions.TimeBucket(bucket, m.Timestamp))
            .ToListAsync();

        AssertSql(
            """
            @bucket='00:05:00' (DbType = Object)

            SELECT time_bucket(@bucket, q."Timestamp")
            FROM query_metrics AS q
            """);
    }

    #endregion

    #region Should_Generate_TimeBucket_DateTime_In_GroupBy

    [Fact]
    public async Task Should_Generate_TimeBucket_DateTime_In_GroupBy()
    {
        await using TimescaleQueryFixture.QueryTestContext context = fixture.CreateContext();

        TimeSpan bucket = TimeSpan.FromMinutes(5);
        _ = await context.Metrics
            .GroupBy(m => EF.Functions.TimeBucket(bucket, m.Timestamp))
            .Select(g => new
            {
                Bucket = g.Key,
                Total = g.Sum(m => m.Value)
            })
            .ToListAsync();

        AssertSql(
            """
            @bucket='00:05:00' (DbType = Object)

            SELECT q0."Key" AS "Bucket", COALESCE(sum(q0."Value"), 0.0) AS "Total"
            FROM (
                SELECT q."Value", time_bucket(@bucket, q."Timestamp") AS "Key"
                FROM query_metrics AS q
            ) AS q0
            GROUP BY q0."Key"
            """);
    }

    #endregion

    #region Should_Generate_TimeBucket_DateTime_In_Where

    [Fact]
    public async Task Should_Generate_TimeBucket_DateTime_In_Where()
    {
        await using TimescaleQueryFixture.QueryTestContext context = fixture.CreateContext();

        TimeSpan bucket = TimeSpan.FromMinutes(5);
        DateTime threshold = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        _ = await context.Metrics
            .Where(m => EF.Functions.TimeBucket(bucket, m.Timestamp) == threshold)
            .ToListAsync();

        AssertSql(
            """
            @bucket='00:05:00' (DbType = Object)
            @threshold='2025-01-06T10:00:00.0000000Z' (DbType = DateTime)

            SELECT q."Id", q."SequenceNumber", q."Timestamp", q."Value"
            FROM query_metrics AS q
            WHERE time_bucket(@bucket, q."Timestamp") = @threshold
            """);
    }

    #endregion

    #region Should_Generate_TimeBucket_DateTime_In_OrderBy

    [Fact]
    public async Task Should_Generate_TimeBucket_DateTime_In_OrderBy()
    {
        await using TimescaleQueryFixture.QueryTestContext context = fixture.CreateContext();

        TimeSpan bucket = TimeSpan.FromMinutes(5);
        _ = await context.Metrics
            .OrderBy(m => EF.Functions.TimeBucket(bucket, m.Timestamp))
            .ToListAsync();

        AssertSql(
            """
            @bucket='00:05:00' (DbType = Object)

            SELECT q."Id", q."SequenceNumber", q."Timestamp", q."Value"
            FROM query_metrics AS q
            ORDER BY time_bucket(@bucket, q."Timestamp")
            """);
    }

    #endregion

    #region Should_Generate_TimeBucket_DateTime_With_Offset

    [Fact]
    public async Task Should_Generate_TimeBucket_DateTime_With_Offset()
    {
        await using TimescaleQueryFixture.QueryTestContext context = fixture.CreateContext();

        TimeSpan bucket = TimeSpan.FromMinutes(5);
        TimeSpan offset = TimeSpan.FromMinutes(1);
        _ = await context.Metrics
            .Select(m => EF.Functions.TimeBucket(bucket, m.Timestamp, offset))
            .ToListAsync();

        AssertSql(
            """
            @bucket='00:05:00' (DbType = Object)
            @offset='00:01:00' (DbType = Object)

            SELECT time_bucket(@bucket, q."Timestamp", @offset)
            FROM query_metrics AS q
            """);
    }

    #endregion

    #region Should_Generate_TimeBucket_Integer_In_GroupBy

    [Fact]
    public async Task Should_Generate_TimeBucket_Integer_In_GroupBy()
    {
        await using TimescaleQueryFixture.QueryTestContext context = fixture.CreateContext();

        int bucket = 5;
        _ = await context.Metrics
            .GroupBy(m => EF.Functions.TimeBucket(bucket, m.SequenceNumber))
            .Select(g => new
            {
                Bucket = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        AssertSql(
            """
            @bucket='5'

            SELECT q0."Key" AS "Bucket", count(*)::int AS "Count"
            FROM (
                SELECT time_bucket(@bucket, q."SequenceNumber") AS "Key"
                FROM query_metrics AS q
            ) AS q0
            GROUP BY q0."Key"
            """);
    }

    #endregion

    private void AssertSql(params string[] expected)
        => fixture.TestSqlLoggerFactory.AssertBaseline(expected);
}

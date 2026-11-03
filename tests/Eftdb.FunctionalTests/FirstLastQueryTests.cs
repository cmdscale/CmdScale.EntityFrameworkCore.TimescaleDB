using CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests;

public class FirstLastQueryTests(TimescaleQueryFixture fixture)
    : FirstLastQueryTestsBase<TimescaleQueryFixture>(fixture), IClassFixture<TimescaleQueryFixture>;

public class FirstLastApacheQueryTests(TimescaleApacheQueryFixture fixture)
    : FirstLastQueryTestsBase<TimescaleApacheQueryFixture>(fixture), IClassFixture<TimescaleApacheQueryFixture>;

public abstract class FirstLastQueryTestsBase<TFixture>(TFixture fixture)
    where TFixture : TimescaleQueryFixtureBase
{
    #region Should_Generate_First_And_Last_In_GroupBy

    [Fact]
    public async Task Should_Generate_First_And_Last_In_GroupBy()
    {
        await using TimescaleQueryFixtureBase.QueryTestContext context = fixture.CreateContext();

        var results = await context.Metrics
            .GroupBy(m => m.SequenceNumber / 6)
            .Select(g => new
            {
                Bucket = g.Key,
                First = EF.Functions.First(g.Select(m => ValueTuple.Create(m.Value, m.Timestamp))),
                Last = EF.Functions.Last(g.Select(m => ValueTuple.Create(m.Value, m.Timestamp)))
            })
            .OrderBy(r => r.Bucket)
            .ToListAsync();

        Assert.Equal(new[] { 10.0, 70.0 }, results.Select(r => r.First));
        Assert.Equal(new[] { 60.0, 120.0 }, results.Select(r => r.Last));

        AssertSql(
            """
            SELECT q0."Key" AS "Bucket", first(q0."Value", q0."Timestamp") AS "First", last(q0."Value", q0."Timestamp") AS "Last"
            FROM (
                SELECT q."Timestamp", q."Value", q."SequenceNumber" / 6 AS "Key"
                FROM query_metrics AS q
            ) AS q0
            GROUP BY q0."Key"
            ORDER BY q0."Key"
            """);
    }

    #endregion

    #region Should_Generate_First_With_Filter

    [Fact]
    public async Task Should_Generate_First_With_Filter()
    {
        await using TimescaleQueryFixtureBase.QueryTestContext context = fixture.CreateContext();

        var results = await context.Metrics
            .GroupBy(m => m.SequenceNumber / 6)
            .Select(g => new
            {
                Bucket = g.Key,
                First = EF.Functions.First(g.Where(m => m.Value > 30).Select(m => ValueTuple.Create(m.Value, m.Timestamp))),
                Count = g.Count()
            })
            .OrderBy(r => r.Bucket)
            .ToListAsync();

        Assert.Equal(new[] { 40.0, 70.0 }, results.Select(r => r.First));
        Assert.Equal(new[] { 6, 6 }, results.Select(r => r.Count));

        AssertSql(
            """
            SELECT q0."Key" AS "Bucket", first(q0."Value", q0."Timestamp") FILTER (WHERE q0."Value" > 30.0) AS "First", count(*)::int AS "Count"
            FROM (
                SELECT q."Timestamp", q."Value", q."SequenceNumber" / 6 AS "Key"
                FROM query_metrics AS q
            ) AS q0
            GROUP BY q0."Key"
            ORDER BY q0."Key"
            """);
    }

    #endregion

    #region Should_Generate_First_And_Last_With_TimeBucket

    [Fact]
    public async Task Should_Generate_First_And_Last_With_TimeBucket()
    {
        await using TimescaleQueryFixtureBase.QueryTestContext context = fixture.CreateContext();

        TimeSpan bucket = TimeSpan.FromMinutes(5);
        var results = await context.Metrics
            .GroupBy(m => EF.Functions.TimeBucket(bucket, m.Timestamp))
            .Select(g => new
            {
                Bucket = g.Key,
                First = EF.Functions.First(g.Select(m => ValueTuple.Create(m.Value, m.Timestamp))),
                Last = EF.Functions.Last(g.Select(m => ValueTuple.Create(m.Value, m.Timestamp)))
            })
            .OrderBy(r => r.Bucket)
            .ToListAsync();

        Assert.Equal(new[] { 10.0, 60.0, 110.0 }, results.Select(r => r.First));
        Assert.Equal(new[] { 50.0, 100.0, 120.0 }, results.Select(r => r.Last));

        AssertSql(
            """
            @bucket='00:05:00' (DbType = Object)

            SELECT q0."Key" AS "Bucket", first(q0."Value", q0."Timestamp") AS "First", last(q0."Value", q0."Timestamp") AS "Last"
            FROM (
                SELECT q."Timestamp", q."Value", time_bucket(@bucket, q."Timestamp") AS "Key"
                FROM query_metrics AS q
            ) AS q0
            GROUP BY q0."Key"
            ORDER BY q0."Key"
            """);
    }

    #endregion

    private void AssertSql(params string[] expected)
        => fixture.TestSqlLoggerFactory.AssertBaseline(expected);
}

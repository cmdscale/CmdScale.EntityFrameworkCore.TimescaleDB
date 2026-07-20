using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

public class ViewDefinitionParserTests
{
    // A representative TimescaleDB CA view definition with several aggregate functions.
    private const string FullViewDef =
        "SELECT time_bucket('01:00:00'::interval, api_log.\"time\") AS bucket," +
        " api_log.service_name AS service_name," +
        " avg(api_log.duration_ms) AS avg_duration_ms," +
        " max(api_log.duration_ms) AS max_duration_ms," +
        " sum(api_log.request_count) AS total_requests," +
        " count(*) AS request_count" +
        " FROM api_log" +
        " GROUP BY time_bucket('01:00:00'::interval, api_log.\"time\"), api_log.service_name";

    private const string ViewDefWithWhere =
        "SELECT time_bucket('1 day'::interval, readings.\"time\") AS bucket," +
        " avg(readings.value) AS avg_value" +
        " FROM readings" +
        " WHERE readings.device_id = 'sensor-1'" +
        " GROUP BY time_bucket('1 day'::interval, readings.\"time\")";

    // ── ParseTimeBucketWidth ────────────────────────────────────────────────

    #region ParseTimeBucketWidth_Extracts_Interval

    [Fact]
    public void ParseTimeBucketWidth_Extracts_Interval()
    {
        string? result = ViewDefinitionParser.ParseTimeBucketWidth(FullViewDef);

        Assert.Equal("01:00:00", result);
    }

    #endregion

    #region ParseTimeBucketWidth_ReturnsNull_WhenNoTimeBucket

    [Fact]
    public void ParseTimeBucketWidth_ReturnsNull_WhenNoTimeBucket()
    {
        string? result = ViewDefinitionParser.ParseTimeBucketWidth("SELECT 1 AS x FROM y");

        Assert.Null(result);
    }

    #endregion

    #region ParseTimeBucketWidth_Extracts_DayInterval

    [Fact]
    public void ParseTimeBucketWidth_Extracts_DayInterval()
    {
        string? result = ViewDefinitionParser.ParseTimeBucketWidth(ViewDefWithWhere);

        Assert.Equal("1 day", result);
    }

    #endregion

    // ── ParseTimeBucketSourceColumn ─────────────────────────────────────────

    #region ParseTimeBucketSourceColumn_Extracts_Column

    [Fact]
    public void ParseTimeBucketSourceColumn_Extracts_Column()
    {
        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn(FullViewDef);

        // Should strip table qualifier (api_log.) and quotes from "time"
        Assert.Equal("time", result);
    }

    #endregion

    #region ParseTimeBucketSourceColumn_ReturnsNull_WhenNoTimeBucket

    [Fact]
    public void ParseTimeBucketSourceColumn_ReturnsNull_WhenNoTimeBucket()
    {
        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn("SELECT avg(x) AS y FROM t GROUP BY t.z");

        Assert.Null(result);
    }

    #endregion

    // ── ParseAggregates ─────────────────────────────────────────────────────

    #region ParseAggregates_Extracts_CommonFunctions

    [Fact]
    public void ParseAggregates_Extracts_CommonFunctions()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(FullViewDef);

        Assert.Contains(result, a => a.Function == EAggregateFunction.Avg && a.Alias == "avg_duration_ms");
        Assert.Contains(result, a => a.Function == EAggregateFunction.Max && a.Alias == "max_duration_ms");
        Assert.Contains(result, a => a.Function == EAggregateFunction.Sum && a.Alias == "total_requests");
    }

    #endregion

    #region ParseAggregates_CountStar_ProducesWildcardSourceColumn

    [Fact]
    public void ParseAggregates_CountStar_ProducesWildcardSourceColumn()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(FullViewDef);

        ViewDefinitionParser.ParsedAggregate? countAgg = result.FirstOrDefault(a => a.Function == EAggregateFunction.Count);
        Assert.NotNull(countAgg);
        Assert.Equal("request_count", countAgg.Alias);
        Assert.Equal("*", countAgg.SourceColumn);
    }

    #endregion

    #region ParseAggregates_StripsTableQualifier

    [Fact]
    public void ParseAggregates_StripsTableQualifier()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(FullViewDef);

        // api_log.duration_ms → duration_ms
        Assert.All(result.Where(a => a.SourceColumn != "*"), a =>
            Assert.DoesNotContain(".", a.SourceColumn));
    }

    #endregion

    #region ParseAggregates_EmptyList_WhenNoAggregates

    [Fact]
    public void ParseAggregates_EmptyList_WhenNoAggregates()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result =
            ViewDefinitionParser.ParseAggregates("SELECT time_bucket('1h'::interval, t.ts) AS b FROM t GROUP BY 1");

        Assert.Empty(result);
    }

    #endregion

    // ── ParseGroupByColumns ─────────────────────────────────────────────────

    #region ParseGroupByColumns_ExcludesTimeBucket

    [Fact]
    public void ParseGroupByColumns_ExcludesTimeBucket()
    {
        IReadOnlyList<string> result = ViewDefinitionParser.ParseGroupByColumns(FullViewDef);
        Assert.DoesNotContain(result, c => c.StartsWith("time_bucket", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("service_name", result);
    }

    #endregion

    #region ParseGroupByColumns_StripsTableQualifier

    [Fact]
    public void ParseGroupByColumns_StripsTableQualifier()
    {
        IReadOnlyList<string> result = ViewDefinitionParser.ParseGroupByColumns(FullViewDef);

        Assert.All(result, c => Assert.DoesNotContain(".", c));
    }

    #endregion

    #region ParseGroupByColumns_SkipsPositionalReferences

    [Fact]
    public void ParseGroupByColumns_SkipsPositionalReferences()
    {
        string viewDef =
            "SELECT time_bucket('1h'::interval, t.ts) AS b, t.zone AS zone" +
            " FROM t GROUP BY 1, t.zone";

        IReadOnlyList<string> result = ViewDefinitionParser.ParseGroupByColumns(viewDef);

        Assert.DoesNotContain(result, c => int.TryParse(c, out _));
        Assert.Contains("zone", result);
    }

    #endregion

    #region ParseGroupByColumns_EmptyList_WhenNoGroupBy

    [Fact]
    public void ParseGroupByColumns_EmptyList_WhenNoGroupBy()
    {
        IReadOnlyList<string> result = ViewDefinitionParser.ParseGroupByColumns("SELECT avg(x) AS y FROM t");

        Assert.Empty(result);
    }

    #endregion

    #region ParseGroupByColumns_StripsTrailingSemicolon

    [Fact]
    public void ParseGroupByColumns_StripsTrailingSemicolon()
    {
        string viewDef =
            "SELECT time_bucket('1h'::interval, t.ts) AS b, t.region AS region" +
            " FROM t GROUP BY time_bucket('1h'::interval, t.ts), t.region;";

        IReadOnlyList<string> result = ViewDefinitionParser.ParseGroupByColumns(viewDef);

        Assert.Single(result);
        Assert.Equal("region", result[0]);
    }

    #endregion

    // ── ParseWhereClause ────────────────────────────────────────────────────

    #region ParseWhereClause_Extracts_Content

    [Fact]
    public void ParseWhereClause_Extracts_Content()
    {
        string? result = ViewDefinitionParser.ParseWhereClause(ViewDefWithWhere);

        Assert.NotNull(result);
        Assert.Contains("device_id", result);
        Assert.DoesNotContain("GROUP BY", result, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ParseWhereClause_ReturnsNull_WhenAbsent

    [Fact]
    public void ParseWhereClause_ReturnsNull_WhenAbsent()
    {
        string? result = ViewDefinitionParser.ParseWhereClause(FullViewDef);

        Assert.Null(result);
    }

    #endregion

    // ── ParseAggregates — first/last (finalize_agg internal form) ──────────

    private const string FinalizeAggFirst =
        "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
        " _timescaledb_internal.finalize_agg('first(double precision,timestamp with time zone)'::text," +
        " NULL::name, NULL::name, x.agg_1_1, x.\"time\") AS first_temperature" +
        " FROM t GROUP BY 1";

    private const string FinalizeAggLast =
        "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
        " _timescaledb_internal.finalize_agg('last(double precision,timestamp with time zone)'::text," +
        " NULL::name, NULL::name, x.agg_2_2, x.\"time\") AS last_temperature" +
        " FROM t GROUP BY 1";

    #region ParseAggregates_FinalizeAgg_First_Produces_FirstFunction

    [Fact]
    public void ParseAggregates_FinalizeAgg_First_Produces_FirstFunction()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(FinalizeAggFirst);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "first_temperature");
        Assert.NotNull(agg);
        Assert.Equal(EAggregateFunction.First, agg.Function);
    }

    #endregion

    #region ParseAggregates_FinalizeAgg_Last_Produces_LastFunction

    [Fact]
    public void ParseAggregates_FinalizeAgg_Last_Produces_LastFunction()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(FinalizeAggLast);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "last_temperature");
        Assert.NotNull(agg);
        Assert.Equal(EAggregateFunction.Last, agg.Function);
    }

    #endregion

    #region ParseAggregates_FinalizeAgg_StripsPrefix_FromSourceColumn

    [Fact]
    public void ParseAggregates_FinalizeAgg_StripsPrefix_FromSourceColumn()
    {
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(FinalizeAggFirst);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "first_temperature");
        Assert.NotNull(agg);
        Assert.Equal("temperature", agg.SourceColumn);
    }

    #endregion

    #region ParseAggregates_FinalizeAgg_FallsBackToAlias_WhenNoPrefix

    [Fact]
    public void ParseAggregates_FinalizeAgg_FallsBackToAlias_WhenNoPrefix()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
            " _timescaledb_internal.finalize_agg('first(double precision,timestamp with time zone)'::text," +
            " NULL::name, NULL::name, x.agg_1_1, x.\"time\") AS opening_price" +
            " FROM t GROUP BY 1";

        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "opening_price");
        Assert.NotNull(agg);
        Assert.Equal(EAggregateFunction.First, agg.Function);
        Assert.Equal("opening_price", agg.SourceColumn);
    }

    #endregion

    #region ParseAggregates_Mixed_StandardAndFinalizeAgg_BothExtracted

    [Fact]
    public void ParseAggregates_Mixed_StandardAndFinalizeAgg_BothExtracted()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
            " avg(t.value) AS avg_value," +
            " _timescaledb_internal.finalize_agg('first(double precision,timestamp with time zone)'::text," +
            " NULL::name, NULL::name, x.agg_1_1, x.\"time\") AS first_value" +
            " FROM t GROUP BY 1";

        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        Assert.Contains(result, a => a.Function == EAggregateFunction.Avg && a.Alias == "avg_value");
        Assert.Contains(result, a => a.Function == EAggregateFunction.First && a.Alias == "first_value");
    }

    #endregion

    // ── ParseTimeBucketSourceColumn — quoted qualifiers and 3-arg forms ─────

    #region ParseTimeBucketSourceColumn_QuotedTableQualifier

    [Fact]
    public void ParseTimeBucketSourceColumn_QuotedTableQualifier()
    {
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, \"DeviceReadings\".\"Timestamp\") AS bucket," +
            " avg(\"DeviceReadings\".\"Value\") AS avg_value" +
            " FROM \"DeviceReadings\" GROUP BY 1";

        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn(viewDef);

        Assert.Equal("Timestamp", result);
    }

    #endregion

    #region ParseTimeBucketSourceColumn_SchemaQualified

    [Fact]
    public void ParseTimeBucketSourceColumn_SchemaQualified()
    {
        const string viewDef =
            "SELECT time_bucket('1 day'::interval, public.\"Readings\".\"Time\") AS bucket" +
            " FROM public.\"Readings\" GROUP BY 1";

        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn(viewDef);

        Assert.Equal("Time", result);
    }

    #endregion

    #region ParseTimeBucketSourceColumn_ThreeArg_Timezone

    [Fact]
    public void ParseTimeBucketSourceColumn_ThreeArg_Timezone()
    {
        // TimescaleDB >= 2.8 supports time_bucket(width, ts, timezone).
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, t.\"time\", 'Europe/Berlin'::text) AS bucket" +
            " FROM t GROUP BY 1";

        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn(viewDef);

        Assert.Equal("time", result);
    }

    #endregion

    #region ParseTimeBucketSourceColumn_ThreeArg_Origin

    [Fact]
    public void ParseTimeBucketSourceColumn_ThreeArg_Origin()
    {
        const string viewDef =
            "SELECT time_bucket('1 week'::interval, t.ts, origin => '2000-01-03 00:00:00'::timestamp without time zone) AS bucket" +
            " FROM t GROUP BY 1";

        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn(viewDef);

        Assert.Equal("ts", result);
    }

    #endregion

    #region ParseTimeBucketSourceColumn_MultiWordCast

    [Fact]
    public void ParseTimeBucketSourceColumn_MultiWordCast()
    {
        const string viewDef =
            "SELECT time_bucket('1 day'::interval, t.ts::timestamp with time zone) AS bucket" +
            " FROM t GROUP BY 1";

        string? result = ViewDefinitionParser.ParseTimeBucketSourceColumn(viewDef);

        Assert.Equal("ts", result);
    }

    #endregion

    // ── ParseAggregates — quoted aliases and plain first/last ───────────────

    #region ParseAggregates_QuotedAlias_And_QuotedColumn

    [Fact]
    public void ParseAggregates_QuotedAlias_And_QuotedColumn()
    {
        const string viewDef =
            "SELECT time_bucket('01:00:00'::interval, \"Readings\".\"Time\") AS \"Bucket\"," +
            " avg(\"Readings\".\"Temperature\") AS \"AvgTemperature\"" +
            " FROM \"Readings\" GROUP BY 1";

        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "AvgTemperature");
        Assert.NotNull(agg);
        Assert.Equal(EAggregateFunction.Avg, agg.Function);
        Assert.Equal("Temperature", agg.SourceColumn);
    }

    #endregion

    #region ParseAggregates_PlainFirst_UsesFirstArgumentAsSource

    [Fact]
    public void ParseAggregates_PlainFirst_UsesFirstArgumentAsSource()
    {
        // Finalized continuous aggregates (TimescaleDB >= 2.7, the default) store first/last in plain form.
        const string viewDef =
            "SELECT time_bucket('1 day'::interval, t.\"time\") AS bucket," +
            " first(t.temperature, t.\"time\") AS first_temperature" +
            " FROM t GROUP BY 1";

        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "first_temperature");
        Assert.NotNull(agg);
        Assert.Equal(EAggregateFunction.First, agg.Function);
        Assert.Equal("temperature", agg.SourceColumn);
    }

    #endregion

    #region ParseAggregates_PlainLast_QuotedArgumentsAndAlias

    [Fact]
    public void ParseAggregates_PlainLast_QuotedArgumentsAndAlias()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, \"Trades\".\"Timestamp\") AS \"Bucket\"," +
            " last(\"Trades\".\"Price\", \"Trades\".\"Timestamp\") AS \"LastPrice\"" +
            " FROM \"Trades\" GROUP BY 1";

        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        ViewDefinitionParser.ParsedAggregate? agg = result.FirstOrDefault(a => a.Alias == "LastPrice");
        Assert.NotNull(agg);
        Assert.Equal(EAggregateFunction.Last, agg.Function);
        Assert.Equal("Price", agg.SourceColumn);
    }

    #endregion

    #region ParseAggregates_FinalizeAgg_SkippedWhenExactParseCoversAlias

    [Fact]
    public void ParseAggregates_FinalizeAgg_SkippedWhenExactParseCoversAlias()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
            " first(t.temperature, t.\"time\") AS first_temp," +
            " _timescaledb_internal.finalize_agg('first(double precision,timestamp with time zone)'::text," +
            " NULL::name, NULL::name, x.agg_1_1, x.\"time\") AS first_temp" +
            " FROM t GROUP BY 1";

        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        ViewDefinitionParser.ParsedAggregate single = Assert.Single(result, a => a.Alias == "first_temp");
        Assert.Equal("temperature", single.SourceColumn);
    }

    #endregion

    #region ParseGroupByColumns_PreservesRawExpressions

    [Fact]
    public void ParseGroupByColumns_PreservesRawExpressions()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket, avg(t.v) AS avg_v" +
            " FROM t GROUP BY time_bucket('1 hour'::interval, t.\"time\"), EXTRACT(HOUR FROM t.\"time\"), t.region";

        IReadOnlyList<string> result = ViewDefinitionParser.ParseGroupByColumns(viewDef);

        Assert.Contains("EXTRACT(HOUR FROM t.\"time\")", result);
        Assert.Contains("region", result);
    }

    #endregion

    // ── Edge cases ─────────────────────────────────────────────────────────

    #region ParseAggregates_UnknownFunctionName_Skipped

    [Fact]
    public void ParseAggregates_UnknownFunctionName_Skipped()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
            " array_agg(t.value) AS values_array," +
            " avg(t.value) AS avg_value" +
            " FROM t GROUP BY 1";

        // Act
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        // Assert
        Assert.DoesNotContain(result, a => a.Alias == "values_array");
        Assert.Contains(result, a => a.Alias == "avg_value" && a.Function == EAggregateFunction.Avg);
    }

    #endregion

    #region ParseAggregates_First_With_Empty_FirstArg_Skipped

    [Fact]
    public void ParseAggregates_First_With_Empty_FirstArg_Skipped()
    {
        const string viewDef =
            "SELECT time_bucket('1 hour'::interval, t.\"time\") AS bucket," +
            " first(,t.\"time\") AS bad_first" +
            " FROM t GROUP BY 1";

        // Act
        IReadOnlyList<ViewDefinitionParser.ParsedAggregate> result = ViewDefinitionParser.ParseAggregates(viewDef);

        // Assert
        Assert.DoesNotContain(result, a => a.Alias == "bad_first");
    }

    #endregion

    // ── Parse (memoized full result) ────────────────────────────────────────

    #region Parse_Returns_CompleteResult_And_Memoizes

    [Fact]
    public void Parse_Returns_CompleteResult_And_Memoizes()
    {
        ViewDefinitionParser.ParsedViewDefinition first = ViewDefinitionParser.Parse(FullViewDef);
        ViewDefinitionParser.ParsedViewDefinition second = ViewDefinitionParser.Parse(FullViewDef);

        Assert.Equal("01:00:00", first.TimeBucketWidth);
        Assert.Equal("time", first.TimeBucketSourceColumn);
        Assert.Contains(first.Aggregates, a => a.Function == EAggregateFunction.Count && a.SourceColumn == "*");
        Assert.Contains("service_name", first.GroupByColumns);
        Assert.Null(first.WhereClause);
        Assert.Same(first, second);
    }

    #endregion
}

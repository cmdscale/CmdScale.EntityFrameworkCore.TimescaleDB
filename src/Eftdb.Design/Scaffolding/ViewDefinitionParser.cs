using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Parses a TimescaleDB continuous aggregate view definition to extract structured configuration.
    /// All parsing is best-effort: unrecognised patterns return null or an empty list rather than throwing.
    /// </summary>
    internal static partial class ViewDefinitionParser
    {
        internal sealed record ParsedAggregate(
            string Alias,
            EAggregateFunction Function,
            string SourceColumn);

        /// <summary>
        /// Complete parse result of a continuous aggregate view definition.
        /// </summary>
        internal sealed record ParsedViewDefinition(
            string? TimeBucketWidth,
            string? TimeBucketSourceColumn,
            string? TimeBucketAlias,
            IReadOnlyList<ParsedAggregate> Aggregates,
            IReadOnlyList<string> GroupByColumns,
            string? WhereClause);

        private static readonly ConcurrentDictionary<string, ParsedViewDefinition> Cache = new();

        /// <summary>
        /// Parses a view definition once and memoizes the result. Rendering runs per entity and per
        /// property against the same SQL; the cache avoids re-parsing the full definition each time.
        /// </summary>
        public static ParsedViewDefinition Parse(string viewDefinition)
            => Cache.GetOrAdd(viewDefinition, static vd => new ParsedViewDefinition(
                ParseTimeBucketWidth(vd),
                ParseTimeBucketSourceColumn(vd),
                ParseTimeBucketAlias(vd),
                ParseAggregates(vd),
                ParseGroupByColumns(vd),
                ParseWhereClause(vd)));

        /// <summary>
        /// Extracts the time bucket interval from a <c>time_bucket('interval'::interval, col)</c> call.
        /// Returns the raw interval string (e.g. <c>"01:00:00"</c>); callers should normalise via
        /// <see cref="IntervalParsingHelper.NormalizeInterval"/>.
        /// </summary>
        public static string? ParseTimeBucketWidth(string viewDefinition)
        {
            Match m = TimeBucketWidthRegex().Match(viewDefinition);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Extracts the source column name from the second argument of the <c>time_bucket()</c> call.
        /// Table aliases and double-quote delimiters are stripped.
        /// </summary>
        public static string? ParseTimeBucketSourceColumn(string viewDefinition)
        {
            Match m = TimeBucketSourceColumnRegex().Match(viewDefinition);
            return m.Success ? StripQuotes(m.Groups[1].Value) : null;
        }

        /// <summary>
        /// Extracts the alias the view assigns to the <c>time_bucket(...)</c> expression
        /// (the <c>AS &lt;alias&gt;</c> that becomes the view's bucket column name). Table-alias
        /// qualifiers and double-quote delimiters are stripped. Returns <c>null</c> when the
        /// bucket expression carries no explicit alias.
        /// </summary>
        public static string? ParseTimeBucketAlias(string viewDefinition)
        {
            Match m = TimeBucketAliasRegex().Match(viewDefinition);
            return m.Success ? StripQuotes(m.Groups[1].Value) : null;
        }

        /// <summary>
        /// Extracts aggregate function definitions (<c>avg</c>, <c>sum</c>, <c>min</c>, <c>max</c>,
        /// <c>count</c>, <c>first</c>, <c>last</c>) from the SELECT clause.
        /// Table-alias qualifiers and double-quote delimiters are stripped from column references and aliases.
        /// TimescaleDB's legacy internal finalized form of first/last is handled as a fallback for aliases
        /// the exact parse did not cover.
        /// </summary>
        public static IReadOnlyList<ParsedAggregate> ParseAggregates(string viewDefinition)
        {
            // Only scan the SELECT clause — stop at FROM to avoid false positives in subqueries.
            string selectClause = ExtractSelectClause(viewDefinition);

            List<ParsedAggregate> result = [];
            foreach (Match m in AggregateRegex().Matches(selectClause))
            {
                string functionName = m.Groups[1].Value.ToUpperInvariant();
                string rawArg = m.Groups[2].Value.Trim();
                string alias = StripQuotes(m.Groups[3].Value);

                EAggregateFunction? function = functionName switch
                {
                    "AVG" => EAggregateFunction.Avg,
                    "SUM" => EAggregateFunction.Sum,
                    "MIN" => EAggregateFunction.Min,
                    "MAX" => EAggregateFunction.Max,
                    "COUNT" => EAggregateFunction.Count,
                    "FIRST" => EAggregateFunction.First,
                    "LAST" => EAggregateFunction.Last,
                    _ => null
                };

                if (function is null)
                {
                    continue;
                }

                string sourceColumn;
                if (function is EAggregateFunction.First or EAggregateFunction.Last)
                {
                    // first/last take (value column, time column); the source is the FIRST argument.
                    string firstArg = SplitTopLevel(rawArg).First().Trim();
                    if (string.IsNullOrWhiteSpace(firstArg))
                    {
                        continue;
                    }

                    sourceColumn = StripQualifierAndQuotes(firstArg);
                }
                else
                {
                    // COUNT(*) wildcard is kept as-is; other args strip table qualifier and quotes.
                    sourceColumn = rawArg == "*" ? "*" : StripQualifierAndQuotes(rawArg);
                }

                result.Add(new ParsedAggregate(alias, function.Value, sourceColumn));
            }

            // Legacy fallback: before TimescaleDB 2.7 ("finalized" continuous aggregates), first()/last()
            // appear as internal finalized aggregates in the view SQL:
            // _timescaledb_internal.finalize_agg('first(double precision,...)'::text, ...) AS alias
            // The source column is not recoverable from that form; it is inferred from the alias prefix.
            HashSet<string> parsedAliases = [.. result.Select(a => a.Alias)];
            foreach (Match m in FinalizeAggRegex().Matches(selectClause))
            {
                string functionName = m.Groups[1].Value.ToUpperInvariant();
                string alias = StripQuotes(m.Groups[2].Value);
                if (parsedAliases.Contains(alias))
                {
                    continue;
                }

                EAggregateFunction function = functionName == "FIRST" ? EAggregateFunction.First : EAggregateFunction.Last;

                string prefix = functionName.ToLowerInvariant() + "_";
                string sourceColumn = alias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? alias[prefix.Length..]
                    : alias;

                result.Add(new ParsedAggregate(alias, function, sourceColumn));
            }

            return result;
        }

        /// <summary>
        /// Extracts GROUP BY column references, excluding the <c>time_bucket(...)</c> expression.
        /// Table-alias qualifiers and double-quote delimiters are stripped.
        /// </summary>
        public static IReadOnlyList<string> ParseGroupByColumns(string viewDefinition)
        {
            Match groupByMatch = GroupByRegex().Match(viewDefinition);
            if (!groupByMatch.Success)
            {
                return [];
            }

            string groupByContent = groupByMatch.Groups[1].Value.TrimEnd(';', ' ', '\t', '\r', '\n');

            List<string> columns = [];
            foreach (string token in SplitTopLevel(groupByContent))
            {
                string trimmed = token.Trim();

                // Skip the time_bucket(...) expression — it is represented by the timeBucketWidth/Column args.
                if (trimmed.StartsWith("time_bucket", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("(time_bucket", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip numeric positional references (e.g. GROUP BY 1, 2).
                if (int.TryParse(trimmed, out _))
                {
                    continue;
                }

                string stripped = SimpleColumnReferenceRegex().IsMatch(trimmed)
                    ? StripQualifierAndQuotes(trimmed)
                    : trimmed;
                if (!string.IsNullOrWhiteSpace(stripped))
                {
                    columns.Add(stripped);
                }
            }

            return columns;
        }

        /// <summary>
        /// Extracts the WHERE clause content.
        /// Returns <c>null</c> when no WHERE clause is present.
        /// </summary>
        public static string? ParseWhereClause(string viewDefinition)
        {
            Match m = WhereClauseRegex().Match(viewDefinition);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string ExtractSelectClause(string viewDefinition)
        {
            int fromIndex = FromRegex().Match(viewDefinition).Index;
            return fromIndex > 0 ? viewDefinition[..fromIndex] : viewDefinition;
        }

        private static string StripQualifierAndQuotes(string token)
        {
            int dot = token.LastIndexOf('.');
            string name = dot >= 0 ? token[(dot + 1)..] : token;
            return StripQuotes(name);
        }

        private static string StripQuotes(string name)
            => name.Length >= 2 && name[0] == '"' && name[^1] == '"'
                ? name[1..^1]
                : name;

        /// <summary>Splits a comma-separated list while respecting parenthesised subexpressions.</summary>
        private static List<string> SplitTopLevel(string input)
        {
            List<string> parts = [];
            int depth = 0;
            int start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '(') { depth++; }
                else if (c == ')') { depth--; }
                else if (c == ',' && depth == 0)
                {
                    parts.Add(input[start..i]);
                    start = i + 1;
                }
            }
            parts.Add(input[start..]);
            return parts;
        }

        [GeneratedRegex(@"time_bucket\s*\(\s*'([^']+)'", RegexOptions.IgnoreCase)]
        private static partial Regex TimeBucketWidthRegex();

        [GeneratedRegex(@"time_bucket\s*\([^,]+,\s*(?:(?:""[^""]+""|\w+)\.)*(""[^""]+""|\w+)\s*(?:::\w+(?:\s+\w+)*)?\s*[,)]", RegexOptions.IgnoreCase)]
        private static partial Regex TimeBucketSourceColumnRegex();

        [GeneratedRegex(@"time_bucket\s*\([^)]*\)\s+AS\s+(""[^""]+""|\w+)", RegexOptions.IgnoreCase)]
        private static partial Regex TimeBucketAliasRegex();

        [GeneratedRegex(@"\b(avg|sum|min|max|count|first|last)\s*\((\*|[^)]*?)\)\s+AS\s+(""[^""]+""|\w+)", RegexOptions.IgnoreCase)]
        private static partial Regex AggregateRegex();

        [GeneratedRegex(@"\bGROUP\s+BY\s+(.*?)(?:\s+HAVING\s+|\s*$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex GroupByRegex();

        [GeneratedRegex(@"\bWHERE\s+(.*?)(?:\s+GROUP\s+BY\s+|\s+HAVING\s+|\s*$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex WhereClauseRegex();

        [GeneratedRegex(@"\bFROM\b", RegexOptions.IgnoreCase)]
        private static partial Regex FromRegex();

        [GeneratedRegex(@"^(?:(?:""[^""]+""|\w+)\.)*(""[^""]+""|\w+)$")]
        private static partial Regex SimpleColumnReferenceRegex();

        [GeneratedRegex(
            @"_timescaledb_internal\.finalize_agg\s*\(\s*'(first|last)\s*\([^']*\)'(?:[^)]*)\)\s+AS\s+(""[^""]+""|\w+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex FinalizeAggRegex();
    }
}

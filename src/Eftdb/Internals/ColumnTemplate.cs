using System.Text;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Parses and formats SQL templates whose column references are <c>{n}</c> placeholders, as used by
    /// <see cref="Abstractions.ContinuousAggregateExpression"/>. Braces inside string literals and quoted
    /// identifiers are left alone; <c>{{</c> and <c>}}</c> outside them stand for literal braces.
    /// </summary>
    internal static class ColumnTemplate
    {
        /// <summary>Returns the placeholder indices in order of appearance.</summary>
        /// <exception cref="FormatException">The template has an unescaped brace that is not a placeholder.</exception>
        public static List<int> GetPlaceholderIndices(string template)
        {
            List<int> indices = [];
            Walk(template, indices.Add, (_, _) => { });
            return indices;
        }

        /// <summary>Replaces each placeholder with the text returned for its index.</summary>
        /// <exception cref="FormatException">The template has an unescaped brace that is not a placeholder.</exception>
        public static string Format(string template, Func<int, string> column)
        {
            StringBuilder builder = new(template.Length);
            Walk(template, index => builder.Append(column(index)), (start, length) => builder.Append(template, start, length));
            return builder.ToString();
        }

        /// <summary>
        /// Reports each placeholder index and each run of literal text (as start and length). An escaped brace
        /// ends the current run after its first character, so the second is skipped.
        /// </summary>
        private static void Walk(string template, Action<int> onPlaceholder, Action<int, int> onText)
        {
            int runStart = 0;
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                if (c is '\'' or '"')
                {
                    int end = template.IndexOf(c, i + 1);
                    // A doubled quote is an escaped quote inside the literal or identifier.
                    while (end >= 0 && end + 1 < template.Length && template[end + 1] == c)
                    {
                        end = template.IndexOf(c, end + 2);
                    }

                    i = end < 0 ? template.Length : end + 1;
                }
                else if (c is '{' or '}' && i + 1 < template.Length && template[i + 1] == c)
                {
                    onText(runStart, i + 1 - runStart);
                    i += 2;
                    runStart = i;
                }
                else if (c == '{')
                {
                    int close = template.IndexOf('}', i + 1);
                    if (close < 0 || !int.TryParse(template.AsSpan(i + 1, close - i - 1), out int index) || index < 0)
                    {
                        throw new FormatException($"Invalid placeholder at position {i} in SQL template '{template}'. Column references are written as {{0}}, {{1}}, …; literal braces as {{{{ and }}}}.");
                    }

                    onText(runStart, i - runStart);
                    onPlaceholder(index);
                    i = close + 1;
                    runStart = i;
                }
                else if (c == '}')
                {
                    throw new FormatException($"Unmatched '}}' at position {i} in SQL template '{template}'. Literal braces are written as {{{{ and }}}}.");
                }
                else
                {
                    i++;
                }
            }

            onText(runStart, template.Length - runStart);
        }
    }
}

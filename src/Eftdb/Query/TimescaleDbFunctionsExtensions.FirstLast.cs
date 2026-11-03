using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query;

public static partial class TimescaleDbFunctionsExtensions
{
    /// <summary>
    /// Returns the value of the row with the smallest time in the group, or <see langword="null"/> when that value is null.
    /// Pass the value and the ordering column as a <see cref="ValueTuple.Create{T1, T2}(T1, T2)"/> pair, for example
    /// <c>EF.Functions.First(g.Select(x =&gt; ValueTuple.Create(x.Value, x.Timestamp)))</c>.
    /// Translates to <c>first(value, time)</c>.
    /// </summary>
    public static TValue First<TValue, TTime>(this DbFunctions _, IEnumerable<(TValue Value, TTime Time)> values) => Throw<TValue>();

    /// <summary>
    /// Returns the value of the row with the largest time in the group, or <see langword="null"/> when that value is null.
    /// Pass the value and the ordering column as a <see cref="ValueTuple.Create{T1, T2}(T1, T2)"/> pair, for example
    /// <c>EF.Functions.Last(g.Select(x =&gt; ValueTuple.Create(x.Value, x.Timestamp)))</c>.
    /// Translates to <c>last(value, time)</c>.
    /// </summary>
    public static TValue Last<TValue, TTime>(this DbFunctions _, IEnumerable<(TValue Value, TTime Time)> values) => Throw<TValue>();
}

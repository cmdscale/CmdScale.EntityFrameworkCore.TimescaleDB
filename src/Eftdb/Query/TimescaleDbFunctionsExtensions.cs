using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query;

/// <summary>
/// Provides extension methods on <see cref="DbFunctions"/> that translate to TimescaleDB SQL functions.
/// These methods are for use with Entity Framework Core LINQ queries only and have no in-memory implementation.
/// </summary>
public static partial class TimescaleDbFunctionsExtensions
{
    private static T Throw<T>() =>
        throw new InvalidOperationException(
            "This method is for use with Entity Framework Core LINQ queries only and has no in-memory implementation.");
}

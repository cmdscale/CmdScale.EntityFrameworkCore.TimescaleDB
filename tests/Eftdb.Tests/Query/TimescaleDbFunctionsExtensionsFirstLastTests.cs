using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Query;

public class TimescaleDbFunctionsExtensionsFirstLastTests
{
    private static readonly IEnumerable<(double Value, DateTime Time)> Values = [(1.0, DateTime.UnixEpoch)];

    #region Should_Throw_When_First_Is_Called_Outside_A_Query

    [Fact]
    public void Should_Throw_When_First_Is_Called_Outside_A_Query()
    {
        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EF.Functions.First(Values));

        // Assert
        Assert.Contains("no in-memory implementation", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Last_Is_Called_Outside_A_Query

    [Fact]
    public void Should_Throw_When_Last_Is_Called_Outside_A_Query()
    {
        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EF.Functions.Last(Values));

        // Assert
        Assert.Contains("no in-memory implementation", exception.Message);
    }

    #endregion
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

public class DimensionAttributeTests
{
    #region Range_Constructor_Throws_For_Null_ColumnName

    [Fact]
    public void Range_Constructor_Throws_For_Null_ColumnName()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute(null!, EDimensionType.Range, "1 day"));
    }

    #endregion

    #region Range_Constructor_Throws_For_Whitespace_ColumnName

    [Fact]
    public void Range_Constructor_Throws_For_Whitespace_ColumnName()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("  ", EDimensionType.Range, "1 day"));
    }

    #endregion

    #region Range_Constructor_Throws_For_Hash_Type

    [Fact]
    public void Range_Constructor_Throws_For_Hash_Type()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("Col", EDimensionType.Hash, "1 day"));
    }

    #endregion

    #region Range_Constructor_Throws_For_Null_Interval

    [Fact]
    public void Range_Constructor_Throws_For_Null_Interval()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("Col", EDimensionType.Range, null!));
    }

    #endregion

    #region Range_Constructor_Throws_For_Whitespace_Interval

    [Fact]
    public void Range_Constructor_Throws_For_Whitespace_Interval()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("Col", EDimensionType.Range, "  "));
    }

    #endregion

    #region Range_Constructor_Sets_Properties

    [Fact]
    public void Range_Constructor_Sets_Properties()
    {
        DimensionAttribute attr = new("MyColumn", EDimensionType.Range, "1 month");

        Assert.Equal("MyColumn", attr.ColumnName);
        Assert.Equal(EDimensionType.Range, attr.Type);
        Assert.Equal("1 month", attr.Interval);
        Assert.Equal(0, attr.NumberOfPartitions);
    }

    #endregion

    #region Hash_Constructor_Throws_For_Null_ColumnName

    [Fact]
    public void Hash_Constructor_Throws_For_Null_ColumnName()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute(null!, EDimensionType.Hash, 4));
    }

    #endregion

    #region Hash_Constructor_Throws_For_Whitespace_ColumnName

    [Fact]
    public void Hash_Constructor_Throws_For_Whitespace_ColumnName()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("  ", EDimensionType.Hash, 4));
    }

    #endregion

    #region Hash_Constructor_Throws_For_Range_Type

    [Fact]
    public void Hash_Constructor_Throws_For_Range_Type()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("Col", EDimensionType.Range, 4));
    }

    #endregion

    #region Hash_Constructor_Throws_For_Zero_NumberOfPartitions

    [Fact]
    public void Hash_Constructor_Throws_For_Zero_NumberOfPartitions()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("Col", EDimensionType.Hash, 0));
    }

    #endregion

    #region Hash_Constructor_Throws_For_Negative_NumberOfPartitions

    [Fact]
    public void Hash_Constructor_Throws_For_Negative_NumberOfPartitions()
    {
        Assert.Throws<ArgumentException>(() => new DimensionAttribute("Col", EDimensionType.Hash, -1));
    }

    #endregion

    #region Hash_Constructor_Sets_Properties

    [Fact]
    public void Hash_Constructor_Sets_Properties()
    {
        DimensionAttribute attr = new("MyColumn", EDimensionType.Hash, 8);

        Assert.Equal("MyColumn", attr.ColumnName);
        Assert.Equal(EDimensionType.Hash, attr.Type);
        Assert.Equal(8, attr.NumberOfPartitions);
        Assert.Null(attr.Interval);
    }

    #endregion
}

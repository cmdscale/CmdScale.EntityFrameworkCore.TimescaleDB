using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Abstractions;

/// <summary>
/// Tests that verify OrderBy classes, builders, and extension methods.
/// </summary>
public class OrderByTests
{
    #region Test Entity

    private class TestEntity
    {
        public DateTime Time { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    #endregion

    #region OrderByExtensions Tests

    [Fact]
    public void Ascending_Should_Create_OrderBy_With_IsAscending_True()
    {
        // Arrange
        string columnName = "timestamp";

        // Act
        OrderBy result = columnName.Ascending();

        // Assert
        Assert.Equal("timestamp", result.ColumnName);
        Assert.True(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void Ascending_With_NullsFirst_True_Should_Set_NullsFirst()
    {
        // Arrange
        string columnName = "value";

        // Act
        OrderBy result = columnName.Ascending(nullsFirst: true);

        // Assert
        Assert.Equal("value", result.ColumnName);
        Assert.True(result.IsAscending);
        Assert.True(result.NullsFirst);
    }

    [Fact]
    public void Ascending_With_NullsFirst_False_Should_Set_NullsFirst()
    {
        // Arrange
        string columnName = "device_id";

        // Act
        OrderBy result = columnName.Ascending(nullsFirst: false);

        // Assert
        Assert.Equal("device_id", result.ColumnName);
        Assert.True(result.IsAscending);
        Assert.False(result.NullsFirst);
    }

    [Fact]
    public void Descending_Should_Create_OrderBy_With_IsAscending_False()
    {
        // Arrange
        string columnName = "timestamp";

        // Act
        OrderBy result = columnName.Descending();

        // Assert
        Assert.Equal("timestamp", result.ColumnName);
        Assert.False(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void Descending_With_NullsFirst_True_Should_Set_NullsFirst()
    {
        // Arrange
        string columnName = "value";

        // Act
        OrderBy result = columnName.Descending(nullsFirst: true);

        // Assert
        Assert.Equal("value", result.ColumnName);
        Assert.False(result.IsAscending);
        Assert.True(result.NullsFirst);
    }

    [Fact]
    public void Descending_With_NullsFirst_False_Should_Set_NullsFirst()
    {
        // Arrange
        string columnName = "device_id";

        // Act
        OrderBy result = columnName.Descending(nullsFirst: false);

        // Assert
        Assert.Equal("device_id", result.ColumnName);
        Assert.False(result.IsAscending);
        Assert.False(result.NullsFirst);
    }

    #endregion

    #region OrderByConfiguration<T> Tests

    [Fact]
    public void OrderByConfiguration_Default_Should_Create_OrderBy_With_Null_IsAscending()
    {
        // Arrange
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.Time);

        // Act
        OrderBy result = config.Default();

        // Assert
        Assert.Equal("Time", result.ColumnName);
        Assert.Null(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void OrderByConfiguration_Default_With_NullsFirst_True_Should_Set_NullsFirst()
    {
        // Arrange
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.Value);

        // Act
        OrderBy result = config.Default(nullsFirst: true);

        // Assert
        Assert.Equal("Value", result.ColumnName);
        Assert.Null(result.IsAscending);
        Assert.True(result.NullsFirst);
    }

    [Fact]
    public void OrderByConfiguration_Default_With_NullsFirst_False_Should_Set_NullsFirst()
    {
        // Arrange
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.DeviceId);

        // Act
        OrderBy result = config.Default(nullsFirst: false);

        // Assert
        Assert.Equal("DeviceId", result.ColumnName);
        Assert.Null(result.IsAscending);
        Assert.False(result.NullsFirst);
    }

    [Fact]
    public void OrderByConfiguration_Ascending_Should_Create_OrderBy_With_IsAscending_True()
    {
        // Arrange
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.Time);

        // Act
        OrderBy result = config.Ascending();

        // Assert
        Assert.Equal("Time", result.ColumnName);
        Assert.True(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void OrderByConfiguration_Descending_Should_Create_OrderBy_With_IsAscending_False()
    {
        // Arrange
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.Value);

        // Act
        OrderBy result = config.Descending();

        // Assert
        Assert.Equal("Value", result.ColumnName);
        Assert.False(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void OrderByConfiguration_With_Invalid_Expression_Should_Throw_ArgumentException()
    {
        // Arrange & Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            OrderByBuilder.For<TestEntity>(x => x.Time.ToString()));

        Assert.Contains("Invalid expression", ex.Message);
    }

    #endregion

    #region OrderBySelector<T> Tests

    [Fact]
    public void OrderBySelector_By_Should_Create_OrderBy_With_Null_IsAscending()
    {
        // Arrange
        OrderBySelector<TestEntity> selector = new();

        // Act
        OrderBy result = selector.By(x => x.Time);

        // Assert
        Assert.Equal("Time", result.ColumnName);
        Assert.Null(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void OrderBySelector_By_With_NullsFirst_True_Should_Set_NullsFirst()
    {
        // Arrange
        OrderBySelector<TestEntity> selector = new();

        // Act
        OrderBy result = selector.By(x => x.DeviceId, nullsFirst: true);

        // Assert
        Assert.Equal("DeviceId", result.ColumnName);
        Assert.Null(result.IsAscending);
        Assert.True(result.NullsFirst);
    }

    [Fact]
    public void OrderBySelector_ByAscending_Should_Create_OrderBy_With_IsAscending_True()
    {
        // Arrange
        OrderBySelector<TestEntity> selector = new();

        // Act
        OrderBy result = selector.ByAscending(x => x.Value);

        // Assert
        Assert.Equal("Value", result.ColumnName);
        Assert.True(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void OrderBySelector_ByDescending_Should_Create_OrderBy_With_IsAscending_False()
    {
        // Arrange
        OrderBySelector<TestEntity> selector = new();

        // Act
        OrderBy result = selector.ByDescending(x => x.Time);

        // Assert
        Assert.Equal("Time", result.ColumnName);
        Assert.False(result.IsAscending);
        Assert.Null(result.NullsFirst);
    }

    [Fact]
    public void OrderBySelector_With_Invalid_Expression_Should_Throw_ArgumentException()
    {
        // Arrange
        OrderBySelector<TestEntity> selector = new();

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            selector.By(x => x.Time.ToString()));

        Assert.Contains("Expression must be a property access", ex.Message);
    }

    #endregion

    #region OrderBy.ToSql() Tests - All 9 Combinations

    [Theory]
    [InlineData(null, null, "timestamp")]
    [InlineData(null, true, "timestamp NULLS FIRST")]
    [InlineData(null, false, "timestamp NULLS LAST")]
    [InlineData(true, null, "timestamp ASC")]
    [InlineData(true, true, "timestamp ASC NULLS FIRST")]
    [InlineData(true, false, "timestamp ASC NULLS LAST")]
    [InlineData(false, null, "timestamp DESC")]
    [InlineData(false, true, "timestamp DESC NULLS FIRST")]
    [InlineData(false, false, "timestamp DESC NULLS LAST")]
    public void ToSql_Should_Generate_Correct_SQL_For_All_Combinations(
        bool? isAscending,
        bool? nullsFirst,
        string expectedSql)
    {
        // Arrange
        OrderBy orderBy = new("timestamp", isAscending, nullsFirst);

        // Act
        string sql = orderBy.ToSql();

        // Assert
        Assert.Equal(expectedSql, sql);
    }

    [Fact]
    public void ToSql_With_Complex_Column_Name_Should_Preserve_It()
    {
        // Arrange
        OrderBy orderBy = new("\"my_schema\".\"my_table\".\"my_column\"", true, false);

        // Act
        string sql = orderBy.ToSql();

        // Assert
        Assert.Equal("\"my_schema\".\"my_table\".\"my_column\" ASC NULLS LAST", sql);
    }

    #endregion

    #region OrderByBuilder Tests

    [Fact]
    public void OrderByBuilder_For_Should_Create_OrderByConfiguration()
    {
        // Arrange & Act
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.Time);
        OrderBy result = config.Ascending();

        // Assert
        Assert.Equal("Time", result.ColumnName);
        Assert.True(result.IsAscending);
    }

    [Fact]
    public void OrderByBuilder_For_Should_Handle_ValueType_Property()
    {
        // Arrange & Act
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.Value);
        OrderBy result = config.Descending(nullsFirst: true);

        // Assert
        Assert.Equal("Value", result.ColumnName);
        Assert.False(result.IsAscending);
        Assert.True(result.NullsFirst);
    }

    [Fact]
    public void OrderByBuilder_For_Should_Handle_ReferenceType_Property()
    {
        // Arrange & Act
        OrderByConfiguration<TestEntity> config = OrderByBuilder.For<TestEntity>(x => x.DeviceId);
        OrderBy result = config.Ascending(nullsFirst: false);

        // Assert
        Assert.Equal("DeviceId", result.ColumnName);
        Assert.True(result.IsAscending);
        Assert.False(result.NullsFirst);
    }

    #endregion

    #region OrderBy Constructor Tests

    [Fact]
    public void OrderBy_Constructor_Should_Set_All_Properties()
    {
        // Arrange & Act
        OrderBy orderBy = new("my_column", true, false);

        // Assert
        Assert.Equal("my_column", orderBy.ColumnName);
        Assert.True(orderBy.IsAscending);
        Assert.False(orderBy.NullsFirst);
    }

    [Fact]
    public void OrderBy_Constructor_With_Nulls_Should_Set_Null_Properties()
    {
        // Arrange & Act
        OrderBy orderBy = new("another_column", null, null);

        // Assert
        Assert.Equal("another_column", orderBy.ColumnName);
        Assert.Null(orderBy.IsAscending);
        Assert.Null(orderBy.NullsFirst);
    }

    [Fact]
    public void OrderBy_Constructor_With_Only_ColumnName_Should_Use_Defaults()
    {
        // Arrange & Act
        OrderBy orderBy = new("simple_column");

        // Assert
        Assert.Equal("simple_column", orderBy.ColumnName);
        Assert.Null(orderBy.IsAscending);
        Assert.Null(orderBy.NullsFirst);
    }

    #endregion
}

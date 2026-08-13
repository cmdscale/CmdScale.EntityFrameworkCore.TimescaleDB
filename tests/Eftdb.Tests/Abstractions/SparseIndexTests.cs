using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Abstractions;

public class SparseIndexTests
{
    // ── SparseIndex.ToSql() ──

    #region ToSql_Bloom_Single_Column

    [Fact]
    public void ToSql_Bloom_Single_Column_Emits_Canonical_Form()
    {
        // Arrange
        SparseIndex index = new(ESparseIndexType.Bloom, ["device_id"]);

        // Act
        string sql = index.ToSql();

        // Assert
        Assert.Equal("bloom(device_id)", sql);
    }

    #endregion

    #region ToSql_Bloom_Composite_Columns_No_Spaces

    [Fact]
    public void ToSql_Bloom_Composite_Columns_Emits_No_Spaces_In_Args()
    {
        // Arrange
        SparseIndex index = new(ESparseIndexType.Bloom, ["device_id", "tenant_id"]);

        // Act
        string sql = index.ToSql();

        // Assert
        Assert.Equal("bloom(device_id,tenant_id)", sql);
    }

    #endregion

    #region ToSql_MinMax_Single_Column

    [Fact]
    public void ToSql_MinMax_Single_Column_Emits_Canonical_Form()
    {
        // Arrange
        SparseIndex index = new(ESparseIndexType.MinMax, ["temperature"]);

        // Act
        string sql = index.ToSql();

        // Assert
        Assert.Equal("minmax(temperature)", sql);
    }

    #endregion

    #region ToSql_Preserves_Column_Name_Casing

    [Fact]
    public void ToSql_Preserves_Column_Name_Casing()
    {
        // Arrange
        SparseIndex index = new(ESparseIndexType.Bloom, ["DeviceId"]);

        // Act
        string sql = index.ToSql();

        // Assert
        Assert.Equal("bloom(DeviceId)", sql);
    }

    #endregion

    // ── SparseIndex constructor validation ──

    #region Constructor_Throws_When_No_Columns

    [Fact]
    public void Constructor_Throws_When_Columns_Is_Empty()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new SparseIndex(ESparseIndexType.Bloom, []));
    }

    #endregion

    #region Constructor_Throws_When_Columns_Is_Null

    [Fact]
    public void Constructor_Throws_When_Columns_Is_Null()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new SparseIndex(ESparseIndexType.MinMax, null!));
    }

    #endregion

    // ── SparseIndexSelector<T> ──

    #region Selector_Bloom_Extracts_Property_Name

    private class SelectorTestEntity { public int DeviceId { get; set; } public double Value { get; set; } }

    [Fact]
    public void Selector_Bloom_Extracts_Property_Name_From_Simple_Lambda()
    {
        // Arrange
        SparseIndexSelector<SelectorTestEntity> selector = new();

        // Act
        SparseIndex index = selector.Bloom(x => x.DeviceId);

        // Assert
        Assert.Equal(ESparseIndexType.Bloom, index.Kind);
        Assert.Equal(["DeviceId"], index.Columns);
    }

    #endregion

    #region Selector_Bloom_Composite_Extracts_All_Property_Names

    [Fact]
    public void Selector_Bloom_Composite_Extracts_All_Property_Names()
    {
        // Arrange
        SparseIndexSelector<SelectorTestEntity> selector = new();

        // Act
        SparseIndex index = selector.Bloom(x => x.DeviceId, x => x.Value);

        // Assert
        Assert.Equal(ESparseIndexType.Bloom, index.Kind);
        Assert.Equal(["DeviceId", "Value"], index.Columns);
    }

    #endregion

    #region Selector_MinMax_Extracts_Property_Name

    [Fact]
    public void Selector_MinMax_Extracts_Property_Name_From_Simple_Lambda()
    {
        // Arrange
        SparseIndexSelector<SelectorTestEntity> selector = new();

        // Act
        SparseIndex index = selector.MinMax(x => x.Value);

        // Assert
        Assert.Equal(ESparseIndexType.MinMax, index.Kind);
        Assert.Equal(["Value"], index.Columns);
    }

    #endregion

    #region Selector_Bloom_Throws_When_No_Expressions

    [Fact]
    public void Selector_Bloom_Throws_When_No_Expressions_Supplied()
    {
        // Arrange
        SparseIndexSelector<SelectorTestEntity> selector = new();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => selector.Bloom([]));
    }

    #endregion

    #region Selector_Bloom_ToSql_Matches_Canonical_Form

    [Fact]
    public void Selector_Bloom_ToSql_Matches_Canonical_Form()
    {
        // Arrange
        SparseIndexSelector<SelectorTestEntity> selector = new();
        SparseIndex index = selector.Bloom(x => x.DeviceId, x => x.Value);

        // Act
        string sql = index.ToSql();

        // Assert
        Assert.Equal("bloom(DeviceId,Value)", sql);
    }

    #endregion

    #region Selector_MinMax_ToSql_Matches_Canonical_Form

    [Fact]
    public void Selector_MinMax_ToSql_Matches_Canonical_Form()
    {
        // Arrange
        SparseIndexSelector<SelectorTestEntity> selector = new();
        SparseIndex index = selector.MinMax(x => x.Value);

        // Act
        string sql = index.ToSql();

        // Assert
        Assert.Equal("minmax(Value)", sql);
    }

    #endregion

    #region ToSql_Unknown_Kind_Throws_InvalidOperationException

    [Fact]
    public void ToSql_Unknown_Kind_Throws_InvalidOperationException()
    {
        // Arrange
        SparseIndex index = new((ESparseIndexType)99, ["col"]);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => index.ToSql());
    }

    #endregion
}

// ── SparseIndexAttribute ──────────────────────────────────────────────────────

public class SparseIndexAttributeTests
{
    #region Constructor_Throws_When_Columns_Is_Empty

    [Fact]
    public void Constructor_Throws_When_Columns_Is_Empty()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(
            () => new SparseIndexAttribute(ESparseIndexType.Bloom));
    }

    #endregion

    #region Constructor_Throws_When_Columns_Is_Null

    [Fact]
    public void Constructor_Throws_When_Columns_Is_Null()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(
            () => new SparseIndexAttribute(ESparseIndexType.Bloom, null!));
    }

    #endregion

    #region Constructor_Stores_Kind_And_Columns

    [Fact]
    public void Constructor_Stores_Kind_And_Columns()
    {
        // Arrange & Act
        SparseIndexAttribute attr = new(ESparseIndexType.MinMax, "device_id");

        // Assert
        Assert.Equal(ESparseIndexType.MinMax, attr.Kind);
        Assert.Equal(["device_id"], attr.Columns);
    }

    #endregion
}

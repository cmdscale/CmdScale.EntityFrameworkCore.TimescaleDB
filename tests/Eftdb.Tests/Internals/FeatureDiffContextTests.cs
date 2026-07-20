using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Pure unit tests for <see cref="FeatureDiffContext"/>: the rename-resolution helpers and the
/// <see cref="FeatureDiffContext.Empty"/> identity behaviour.
/// </summary>
public class FeatureDiffContextTests
{
    #region ResolveTable

    [Fact]
    public void ResolveTable_Returns_Mapped_Value_When_Rename_Exists()
    {
        // Arrange
        FeatureDiffContext context = new()
        {
            TableRenames = new Dictionary<(string, string), (string, string)>
            {
                [("public", "old_metrics")] = ("public", "new_metrics"),
            },
        };

        // Act
        (string Schema, string Name) result = context.ResolveTable("public", "old_metrics");

        // Assert
        Assert.Equal("public", result.Schema);
        Assert.Equal("new_metrics", result.Name);
    }

    [Fact]
    public void ResolveTable_Returns_Identity_When_No_Rename()
    {
        // Arrange
        FeatureDiffContext context = new();

        // Act
        (string Schema, string Name) result = context.ResolveTable("public", "metrics");

        // Assert
        Assert.Equal("public", result.Schema);
        Assert.Equal("metrics", result.Name);
    }

    [Fact]
    public void ResolveTable_Can_Map_To_Different_Schema()
    {
        // Arrange
        FeatureDiffContext context = new()
        {
            TableRenames = new Dictionary<(string, string), (string, string)>
            {
                [("public", "metrics")] = ("analytics", "metrics"),
            },
        };

        // Act
        (string Schema, string Name) result = context.ResolveTable("public", "metrics");

        // Assert
        Assert.Equal("analytics", result.Schema);
        Assert.Equal("metrics", result.Name);
    }

    #endregion

    #region ResolveIndex

    [Fact]
    public void ResolveIndex_Returns_Mapped_Value_When_Rename_Exists()
    {
        // Arrange
        FeatureDiffContext context = new()
        {
            IndexRenames = new Dictionary<(string, string), (string, string)>
            {
                [("public", "old_idx")] = ("public", "new_idx"),
            },
        };

        // Act
        (string Schema, string Name) result = context.ResolveIndex("public", "old_idx");

        // Assert
        Assert.Equal("public", result.Schema);
        Assert.Equal("new_idx", result.Name);
    }

    [Fact]
    public void ResolveIndex_Returns_Identity_When_No_Rename()
    {
        // Arrange
        FeatureDiffContext context = new();

        // Act
        (string Schema, string Name) result = context.ResolveIndex("public", "metrics_idx");

        // Assert
        Assert.Equal("public", result.Schema);
        Assert.Equal("metrics_idx", result.Name);
    }

    #endregion

    #region ResolveColumn

    [Fact]
    public void ResolveColumn_Returns_Mapped_Name_When_Rename_Exists()
    {
        // Arrange
        FeatureDiffContext context = new()
        {
            ColumnRenames = new Dictionary<(string, string, string), string>
            {
                [("public", "metrics", "old_value")] = "new_value",
            },
        };

        // Act
        string result = context.ResolveColumn("public", "metrics", "old_value");

        // Assert
        Assert.Equal("new_value", result);
    }

    [Fact]
    public void ResolveColumn_Returns_Identity_When_No_Rename()
    {
        // Arrange
        FeatureDiffContext context = new();

        // Act
        string result = context.ResolveColumn("public", "metrics", "value");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact]
    public void ResolveColumn_Keys_On_Post_Rename_Table_Name()
    {
        // Per the XML remark, the column-rename map is keyed by the POST-rename table name because EF Core
        // emits RenameColumnOperation against the already-renamed table. Resolving with the new table name
        // succeeds; resolving with the old table name does not.

        // Arrange
        FeatureDiffContext context = new()
        {
            ColumnRenames = new Dictionary<(string, string, string), string>
            {
                [("public", "new_metrics", "old_value")] = "new_value",
            },
        };

        // Act
        string resolvedWithNewTable = context.ResolveColumn("public", "new_metrics", "old_value");
        string resolvedWithOldTable = context.ResolveColumn("public", "old_metrics", "old_value");

        // Assert
        Assert.Equal("new_value", resolvedWithNewTable);
        Assert.Equal("old_value", resolvedWithOldTable); // identity: old table name is not a key
    }

    #endregion

    #region Empty

    [Fact]
    public void Empty_Resolves_Every_Input_To_Itself()
    {
        // Arrange
        FeatureDiffContext context = FeatureDiffContext.Empty;

        // Act & Assert
        Assert.Equal(("public", "metrics"), context.ResolveTable("public", "metrics"));
        Assert.Equal(("public", "metrics_idx"), context.ResolveIndex("public", "metrics_idx"));
        Assert.Equal("value", context.ResolveColumn("public", "metrics", "value"));
    }

    [Fact]
    public void Empty_Has_No_Recreated_Aggregates()
    {
        // Arrange
        FeatureDiffContext context = FeatureDiffContext.Empty;

        // Act & Assert
        Assert.Empty(context.RecreatedAggregates);
    }

    #endregion

    #region Schema normalization

    [Fact]
    public void ResolveTable_Requires_Normalized_Schema_Key()
    {
        // The maps store concrete (normalized) schemas. Callers must normalize a missing schema to
        // DefaultValues.DefaultSchema before building/querying. This test documents that resolving with the
        // normalized schema key works, while a null/blank key misses the entry and resolves to identity.

        // Arrange
        FeatureDiffContext context = new()
        {
            TableRenames = new Dictionary<(string, string), (string, string)>
            {
                [(DefaultValues.DefaultSchema, "old_metrics")] = (DefaultValues.DefaultSchema, "new_metrics"),
            },
        };

        // Act
        (string Schema, string Name) resolvedWithNormalizedKey = context.ResolveTable(DefaultValues.DefaultSchema, "old_metrics");
        (string Schema, string Name) resolvedWithBlankKey = context.ResolveTable("", "old_metrics");

        // Assert
        Assert.Equal((DefaultValues.DefaultSchema, "new_metrics"), resolvedWithNormalizedKey);
        Assert.Equal(("", "old_metrics"), resolvedWithBlankKey); // blank schema is not the stored key -> identity
    }

    #endregion
}

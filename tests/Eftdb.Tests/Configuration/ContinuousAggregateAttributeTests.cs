using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Configuration;

/// <summary>
/// Tests that verify the tri-state CreateGroupIndexes tracking on ContinuousAggregateAttribute:
/// the public getter falls back to the server default (true) while the internal
/// CreateGroupIndexesConfigured view only reports explicitly assigned values.
/// </summary>
public class ContinuousAggregateAttributeTests
{
    [Fact]
    public void CreateGroupIndexes_WhenUnset_ReturnsServerDefaultTrue()
    {
        // Arrange
        ContinuousAggregateAttribute attr = new();

        // Act & Assert
        Assert.True(attr.CreateGroupIndexes);
    }

    [Fact]
    public void CreateGroupIndexesConfigured_WhenUnset_IsNull()
    {
        // Arrange
        ContinuousAggregateAttribute attr = new();

        // Act & Assert
        Assert.Null(attr.CreateGroupIndexesConfigured);
    }

    [Fact]
    public void CreateGroupIndexes_SetToTrue_ReturnsTrueAndRecordsAssignment()
    {
        // Arrange
        ContinuousAggregateAttribute attr = new()
        {
            // Act
            CreateGroupIndexes = true
        };

        // Assert
        Assert.True(attr.CreateGroupIndexes);
        Assert.True(attr.CreateGroupIndexesConfigured);
    }

    [Fact]
    public void CreateGroupIndexes_SetToFalse_ReturnsFalseAndRecordsAssignment()
    {
        // Arrange
        ContinuousAggregateAttribute attr = new()
        {
            // Act
            CreateGroupIndexes = false
        };

        // Assert
        Assert.False(attr.CreateGroupIndexes);
        Assert.False(attr.CreateGroupIndexesConfigured);
    }
}

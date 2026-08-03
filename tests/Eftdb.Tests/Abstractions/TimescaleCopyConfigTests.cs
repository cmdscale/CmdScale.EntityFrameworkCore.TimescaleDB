using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using NpgsqlTypes;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Abstractions;

/// <summary>
/// Unit tests for <c>TimescaleCopyConfig&lt;T&gt;</c> auto-discovery and type-mapping behaviour.
/// </summary>
public class TimescaleCopyConfigTests
{
    #region Constructor_ExcludesProperty_When_Type_Has_No_NpgsqlDbType_Mapping

    private class EntityWithUnsupportedType
    {
        public int KnownProp { get; set; }
        public Uri? UnknownProp { get; set; }
    }

    [Fact]
    public void Constructor_ExcludesProperty_When_Type_Has_No_NpgsqlDbType_Mapping()
    {
        // Arrange & Act
        TimescaleCopyConfig<EntityWithUnsupportedType> config = new();

        // Assert
        Assert.True(config.ColumnMappings.ContainsKey(nameof(EntityWithUnsupportedType.KnownProp)));
        Assert.False(config.ColumnMappings.ContainsKey(nameof(EntityWithUnsupportedType.UnknownProp)));
    }

    #endregion

    #region Constructor_MapsKnownTypes_ToCorrectNpgsqlDbTypes

    private class EntityWithKnownTypes
    {
        public short ShortProp { get; set; }
        public int IntProp { get; set; }
        public long LongProp { get; set; }
        public float FloatProp { get; set; }
        public double DoubleProp { get; set; }
        public decimal DecimalProp { get; set; }
        public string StringProp { get; set; } = "";
        public DateTime DateTimeProp { get; set; }
        public bool BoolProp { get; set; }
        public Guid GuidProp { get; set; }
    }

    [Fact]
    public void Constructor_MapsKnownTypes_ToCorrectNpgsqlDbTypes()
    {
        // Arrange & Act
        TimescaleCopyConfig<EntityWithKnownTypes> config = new();

        // Assert
        Assert.Equal(NpgsqlDbType.Smallint, config.ColumnMappings[nameof(EntityWithKnownTypes.ShortProp)].DbType);
        Assert.Equal(NpgsqlDbType.Integer, config.ColumnMappings[nameof(EntityWithKnownTypes.IntProp)].DbType);
        Assert.Equal(NpgsqlDbType.Bigint, config.ColumnMappings[nameof(EntityWithKnownTypes.LongProp)].DbType);
        Assert.Equal(NpgsqlDbType.Real, config.ColumnMappings[nameof(EntityWithKnownTypes.FloatProp)].DbType);
        Assert.Equal(NpgsqlDbType.Double, config.ColumnMappings[nameof(EntityWithKnownTypes.DoubleProp)].DbType);
        Assert.Equal(NpgsqlDbType.Numeric, config.ColumnMappings[nameof(EntityWithKnownTypes.DecimalProp)].DbType);
        Assert.Equal(NpgsqlDbType.Text, config.ColumnMappings[nameof(EntityWithKnownTypes.StringProp)].DbType);
        Assert.Equal(NpgsqlDbType.TimestampTz, config.ColumnMappings[nameof(EntityWithKnownTypes.DateTimeProp)].DbType);
        Assert.Equal(NpgsqlDbType.Boolean, config.ColumnMappings[nameof(EntityWithKnownTypes.BoolProp)].DbType);
        Assert.Equal(NpgsqlDbType.Uuid, config.ColumnMappings[nameof(EntityWithKnownTypes.GuidProp)].DbType);
    }

    #endregion

    #region Constructor_MapsNullableType_UsingUnderlyingType

    private class EntityWithNullable
    {
        public int? NullableInt { get; set; }
        public DateTime? NullableDateTime { get; set; }
    }

    [Fact]
    public void Constructor_MapsNullableType_UsingUnderlyingType()
    {
        // Arrange & Act
        TimescaleCopyConfig<EntityWithNullable> config = new();

        // Assert
        Assert.Equal(NpgsqlDbType.Integer, config.ColumnMappings[nameof(EntityWithNullable.NullableInt)].DbType);
        Assert.Equal(NpgsqlDbType.TimestampTz, config.ColumnMappings[nameof(EntityWithNullable.NullableDateTime)].DbType);
    }

    #endregion
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.MigrationExtensions
{
    /// <summary>
    /// Unit tests for the typed <c>CreateHypertable</c>/<c>AlterHypertable</c> migration
    /// builder extensions. Each test verifies argument-to-operation mapping and null
    /// coalescing behavior.
    /// </summary>
    public class HypertableMigrationExtensionsTests
    {
        #region CreateHypertable_MapsAllArguments

        [Fact]
        public void CreateHypertable_MapsAllArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            List<Dimension> dims = [Dimension.CreateRange("col", "1 day")];

            // Act
            OperationBuilder<CreateHypertableOperation> result = mb.CreateHypertable(
                tableName: "sensor_data",
                timeColumnName: "ts",
                schema: "public",
                chunkTimeInterval: "1 day",
                enableCompression: true,
                migrateData: true,
                chunkSkipColumns: ["a", "b"],
                additionalDimensions: dims,
                compressionSegmentBy: ["device_id"],
                compressionOrderBy: ["ts"]);

            // Assert
            CreateHypertableOperation op = Assert.IsType<CreateHypertableOperation>(Assert.Single(mb.Operations));
            Assert.NotNull(result);
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("ts", op.TimeColumnName);
            Assert.Equal("public", op.Schema);
            Assert.Equal("1 day", op.ChunkTimeInterval);
            Assert.True(op.EnableCompression);
            Assert.True(op.MigrateData);
            Assert.Equal(["a", "b"], op.ChunkSkipColumns);
            Assert.Same(dims, op.AdditionalDimensions);
            Assert.Equal(["device_id"], op.CompressionSegmentBy);
            Assert.Equal(["ts"], op.CompressionOrderBy);
        }

        #endregion

        #region CreateHypertable_NullSchemaAndInterval_CoalesceToEmpty

        [Fact]
        public void CreateHypertable_NullSchemaAndInterval_CoalesceToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.CreateHypertable(tableName: "sensor_data", timeColumnName: "ts", schema: null, chunkTimeInterval: null);

            // Assert
            CreateHypertableOperation op = Assert.IsType<CreateHypertableOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
            Assert.Equal(string.Empty, op.ChunkTimeInterval);
            Assert.False(op.EnableCompression);
            Assert.False(op.MigrateData);
            Assert.Null(op.ChunkSkipColumns);
            Assert.Null(op.AdditionalDimensions);
        }

        #endregion

        #region CreateHypertable_ReturnsOperationBuilderForSameOperation

        [Fact]
        public void CreateHypertable_ReturnsOperationBuilderForSameOperation()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            OperationBuilder<CreateHypertableOperation> result = mb.CreateHypertable(tableName: "t", timeColumnName: "ts");

            // Assert
            Assert.IsType<OperationBuilder<CreateHypertableOperation>>(result);
            Assert.IsType<CreateHypertableOperation>(Assert.Single(mb.Operations));
        }

        #endregion

        #region AlterHypertable_MapsOldArguments

        [Fact]
        public void AlterHypertable_MapsOldArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);
            List<Dimension> oldDims = [Dimension.CreateHash("col", 4)];

            // Act
            mb.AlterHypertable(
                tableName: "sensor_data",
                schema: "public",
                chunkTimeInterval: "2 days",
                enableCompression: true,
                chunkSkipColumns: ["x"],
                compressionSegmentBy: ["dev"],
                compressionOrderBy: ["ts"],
                oldChunkTimeInterval: "1 day",
                oldEnableCompression: true,
                oldChunkSkipColumns: ["y"],
                oldAdditionalDimensions: oldDims,
                oldCompressionSegmentBy: ["olddev"],
                oldCompressionOrderBy: ["oldts"]);

            // Assert
            AlterHypertableOperation op = Assert.IsType<AlterHypertableOperation>(Assert.Single(mb.Operations));
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("2 days", op.ChunkTimeInterval);
            Assert.True(op.EnableCompression);
            Assert.Equal("1 day", op.OldChunkTimeInterval);
            Assert.True(op.OldEnableCompression);
            Assert.Equal(["y"], op.OldChunkSkipColumns);
            Assert.Same(oldDims, op.OldAdditionalDimensions);
            Assert.Equal(["olddev"], op.OldCompressionSegmentBy);
            Assert.Equal(["oldts"], op.OldCompressionOrderBy);
        }

        #endregion

        #region AlterHypertable_MapsTimeColumnArguments

        [Fact]
        public void AlterHypertable_MapsTimeColumnArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterHypertable(
                tableName: "sensor_data",
                schema: "public",
                timeColumnName: "new_ts",
                oldTimeColumnName: "old_ts");

            // Assert
            AlterHypertableOperation op = Assert.IsType<AlterHypertableOperation>(Assert.Single(mb.Operations));
            Assert.Equal("new_ts", op.TimeColumnName);
            Assert.Equal("old_ts", op.OldTimeColumnName);
        }

        #endregion

        #region AlterHypertable_NullTimeColumnNames_CoalesceToEmpty

        [Fact]
        public void AlterHypertable_NullTimeColumnNames_CoalesceToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterHypertable(tableName: "sensor_data", timeColumnName: null, oldTimeColumnName: null);

            // Assert
            AlterHypertableOperation op = Assert.IsType<AlterHypertableOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.TimeColumnName);
            Assert.Equal(string.Empty, op.OldTimeColumnName);
        }

        #endregion

        #region RemoveHypertable_MapsAllArguments

        [Fact]
        public void RemoveHypertable_MapsAllArguments()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            OperationBuilder<RemoveHypertableOperation> result = mb.RemoveHypertable(
                tableName: "sensor_data",
                schema: "public");

            // Assert
            RemoveHypertableOperation op = Assert.IsType<RemoveHypertableOperation>(Assert.Single(mb.Operations));
            Assert.NotNull(result);
            Assert.Equal("sensor_data", op.TableName);
            Assert.Equal("public", op.Schema);
        }

        #endregion

        #region RemoveHypertable_IsNotDestructiveChange

        [Fact]
        public void RemoveHypertable_IsNotDestructiveChange()
        {
            // Arrange & Act
            RemoveHypertableOperation op = new();

            // Assert
            Assert.False(op.IsDestructiveChange);
        }

        #endregion

        #region RemoveHypertable_NullSchema_CoalescesToEmpty

        [Fact]
        public void RemoveHypertable_NullSchema_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.RemoveHypertable(tableName: "sensor_data", schema: null);

            // Assert
            RemoveHypertableOperation op = Assert.IsType<RemoveHypertableOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.Schema);
        }

        #endregion

        #region AlterHypertable_NullOldChunkTimeInterval_CoalescesToEmpty

        [Fact]
        public void AlterHypertable_NullOldChunkTimeInterval_CoalescesToEmpty()
        {
            // Arrange
            MigrationBuilder mb = new(activeProvider: null);

            // Act
            mb.AlterHypertable(tableName: "sensor_data", oldChunkTimeInterval: null);

            // Assert
            AlterHypertableOperation op = Assert.IsType<AlterHypertableOperation>(Assert.Single(mb.Operations));
            Assert.Equal(string.Empty, op.OldChunkTimeInterval);
        }

        #endregion
    }
}

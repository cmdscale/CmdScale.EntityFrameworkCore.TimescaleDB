using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.Hypertable
{
    /// <summary>
    /// Tests the actual C# text emitted by <see cref="HypertableCSharpGenerator"/> using a
    /// real <see cref="ICSharpHelper"/> so literal values are asserted, not just structure.
    /// </summary>
    public class HypertableCSharpGeneratorTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(CreateHypertableOperation operation)
        {
            IndentedStringBuilder builder = new();
            new HypertableCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(AlterHypertableOperation operation)
        {
            IndentedStringBuilder builder = new();
            new HypertableCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region CreateHypertable_EmitsRequiredArgsAndOmitsEmptyAndFalse

        [Fact]
        public void CreateHypertable_EmitsRequiredArgsAndOmitsEmptyAndFalse()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "sensor_data",
                TimeColumnName = "ts",
                Schema = string.Empty,
                ChunkTimeInterval = string.Empty,
                EnableCompression = false,
                MigrateData = false,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("timeColumnName: \"ts\"", result);
            Assert.DoesNotContain("schema:", result);
            Assert.DoesNotContain("chunkTimeInterval:", result);
            Assert.DoesNotContain("enableCompression:", result);
            Assert.DoesNotContain("migrateData:", result);
            Assert.EndsWith(")", result.TrimEnd());
        }

        #endregion

        #region CreateHypertable_OneNamedArgPerLine

        [Fact]
        public void CreateHypertable_OneNamedArgPerLine()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "sensor_data",
                TimeColumnName = "ts",
                Schema = "public",
            };

            // Act
            string result = Generate(op);
            string[] lines = result.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

            // Assert
            Assert.Contains(lines, l => l.StartsWith("tableName: "));
            Assert.Contains(lines, l => l.StartsWith("timeColumnName: "));
            Assert.Contains(lines, l => l.StartsWith("schema: "));
        }

        #endregion

        #region CreateHypertable_StringListArgs_EmitCollectionExpression

        [Fact]
        public void CreateHypertable_StringListArgs_EmitCollectionExpression()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "sensor_data",
                TimeColumnName = "ts",
                ChunkSkipColumns = ["a", "b"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("chunkSkipColumns: [\"a\", \"b\"]", result);
        }

        #endregion

        #region CreateHypertable_RangeAndHashDimensions_FullyQualified

        [Fact]
        public void CreateHypertable_RangeAndHashDimensions_FullyQualified()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "sensor_data",
                TimeColumnName = "ts",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("range_col", "1 day"),
                    Dimension.CreateHash("hash_col", 4),
                ],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.Dimension.CreateRange(\"range_col\", \"1 day\")", result);
            Assert.Contains("CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions.Dimension.CreateHash(\"hash_col\", 4)", result);
            Assert.Contains("CreateRange(\"range_col\", \"1 day\"),", result);
        }

        #endregion

        #region CreateHypertable_DimensionWithNullIntervalAndPartitions_DefaultsEmitted

        [Fact]
        public void CreateHypertable_DimensionWithNullIntervalAndPartitions_DefaultsEmitted()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "sensor_data",
                TimeColumnName = "ts",
                AdditionalDimensions =
                [
                    new Dimension { ColumnName = "range_col", Type = EDimensionType.Range, Interval = null },
                    new Dimension { ColumnName = "hash_col", Type = EDimensionType.Hash, NumberOfPartitions = null },
                ],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("Dimension.CreateRange(\"range_col\", \"\")", result);
            Assert.Contains("Dimension.CreateHash(\"hash_col\", 0)", result);
        }

        #endregion

        #region AlterHypertable_EmitsOldArgsOnlyWhenNonDefault

        [Fact]
        public void AlterHypertable_EmitsOldArgsOnlyWhenNonDefault()
        {
            // Arrange
            AlterHypertableOperation op = new()
            {
                TableName = "sensor_data",
                ChunkTimeInterval = "2 days",
                EnableCompression = true,
                OldChunkTimeInterval = "1 day",
                OldEnableCompression = true,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("chunkTimeInterval: \"2 days\"", result);
            Assert.Contains("enableCompression: true", result);
            Assert.Contains("oldChunkTimeInterval: \"1 day\"", result);
            Assert.Contains("oldEnableCompression: true", result);
        }

        #endregion

        #region CreateHypertable_FullyPopulated_EmitsMigrateDataAndAllCompressionLists

        [Fact]
        public void CreateHypertable_FullyPopulated_EmitsMigrateDataAndAllCompressionLists()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "sensor_data",
                TimeColumnName = "ts",
                Schema = "metrics",
                ChunkTimeInterval = "1 day",
                EnableCompression = true,
                MigrateData = true,
                ChunkSkipColumns = ["a", "b"],
                AdditionalDimensions = [Dimension.CreateHash("device_id", 4)],
                CompressionSegmentBy = ["device_id"],
                CompressionOrderBy = ["ts DESC"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("migrateData: true", result);
            Assert.Contains("enableCompression: true", result);
            Assert.Contains("chunkSkipColumns: [\"a\", \"b\"]", result);
            Assert.Contains("compressionSegmentBy: [\"device_id\"]", result);
            Assert.Contains("compressionOrderBy: [\"ts DESC\"]", result);
            Assert.Contains("additionalDimensions:", result);
        }

        #endregion

        #region AlterHypertable_FullyPopulated_EmitsAllNewAndOldLists

        [Fact]
        public void AlterHypertable_FullyPopulated_EmitsAllNewAndOldLists()
        {
            // Arrange
            AlterHypertableOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "metrics",
                ChunkTimeInterval = "2 days",
                EnableCompression = true,
                ChunkSkipColumns = ["a", "b"],
                AdditionalDimensions = [Dimension.CreateHash("device_id", 4)],
                CompressionSegmentBy = ["device_id"],
                CompressionOrderBy = ["ts DESC"],
                OldChunkTimeInterval = "1 day",
                OldEnableCompression = true,
                OldChunkSkipColumns = ["a"],
                OldAdditionalDimensions = [Dimension.CreateRange("region", "10")],
                OldCompressionSegmentBy = ["region"],
                OldCompressionOrderBy = ["ts"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("schema: \"metrics\"", result);
            Assert.Contains("chunkTimeInterval: \"2 days\"", result);
            Assert.Contains("enableCompression: true", result);
            Assert.Contains("chunkSkipColumns: [\"a\", \"b\"]", result);
            Assert.Contains("compressionSegmentBy: [\"device_id\"]", result);
            Assert.Contains("compressionOrderBy: [\"ts DESC\"]", result);
            Assert.Contains("additionalDimensions:", result);

            Assert.Contains("oldChunkTimeInterval: \"1 day\"", result);
            Assert.Contains("oldEnableCompression: true", result);
            Assert.Contains("oldChunkSkipColumns: [\"a\"]", result);
            Assert.Contains("oldCompressionSegmentBy: [\"region\"]", result);
            Assert.Contains("oldCompressionOrderBy: [\"ts\"]", result);
            Assert.Contains("oldAdditionalDimensions:", result);
        }

        #endregion

        #region AlterHypertable_OmitsOldArgsWhenDefault

        [Fact]
        public void AlterHypertable_OmitsOldArgsWhenDefault()
        {
            // Arrange
            AlterHypertableOperation op = new()
            {
                TableName = "sensor_data",
                OldChunkTimeInterval = string.Empty,
                OldEnableCompression = false,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.DoesNotContain("oldChunkTimeInterval:", result);
            Assert.DoesNotContain("oldEnableCompression:", result);
        }

        #endregion
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    public class HypertableOperationGeneratorTests
    {
        /// <summary>
        /// A helper to run the generator and capture its string output.
        /// </summary>
        private static string GetGeneratedCode(dynamic operation)
        {
            IndentedStringBuilder builder = new();

            HypertableOperationGenerator generator = new(true);
            List<string> statements = generator.Generate(operation);
            SqlBuilderHelper.BuildQueryString(statements, builder);
            return builder.ToString();
        }

        // --- Tests for CreateHypertableOperation ---

        [Fact]
        public void Generate_Create_with_minimal_details_generates_correct_sql()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "MinimalTable",
                Schema = "public",
                TimeColumnName = "Timestamp"
            };

            string expected = @".Sql(@""
                SELECT create_hypertable('public.""""MinimalTable""""', 'Timestamp');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Create_with_all_options_generates_comprehensive_sql()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "FullTable",
                Schema = "custom_schema",
                TimeColumnName = "EventTime",
                ChunkTimeInterval = "1 day",
                EnableCompression = true,
                ChunkSkipColumns = ["DeviceId"],
                AdditionalDimensions =
                [
                    Dimension.CreateHash("LocationId", 4)
                ]
            };

            string expected = @".Sql(@""
                SELECT create_hypertable('custom_schema.""""FullTable""""', 'EventTime');
                SELECT set_chunk_time_interval('custom_schema.""""FullTable""""', INTERVAL '1 day');
                ALTER TABLE """"custom_schema"""".""""FullTable"""" SET (timescaledb.compress = true);
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('custom_schema.""""FullTable""""', 'DeviceId');
                SELECT add_dimension('custom_schema.""""FullTable""""', by_hash('LocationId', 4));
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_WhenAddingChunkSkippingToUncompressedTable_ShouldAlsoEnableCompression()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "custom_schema",
                OldEnableCompression = false,
                OldChunkSkipColumns = [],
                EnableCompression = false,
                ChunkSkipColumns = ["device_id"]
            };

            string expected = @".Sql(@""
                ALTER TABLE """"custom_schema"""".""""Metrics"""" SET (timescaledb.compress = true);
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('custom_schema.""""Metrics""""', 'device_id');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        // --- Tests for AlterHypertableOperation ---

        [Fact]
        public void Generate_Alter_when_changing_compression_generates_correct_sql()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "SensorData",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = false
            };

            string expected = @".Sql(@""
                ALTER TABLE """"public"""".""""SensorData"""" SET (timescaledb.compress = true);
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_when_adding_and_removing_skip_columns_generates_correct_sql()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "metrics_schema",
                ChunkSkipColumns = ["host", "service"],
                OldChunkSkipColumns = ["host", "region"]
            };

            string expected = @".Sql(@""
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('metrics_schema.""""Metrics""""', 'service');
                SELECT disable_chunk_skipping('metrics_schema.""""Metrics""""', 'region');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_when_no_properties_change_generates_no_sql()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "NoChangeTable",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = true,
                ChunkTimeInterval = "7 days",
                OldChunkTimeInterval = "7 days"
            };

            string expected = "";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Generate_Alter_WhenRemovingLastChunkSkipColumn_ShouldDisableCompression_IfNotExplicitlyEnabled()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "Logs",
                Schema = "public",
                OldEnableCompression = false,
                OldChunkSkipColumns = ["trace_id"],
                EnableCompression = false,
                ChunkSkipColumns = []
            };
            string expected = @".Sql(@""
                ALTER TABLE """"public"""".""""Logs"""" SET (timescaledb.compress = false);
                SELECT disable_chunk_skipping('public.""""Logs""""', 'trace_id');
            "")";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }
    }
}

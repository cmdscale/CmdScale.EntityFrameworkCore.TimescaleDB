using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    public class HypertableSqlGeneratorTests
    {
        private static string GetGeneratedCode(dynamic operation)
        {
            List<string> statements = HypertableSqlGenerator.Generate(operation);
            return string.Join("\n", statements);
        }

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

            string expected = @"
                SELECT create_hypertable('public.""MinimalTable""', 'Timestamp');
            ";

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

            string expected = @"
                SELECT create_hypertable('custom_schema.""FullTable""', 'EventTime', chunk_time_interval => INTERVAL '1 day');
                SELECT add_dimension('custom_schema.""FullTable""', by_hash('LocationId', 4));
                ALTER TABLE ""custom_schema"".""FullTable"" SET (timescaledb.enable_columnstore = true);
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('custom_schema.""FullTable""', 'DeviceId');
            ";

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

            string expected = @"
                ALTER TABLE ""custom_schema"".""Metrics"" SET (timescaledb.enable_columnstore = true);
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('custom_schema.""Metrics""', 'device_id');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

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

            string expected = @"
                ALTER TABLE ""public"".""SensorData"" SET (timescaledb.enable_columnstore = true);
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Create_With_Compression_Segment_And_OrderBy_Generates_Correct_Sql()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "CompressedTable",
                Schema = "public",
                TimeColumnName = "Timestamp",
                EnableCompression = true,
                CompressionSegmentBy = ["TenantId", "DeviceId"],
                CompressionOrderBy = ["Timestamp DESC", "Value ASC NULLS LAST"]
            };

            string expected = @"
                SELECT create_hypertable('public.""CompressedTable""', 'Timestamp');
                ALTER TABLE ""public"".""CompressedTable"" SET (timescaledb.enable_columnstore = true, timescaledb.segmentby = '""TenantId"", ""DeviceId""', timescaledb.orderby = '""Timestamp"" DESC, ""Value"" ASC NULLS LAST');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_Adding_Compression_SegmentBy_Generates_Correct_Sql()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "public",
                CompressionSegmentBy = ["DeviceId"],
                OldCompressionSegmentBy = []
            };

            string expected = @"
                ALTER TABLE ""public"".""Metrics"" SET (timescaledb.enable_columnstore = true, timescaledb.segmentby = '""DeviceId""');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_Modifying_Compression_OrderBy_Generates_Correct_Sql()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "public",
                CompressionOrderBy = ["Timestamp DESC"],
                OldCompressionOrderBy = ["Timestamp ASC"]
            };

            string expected = @"
                ALTER TABLE ""public"".""Metrics"" SET (timescaledb.orderby = '""Timestamp"" DESC');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Alter_Removing_Compression_Configuration_Generates_Empty_Strings()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = true,
                CompressionSegmentBy = [],
                OldCompressionSegmentBy = ["DeviceId"],
                CompressionOrderBy = null,
                OldCompressionOrderBy = ["Timestamp DESC"]
            };

            string expected = @"
                ALTER TABLE ""public"".""Metrics"" SET (timescaledb.segmentby = '', timescaledb.orderby = '');
            ";

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

            string expected = @"
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('metrics_schema.""Metrics""', 'service');
                SELECT disable_chunk_skipping('metrics_schema.""Metrics""', 'region');
            ";

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
            string expected = @"
                ALTER TABLE ""public"".""Logs"" SET (timescaledb.enable_columnstore = false);
                SELECT disable_chunk_skipping('public.""Logs""', 'trace_id');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Create_When_MigrateData_Is_False_Does_Not_Include_Migrate_Data_Parameter()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "public",
                TimeColumnName = "Timestamp",
                MigrateData = false
            };

            string expected = @"
                SELECT create_hypertable('public.""Metrics""', 'Timestamp');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Create_When_MigrateData_Is_True_Includes_Migrate_Data_Parameter()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "Metrics",
                Schema = "public",
                TimeColumnName = "Timestamp",
                MigrateData = true
            };

            string expected = @"
                SELECT create_hypertable('public.""Metrics""', 'Timestamp', migrate_data => true);
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Create_When_MigrateData_True_With_All_Options_Generates_Comprehensive_Sql()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "CompleteTable",
                Schema = "custom_schema",
                TimeColumnName = "EventTime",
                MigrateData = true,
                ChunkTimeInterval = "1 day",
                EnableCompression = true,
                ChunkSkipColumns = ["DeviceId"],
                AdditionalDimensions =
                [
                    Dimension.CreateHash("LocationId", 4)
                ]
            };

            string expected = @"
                SELECT create_hypertable('custom_schema.""CompleteTable""', 'EventTime', migrate_data => true, chunk_time_interval => INTERVAL '1 day');
                SELECT add_dimension('custom_schema.""CompleteTable""', by_hash('LocationId', 4));
                ALTER TABLE ""custom_schema"".""CompleteTable"" SET (timescaledb.enable_columnstore = true);
                SET timescaledb.enable_chunk_skipping = 'ON';
                SELECT enable_chunk_skipping('custom_schema.""CompleteTable""', 'DeviceId');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Generate_Create_Default_MigrateData_Does_Not_Include_Parameter()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "DefaultTable",
                Schema = "public",
                TimeColumnName = "Timestamp"
            };

            string expected = @"
                SELECT create_hypertable('public.""DefaultTable""', 'Timestamp');
            ";

            // Act
            string result = GetGeneratedCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
            Assert.DoesNotContain("migrate_data", result);
        }

        [Fact]
        public void Legacy_Generate_Create_WithCompression_Emits_Legacy_Names()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "LegacyTable",
                Schema = "public",
                TimeColumnName = "ts",
                EnableCompression = true,
                CompressionSegmentBy = ["device_id"],
                CompressionOrderBy = ["ts DESC"]
            };

            // Act
            List<string> statements = HypertableSqlGenerator.Generate(operation, useLegacyCompressionNames: true);
            string result = string.Join("\n", statements);

            // Assert
            Assert.Contains("timescaledb.compress = true", result);
            Assert.Contains("timescaledb.compress_segmentby", result);
            Assert.Contains("timescaledb.compress_orderby", result);
            Assert.DoesNotContain("enable_columnstore", result);
        }

        [Fact]
        public void Legacy_Generate_Alter_EnableCompression_Emits_compress_Name()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "LegacyAlter",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = false
            };

            // Act
            List<string> statements = HypertableSqlGenerator.Generate(operation, useLegacyCompressionNames: true);
            string result = string.Join("\n", statements);

            // Assert
            Assert.Contains("timescaledb.compress = true", result);
            Assert.DoesNotContain("enable_columnstore", result);
        }

        [Fact]
        public void Legacy_Generate_Alter_DisableCompression_Emits_compress_False()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "LegacyDisable",
                Schema = "public",
                EnableCompression = false,
                OldEnableCompression = true
            };

            // Act
            List<string> statements = HypertableSqlGenerator.Generate(operation, useLegacyCompressionNames: true);
            string result = string.Join("\n", statements);

            // Assert
            Assert.Contains("timescaledb.compress = false", result);
            Assert.DoesNotContain("enable_columnstore", result);
        }
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    /// <summary>
    /// Comprehensive tests for HypertableOperationGenerator validating design-time and runtime
    /// SQL generation according to TimescaleDB requirements.
    ///
    /// TimescaleDB Requirements (researched from official docs):
    /// - create_hypertable(relation, by_range/by_hash) - modern API (v2.13+)
    /// - set_chunk_time_interval() - accepts INTERVAL or bigint (microseconds)
    /// - Compression requires ALTER TABLE SET (timescaledb.compress = true/false)
    /// - enable_chunk_skipping() requires compression to be enabled first
    /// - add_dimension() uses by_hash(column, partitions) or by_range(column, interval)
    /// - Dimensions can only be added to empty hypertables (in practice, add during creation)
    /// </summary>
    public class HypertableOperationGeneratorComprehensiveTests
    {
        /// <summary>
        /// Helper to run the generator and capture design-time C# code output.
        /// </summary>
        private static string GetDesignTimeCode(dynamic operation)
        {
            IndentedStringBuilder builder = new();
            HypertableOperationGenerator generator = new(isDesignTime: true);
            List<string> statements = generator.Generate(operation);
            SqlBuilderHelper.BuildQueryString(statements, builder);
            return builder.ToString();
        }

        /// <summary>
        /// Helper to run the generator and capture runtime SQL output.
        /// </summary>
        private static string GetRuntimeSql(dynamic operation)
        {
            HypertableOperationGenerator generator = new(isDesignTime: false);
            List<string> statements = generator.Generate(operation);
            return string.Join("\n", statements);
        }

        #region CreateHypertableOperation - Design Time Tests

        [Fact]
        public void DesignTime_Create_WithRangeDimension_GeneratesCorrectCode()
        {
            // Arrange - Test by_range() dimension syntax
            CreateHypertableOperation operation = new()
            {
                TableName = "events",
                Schema = "public",
                TimeColumnName = "event_time",
                ChunkTimeInterval = "1 day",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("received_time", "7 days")
                ]
            };

            string expected = @".Sql(@""
                SELECT create_hypertable('public.""""events""""', 'event_time', chunk_time_interval => INTERVAL '1 day');
                SELECT add_dimension('public.""""events""""', by_range('received_time', INTERVAL '7 days'));
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithMultipleDimensions_GeneratesCorrectOrder()
        {
            // Arrange - Test multiple dimensions (hash + range)
            CreateHypertableOperation operation = new()
            {
                TableName = "distributed_events",
                Schema = "public",
                TimeColumnName = "timestamp",
                AdditionalDimensions =
                [
                    Dimension.CreateHash("device_id", 4),
                    Dimension.CreateRange("processed_time", "1 month")
                ]
            };

            string expected = @".Sql(@""
                SELECT create_hypertable('public.""""distributed_events""""', 'timestamp');
                SELECT add_dimension('public.""""distributed_events""""', by_hash('device_id', 4));
                SELECT add_dimension('public.""""distributed_events""""', by_range('processed_time', INTERVAL '1 month'));
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithChunkTimeIntervalAsMicroseconds_GeneratesCorrectCode()
        {
            // Arrange - Test bigint interval (microseconds)
            CreateHypertableOperation operation = new()
            {
                TableName = "high_freq_data",
                Schema = "public",
                TimeColumnName = "ts",
                ChunkTimeInterval = "86400000000" // 1 day in microseconds
            };

            string expected = @".Sql(@""
                SELECT create_hypertable('public.""""high_freq_data""""', 'ts', chunk_time_interval => 86400000000::bigint);
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_CompressionWithoutChunkSkipping_GeneratesCorrectCode()
        {
            // Arrange - Compression enabled but no chunk skipping
            CreateHypertableOperation operation = new()
            {
                TableName = "compressed_data",
                Schema = "public",
                TimeColumnName = "time",
                EnableCompression = true
            };

            string expected = @".Sql(@""
                SELECT create_hypertable('public.""""compressed_data""""', 'time');
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE """"public"""".""""compressed_data"""" SET (timescaledb.compress = true)';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_ChunkSkippingAutoEnablesCompression_GeneratesCorrectCode()
        {
            // Arrange - Chunk skipping automatically enables compression (TimescaleDB requirement)
            CreateHypertableOperation operation = new()
            {
                TableName = "skippable_chunks",
                Schema = "public",
                TimeColumnName = "timestamp",
                EnableCompression = false, // Explicitly false
                ChunkSkipColumns = ["device_id", "sensor_type"]
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Compression should be auto-enabled
            Assert.Contains("timescaledb.compress = true", result);
        }

        #endregion

        #region CreateHypertableOperation - Runtime Tests

        [Fact]
        public void Runtime_Create_Minimal_GeneratesCorrectSQL()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "simple_table",
                Schema = "public",
                TimeColumnName = "time"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Runtime uses single quotes (not doubled)
            Assert.Contains("SELECT create_hypertable('public.\"simple_table\"', 'time')", result);
            Assert.EndsWith(";", result.Trim());
        }

        [Fact]
        public void Runtime_Create_WithIntervalString_UsesIntervalKeyword()
        {
            // Arrange - String intervals should use INTERVAL keyword
            CreateHypertableOperation operation = new()
            {
                TableName = "timed_data",
                Schema = "public",
                TimeColumnName = "timestamp",
                ChunkTimeInterval = "7 days"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("INTERVAL '7 days'", result);
            Assert.DoesNotContain("::bigint", result);
        }

        [Fact]
        public void Runtime_Create_WithNumericInterval_UsesBigintCast()
        {
            // Arrange - Numeric intervals should use ::bigint cast
            CreateHypertableOperation operation = new()
            {
                TableName = "numeric_interval",
                Schema = "public",
                TimeColumnName = "ts",
                ChunkTimeInterval = "604800000000"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("604800000000::bigint", result);
            Assert.DoesNotContain("INTERVAL", result);
        }

        [Fact]
        public void Runtime_Create_WithHashDimension_GeneratesByHashSyntax()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "partitioned",
                Schema = "public",
                TimeColumnName = "time",
                AdditionalDimensions =
                [
                    Dimension.CreateHash("location_id", 8)
                ]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Must use by_hash() with partition count
            Assert.Contains("add_dimension('public.\"partitioned\"', by_hash('location_id', 8))", result);
        }

        [Fact]
        public void Runtime_Create_WithRangeDimension_GeneratesByRangeSyntax()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "ranged",
                Schema = "public",
                TimeColumnName = "time",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("secondary_time", "30 days")
                ]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Must use by_range() with interval
            Assert.Contains("add_dimension('public.\"ranged\"', by_range('secondary_time', INTERVAL '30 days'))", result);
        }

        #endregion

        #region AlterHypertableOperation - Design Time Tests

        [Fact]
        public void DesignTime_Alter_ChangingChunkInterval_FromStringToString_GeneratesCorrectCode()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                ChunkTimeInterval = "1 day",
                OldChunkTimeInterval = "7 days"
            };

            string expected = @".Sql(@""
                SELECT set_chunk_time_interval('public.""""metrics""""', INTERVAL '1 day');
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_ChangingChunkInterval_FromStringToNumeric_GeneratesCorrectCode()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                ChunkTimeInterval = "86400000000", // Numeric (microseconds)
                OldChunkTimeInterval = "1 day" // String interval
            };

            string expected = @".Sql(@""
                SELECT set_chunk_time_interval('public.""""metrics""""', 86400000000::bigint);
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_AddingDimension_GeneratesCorrectCode()
        {
            // Arrange - Adding a new dimension to existing hypertable
            AlterHypertableOperation operation = new()
            {
                TableName = "expandable",
                Schema = "public",
                AdditionalDimensions =
                [
                    Dimension.CreateHash("user_id", 4)
                ],
                OldAdditionalDimensions = []
            };

            string expected = @".Sql(@""
                SELECT add_dimension('public.""""expandable""""', by_hash('user_id', 4));
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_RemovingDimension_GeneratesWarningComment()
        {
            // Arrange - TimescaleDB does NOT support removing dimensions
            AlterHypertableOperation operation = new()
            {
                TableName = "cannot_remove",
                Schema = "public",
                AdditionalDimensions = [],
                OldAdditionalDimensions =
                [
                    Dimension.CreateHash("old_column", 4)
                ]
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - Should include warning comment
            Assert.Contains("WARNING", result);
            Assert.Contains("does not support removing dimensions", result);
            Assert.Contains("old_column", result);
        }

        [Fact]
        public void DesignTime_Alter_ModifyingDimension_GeneratesAddForNew()
        {
            // Arrange - Changing dimension parameters (adds new, warns about old)
            AlterHypertableOperation operation = new()
            {
                TableName = "modified_dims",
                Schema = "public",
                AdditionalDimensions =
                [
                    Dimension.CreateHash("location", 8) // Changed from 4 to 8 partitions
                ],
                OldAdditionalDimensions =
                [
                    Dimension.CreateHash("location", 4)
                ]
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert - New dimension added (old one cannot be removed)
            Assert.Contains("by_hash('location', 8)", result);
        }

        [Fact]
        public void DesignTime_Alter_DisablingCompression_GeneratesCorrectCode()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "decompress",
                Schema = "public",
                EnableCompression = false,
                OldEnableCompression = true
            };

            string expected = @".Sql(@""
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE """"public"""".""""decompress"""" SET (timescaledb.compress = false)';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_AddingChunkSkipColumn_GeneratesCorrectSequence()
        {
            // Arrange - Adding new chunk skip columns
            AlterHypertableOperation operation = new()
            {
                TableName = "add_skip",
                Schema = "public",
                ChunkSkipColumns = ["col1", "col2", "col3"],
                OldChunkSkipColumns = ["col1"]
            };

            string expected = @".Sql(@""
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'SET timescaledb.enable_chunk_skipping = ''ON''';
                        EXECUTE 'SELECT enable_chunk_skipping(''public.""""add_skip""""'', ''col2'')';
                        EXECUTE 'SELECT enable_chunk_skipping(''public.""""add_skip""""'', ''col3'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_RemovingChunkSkipColumn_GeneratesDisableCommands()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "remove_skip",
                Schema = "public",
                ChunkSkipColumns = ["keep_this"],
                OldChunkSkipColumns = ["keep_this", "remove_this"]
            };

            string expected = @".Sql(@""
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'SELECT disable_chunk_skipping(''public.""""remove_skip""""'', ''remove_this'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            "")";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region AlterHypertableOperation - Runtime Tests

        [Fact]
        public void Runtime_Alter_ChunkInterval_GeneratesCorrectSQL()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "adjust_chunks",
                Schema = "analytics",
                ChunkTimeInterval = "2 weeks",
                OldChunkTimeInterval = "1 week"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("set_chunk_time_interval('analytics.\"adjust_chunks\"', INTERVAL '2 weeks')", result);
        }

        [Fact]
        public void Runtime_Alter_EnableCompression_GeneratesCorrectSQL()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "enable_compress",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = false
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("ALTER TABLE \"public\".\"enable_compress\" SET (timescaledb.compress = true)", result);
        }

        [Fact]
        public void Runtime_Alter_DisableCompression_GeneratesCorrectSQL()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "disable_compress",
                Schema = "public",
                EnableCompression = false,
                OldEnableCompression = true
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("ALTER TABLE \"public\".\"disable_compress\" SET (timescaledb.compress = false)", result);
        }

        [Fact]
        public void Runtime_Alter_ChunkSkipping_RequiresSETCommand()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "skip_test",
                Schema = "public",
                ChunkSkipColumns = ["new_col"],
                OldChunkSkipColumns = []
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Must SET enable_chunk_skipping = 'ON' before enable_chunk_skipping()
            Assert.Contains("SET timescaledb.enable_chunk_skipping = ''ON''", result);
            Assert.Contains("enable_chunk_skipping(''public.\"skip_test\"'', ''new_col'')", result);
        }

        #endregion

        #region TimescaleDB Constraint Validation Tests

        [Fact]
        public void Create_ChunkSkipping_RequiresCompression()
        {
            // Arrange - TimescaleDB requires compression for chunk skipping
            CreateHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                TimeColumnName = "time",
                EnableCompression = false,
                ChunkSkipColumns = ["col1"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Compression must be automatically enabled
            Assert.Contains("timescaledb.compress = true", result);
        }

        [Fact]
        public void Alter_AddingChunkSkipping_AutoEnablesCompression()
        {
            // Arrange - Adding chunk skip columns when compression is disabled
            AlterHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                EnableCompression = false,
                OldEnableCompression = false,
                ChunkSkipColumns = ["device_id"],
                OldChunkSkipColumns = []
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Compression should be auto-enabled
            Assert.Contains("ALTER TABLE \"public\".\"test\" SET (timescaledb.compress = true)", result);
        }

        [Fact]
        public void Alter_RemovingAllChunkSkipColumns_CanDisableCompression()
        {
            // Arrange - Removing all chunk skip columns when compression not explicitly enabled
            AlterHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                EnableCompression = false,
                OldEnableCompression = false,
                ChunkSkipColumns = [],
                OldChunkSkipColumns = ["col1", "col2"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Compression can be disabled when no chunk skipping
            Assert.Contains("timescaledb.compress = false", result);
            Assert.Contains("disable_chunk_skipping", result);
        }

        [Fact]
        public void Alter_KeepingExplicitCompression_WhenRemovingChunkSkipping()
        {
            // Arrange - Compression explicitly enabled, removing chunk skip columns
            AlterHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = true,
                ChunkSkipColumns = [],
                OldChunkSkipColumns = ["col1"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert - Compression should remain enabled
            Assert.DoesNotContain("timescaledb.compress = false", result);
            Assert.Contains("disable_chunk_skipping", result);
        }

        [Fact]
        public void Create_EmptyHypertable_OnlyGeneratesCreateStatement()
        {
            // Arrange - Minimal hypertable
            CreateHypertableOperation operation = new()
            {
                TableName = "minimal",
                Schema = "public",
                TimeColumnName = "ts"
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Single(result.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            Assert.Contains("create_hypertable", result);
        }

        [Fact]
        public void Create_Dimensions_GeneratedAfterHypertableCreation()
        {
            // Arrange - Dimensions must be added after create_hypertable
            CreateHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                TimeColumnName = "time",
                AdditionalDimensions =
                [
                    Dimension.CreateHash("location", 4)
                ]
            };

            // Act
            string result = GetRuntimeSql(operation);
            string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert - create_hypertable must come before add_dimension
            int createIndex = Array.FindIndex(lines, l => l.Contains("create_hypertable"));
            int dimensionIndex = Array.FindIndex(lines, l => l.Contains("add_dimension"));
            Assert.True(createIndex < dimensionIndex, "create_hypertable must execute before add_dimension");
        }

        [Fact]
        public void Create_Compression_GeneratedAfterHypertableCreation()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                TimeColumnName = "time",
                EnableCompression = true
            };

            // Act
            string result = GetRuntimeSql(operation);
            string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert - create_hypertable must come before ALTER TABLE
            int createIndex = Array.FindIndex(lines, l => l.Contains("create_hypertable"));
            int compressIndex = Array.FindIndex(lines, l => l.Contains("ALTER TABLE"));
            Assert.True(createIndex < compressIndex, "create_hypertable must execute before compression settings");
        }

        [Fact]
        public void Create_ChunkSkipping_GeneratedAfterCompression()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "test",
                Schema = "public",
                TimeColumnName = "time",
                ChunkSkipColumns = ["col1"]
            };

            // Act
            string result = GetRuntimeSql(operation);
            string[] lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert - Compression (ALTER TABLE) must come before chunk skipping
            int compressIndex = Array.FindIndex(lines, l => l.Contains("ALTER TABLE") && l.Contains("compress"));
            int skipIndex = Array.FindIndex(lines, l => l.Contains("enable_chunk_skipping"));
            Assert.True(compressIndex < skipIndex, "Compression must be enabled before chunk skipping");
        }

        #endregion
    }
}

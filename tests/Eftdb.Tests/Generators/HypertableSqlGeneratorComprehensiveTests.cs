using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    /// <summary>
    /// Comprehensive tests for HypertableSqlGenerator validating SQL generation
    /// according to TimescaleDB requirements.
    ///
    /// TimescaleDB Requirements (researched from official docs):
    /// - create_hypertable(relation, by_range/by_hash) - modern API (v2.13+)
    /// - set_chunk_time_interval() - accepts INTERVAL or bigint (microseconds)
    /// - Compression requires ALTER TABLE SET (timescaledb.compress = true/false)
    /// - enable_chunk_skipping() requires compression to be enabled first
    /// - add_dimension() uses by_hash(column, partitions) or by_range(column, interval)
    /// - Dimensions can only be added to empty hypertables (in practice, add during creation)
    /// </summary>
    public class HypertableSqlGeneratorComprehensiveTests
    {
        private static string GetDesignTimeCode(dynamic operation) => GetRuntimeSql(operation);

        private static string GetRuntimeSql(dynamic operation)
        {
            List<string> statements = HypertableSqlGenerator.Generate(operation);
            return string.Join("\n", statements);
        }

        #region CreateHypertableOperation - Design Time Tests

        [Fact]
        public void DesignTime_Create_WithRangeDimension_GeneratesCorrectCode()
        {
            // Arrange
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

            string expected = @"
                SELECT create_hypertable('public.""events""', 'event_time', chunk_time_interval => INTERVAL '1 day');
                SELECT add_dimension('public.""events""', by_range('received_time', INTERVAL '7 days'));
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithMultipleDimensions_GeneratesCorrectOrder()
        {
            // Arrange
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

            string expected = @"
                SELECT create_hypertable('public.""distributed_events""', 'timestamp');
                SELECT add_dimension('public.""distributed_events""', by_hash('device_id', 4));
                SELECT add_dimension('public.""distributed_events""', by_range('processed_time', INTERVAL '1 month'));
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithChunkTimeIntervalAsMicroseconds_GeneratesCorrectCode()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "high_freq_data",
                Schema = "public",
                TimeColumnName = "ts",
                ChunkTimeInterval = "86400000000"
            };

            string expected = @"
                SELECT create_hypertable('public.""high_freq_data""', 'ts', chunk_time_interval => 86400000000::bigint);
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_CompressionWithoutChunkSkipping_GeneratesCorrectCode()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "compressed_data",
                Schema = "public",
                TimeColumnName = "time",
                EnableCompression = true
            };

            string expected = @"
                SELECT create_hypertable('public.""compressed_data""', 'time');
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE ""public"".""compressed_data"" SET (timescaledb.enable_columnstore = true)';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_ChunkSkippingAutoEnablesCompression_GeneratesCorrectCode()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "skippable_chunks",
                Schema = "public",
                TimeColumnName = "timestamp",
                EnableCompression = false,
                ChunkSkipColumns = ["device_id", "sensor_type"]
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Contains("timescaledb.enable_columnstore = true", result);
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

            // Assert
            Assert.Contains("SELECT create_hypertable('public.\"simple_table\"', 'time')", result);
            Assert.EndsWith(";", result.Trim());
        }

        [Fact]
        public void Runtime_Create_WithIntervalString_UsesIntervalKeyword()
        {
            // Arrange
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
            // Arrange
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

            // Assert
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

            // Assert
            Assert.Contains("add_dimension('public.\"ranged\"', by_range('secondary_time', INTERVAL '30 days'))", result);
        }

        [Fact]
        public void Runtime_Create_WithRangeDimension_IntegerInterval_GeneratesNumericByRangeSyntax()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "integer_ranged",
                Schema = "public",
                TimeColumnName = "time",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("sensor_id", "10000")
                ]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("add_dimension('public.\"integer_ranged\"', by_range('sensor_id', 10000::bigint))", result);
            Assert.DoesNotContain("INTERVAL", result);
        }

        [Fact]
        public void Runtime_Create_WithRangeDimension_TimeInterval_GeneratesIntervalByRangeSyntax()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "time_ranged",
                Schema = "public",
                TimeColumnName = "event_time",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("processed_time", "1 hour")
                ]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("add_dimension('public.\"time_ranged\"', by_range('processed_time', INTERVAL '1 hour'))", result);
        }

        [Fact]
        public void DesignTime_Create_WithRangeDimension_IntegerInterval_GeneratesCorrectCode()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "integer_partitions",
                Schema = "analytics",
                TimeColumnName = "timestamp",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("partition_key", "5000")
                ]
            };

            string expected = @"
                SELECT create_hypertable('analytics.""integer_partitions""', 'timestamp');
                SELECT add_dimension('analytics.""integer_partitions""', by_range('partition_key', 5000::bigint));
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region CreateHypertableOperation - Compression Settings Tests

        [Fact]
        public void DesignTime_Create_WithCompressionSegmentBy_GeneratesCorrectCode()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "segmented_data",
                Schema = "public",
                TimeColumnName = "time",
                CompressionSegmentBy = ["tenant_id", "device_id"]
            };

            string expected = @"
                SELECT create_hypertable('public.""segmented_data""', 'time');
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE ""public"".""segmented_data"" SET (timescaledb.enable_columnstore = true, timescaledb.segmentby = ''""tenant_id"", ""device_id""'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Create_WithCompressionOrderBy_GeneratesCorrectCode()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "ordered_data",
                Schema = "public",
                TimeColumnName = "time",
                CompressionOrderBy = ["time DESC", "value ASC NULLS LAST"]
            };

            string expected = @"
                SELECT create_hypertable('public.""ordered_data""', 'time');
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE ""public"".""ordered_data"" SET (timescaledb.enable_columnstore = true, timescaledb.orderby = ''""time"" DESC, ""value"" ASC NULLS LAST'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Runtime_Create_WithFullCompressionSettings_GeneratesUnifiedAlter()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "full_compression",
                Schema = "public",
                TimeColumnName = "time",
                EnableCompression = true,
                CompressionSegmentBy = ["tenant_id"],
                CompressionOrderBy = ["time DESC"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("ALTER TABLE \"public\".\"full_compression\" SET", result);
            Assert.Contains("timescaledb.enable_columnstore = true", result);
            Assert.Contains("timescaledb.segmentby = ''\"tenant_id\"''", result);
            Assert.Contains("timescaledb.orderby = ''\"time\" DESC''", result);
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

            string expected = @"
                SELECT set_chunk_time_interval('public.""metrics""', INTERVAL '1 day');
            ";

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
                ChunkTimeInterval = "86400000000",
                OldChunkTimeInterval = "1 day"
            };

            string expected = @"
                SELECT set_chunk_time_interval('public.""metrics""', 86400000000::bigint);
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_AddingDimension_GeneratesCorrectCode()
        {
            // Arrange
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

            string expected = @"
                SELECT add_dimension('public.""expandable""', by_hash('user_id', 4));
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_RemovingDimension_GeneratesWarningComment()
        {
            // Arrange
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

            // Assert
            Assert.Contains("WARNING", result);
            Assert.Contains("does not support removing dimensions", result);
            Assert.Contains("old_column", result);
        }

        [Fact]
        public void DesignTime_Alter_RemovingDimension_EmitsExactWarningCommentLine()
        {
            // Arrange
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

            // Assert
            Assert.Contains(
                "-- WARNING: TimescaleDB does not support removing dimensions. The following dimensions cannot be removed: 'old_column'",
                result);
        }

        [Fact]
        public void DesignTime_Alter_ModifyingDimension_GeneratesAddForNew()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "modified_dims",
                Schema = "public",
                AdditionalDimensions =
                [
                    Dimension.CreateHash("location", 8)
                ],
                OldAdditionalDimensions =
                [
                    Dimension.CreateHash("location", 4)
                ]
            };

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
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

            string expected = @"
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE ""public"".""decompress"" SET (timescaledb.enable_columnstore = false)';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void DesignTime_Alter_AddingChunkSkipColumn_GeneratesCorrectSequence()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "add_skip",
                Schema = "public",
                ChunkSkipColumns = ["col1", "col2", "col3"],
                OldChunkSkipColumns = ["col1"]
            };

            string expected = @"
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'SET timescaledb.enable_chunk_skipping = ''ON''';
                        EXECUTE 'SELECT enable_chunk_skipping(''public.""add_skip""'', ''col2'')';
                        EXECUTE 'SELECT enable_chunk_skipping(''public.""add_skip""'', ''col3'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

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

            string expected = @"
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'SELECT disable_chunk_skipping(''public.""remove_skip""'', ''remove_this'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

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
            Assert.Contains("ALTER TABLE \"public\".\"enable_compress\" SET (timescaledb.enable_columnstore = true)", result);
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
            Assert.Contains("ALTER TABLE \"public\".\"disable_compress\" SET (timescaledb.enable_columnstore = false)", result);
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

            // Assert
            Assert.Contains("SET timescaledb.enable_chunk_skipping = ''ON''", result);
            Assert.Contains("enable_chunk_skipping(''public.\"skip_test\"'', ''new_col'')", result);
        }

        [Fact]
        public void Runtime_Alter_AddingRangeDimension_WithIntegerInterval_GeneratesCorrectSQL()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "events",
                Schema = "public",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("event_id", "1000")
                ],
                OldAdditionalDimensions = []
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("add_dimension('public.\"events\"', by_range('event_id', 1000::bigint))", result);
            Assert.DoesNotContain("INTERVAL", result);
        }

        [Fact]
        public void Runtime_Alter_AddingRangeDimension_WithTimeInterval_GeneratesCorrectSQL()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "logs",
                Schema = "public",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("ingestion_time", "2 hours")
                ],
                OldAdditionalDimensions = []
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("add_dimension('public.\"logs\"', by_range('ingestion_time', INTERVAL '2 hours'))", result);
        }

        [Fact]
        public void DesignTime_Alter_AddingRangeDimension_WithIntegerInterval_GeneratesCorrectCode()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "analytics",
                AdditionalDimensions =
                [
                    Dimension.CreateRange("metric_id", "50000")
                ],
                OldAdditionalDimensions = []
            };

            string expected = @"
                SELECT add_dimension('analytics.""metrics""', by_range('metric_id', 50000::bigint));
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        #endregion

        #region AlterHypertableOperation - Compression Settings Tests

        [Fact]
        public void DesignTime_Alter_AddingCompressionSegmentBy_GeneratesCorrectCode()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                CompressionSegmentBy = ["device_id"],
                OldCompressionSegmentBy = []
            };

            string expected = @"
                DO $$
                DECLARE
                    license TEXT;
                BEGIN
                    license := current_setting('timescaledb.license', true);

                    IF license IS NULL OR license != 'apache' THEN
                        EXECUTE 'ALTER TABLE ""public"".""metrics"" SET (timescaledb.enable_columnstore = true, timescaledb.segmentby = ''""device_id""'')';
                    ELSE
                        RAISE WARNING 'Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition';
                    END IF;
                END $$;
            ";

            // Act
            string result = GetDesignTimeCode(operation);

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void Runtime_Alter_ChangingCompressionOrderBy_GeneratesCorrectSQL()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                CompressionOrderBy = ["time DESC"],
                OldCompressionOrderBy = ["time ASC"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("timescaledb.orderby = ''\"time\" DESC''", result);
        }

        [Fact]
        public void Runtime_Alter_RemovingCompressionSegmentBy_GeneratesEmptyStringSetting()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                CompressionSegmentBy = [],
                OldCompressionSegmentBy = ["device_id"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("timescaledb.segmentby = ''", result);
        }

        [Fact]
        public void Runtime_Alter_RemovingCompressionOrderBy_GeneratesEmptyStringSetting()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                CompressionOrderBy = null,
                OldCompressionOrderBy = ["time DESC"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("timescaledb.orderby = ''", result);
        }

        [Fact]
        public void Runtime_Alter_ComplexCompressionUpdate_GeneratesUnifiedAlter()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                EnableCompression = true,
                OldEnableCompression = false,
                CompressionSegmentBy = ["new_col"],
                OldCompressionSegmentBy = ["old_col"],
                CompressionOrderBy = [],
                OldCompressionOrderBy = ["time DESC"]
            };

            // Act
            string result = GetRuntimeSql(operation);

            // Assert
            Assert.Contains("ALTER TABLE \"public\".\"metrics\" SET", result);
            Assert.Contains("timescaledb.segmentby = ''\"new_col\"''", result);
            Assert.Contains("timescaledb.orderby = ''''", result);
        }

        #endregion

        #region TimescaleDB Constraint Validation Tests

        [Fact]
        public void Create_ChunkSkipping_RequiresCompression()
        {
            // Arrange
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

            // Assert
            Assert.Contains("timescaledb.enable_columnstore = true", result);
        }

        [Fact]
        public void Alter_AddingChunkSkipping_AutoEnablesCompression()
        {
            // Arrange
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

            // Assert
            Assert.Contains("ALTER TABLE \"public\".\"test\" SET (timescaledb.enable_columnstore = true)", result);
        }

        [Fact]
        public void Alter_RemovingAllChunkSkipColumns_CanDisableCompression()
        {
            // Arrange
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

            // Assert
            Assert.Contains("timescaledb.enable_columnstore = false", result);
            Assert.Contains("disable_chunk_skipping", result);
        }

        [Fact]
        public void Alter_KeepingExplicitCompression_WhenRemovingChunkSkipping()
        {
            // Arrange
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

            // Assert
            Assert.DoesNotContain("timescaledb.compress = false", result);
            Assert.Contains("disable_chunk_skipping", result);
        }

        [Fact]
        public void Create_EmptyHypertable_OnlyGeneratesCreateStatement()
        {
            // Arrange
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
            // Arrange
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

            // Assert
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

            // Assert
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

            // Assert
            int compressIndex = Array.FindIndex(lines, l => l.Contains("ALTER TABLE") && l.Contains("compress"));
            int skipIndex = Array.FindIndex(lines, l => l.Contains("enable_chunk_skipping"));
            Assert.True(compressIndex < skipIndex, "Compression must be enabled before chunk skipping");
        }

        #endregion

        #region Legacy mode

        [Fact]
        public void Legacy_Create_WithCompression_EmitsLegacyOptionNames()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "legacy_create",
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
            Assert.DoesNotContain("timescaledb.segmentby =", result);
            Assert.DoesNotContain("timescaledb.orderby =", result);
        }

        [Fact]
        public void Legacy_Alter_EnableCompression_EmitsLegacyCompressName()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "legacy_alter_enable",
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
        public void Legacy_Alter_DisableCompression_EmitsLegacyCompressFalse()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "legacy_alter_disable",
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

        #endregion
    }
}

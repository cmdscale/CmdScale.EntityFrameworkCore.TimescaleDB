using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
    /// <summary>
    /// Validates that every generator, when targeting the Apache edition, replaces Community-only
    /// statements with a single SQL skip comment while keeping the Apache-compatible statements intact.
    /// </summary>
    public class ApacheEditionSqlGenerationTests
    {
        #region Should_Skip_Community_SubStatements_On_Hypertable_Create

        [Fact]
        public void Should_Skip_Community_SubStatements_On_Hypertable_Create()
        {
            // Arrange
            CreateHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                TimeColumnName = "time",
                EnableCompression = true,
                ChunkSkipColumns = ["device_id"]
            };

            // Act
            List<string> statements = HypertableSqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            Assert.Contains(statements, s => s.Contains("create_hypertable"));
            Assert.Single(statements, s => s == "-- Skipping Community Edition features (compression, chunk skipping) - not available in Apache Edition");
            Assert.DoesNotContain(statements, s => s.Contains("enable_columnstore"));
            Assert.DoesNotContain(statements, s => s.Contains("enable_chunk_skipping"));
        }

        #endregion

        #region Should_Not_Emit_Comment_On_Hypertable_Alter_ChunkInterval_Only

        [Fact]
        public void Should_Not_Emit_Comment_On_Hypertable_Alter_ChunkInterval_Only()
        {
            // Arrange
            AlterHypertableOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                ChunkTimeInterval = "1 day",
                OldChunkTimeInterval = "7 days"
            };

            // Act
            List<string> statements = HypertableSqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            Assert.Single(statements);
            Assert.Contains("set_chunk_time_interval", statements[0]);
            Assert.DoesNotContain(statements, s => s.StartsWith("--"));
        }

        #endregion

        #region Should_Skip_RetentionPolicy_Add

        [Fact]
        public void Should_Skip_RetentionPolicy_Add()
        {
            // Arrange
            AddRetentionPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                DropAfter = "30 days"
            };

            // Act
            List<string> statements = RetentionPolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (retention policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_RetentionPolicy_Drop

        [Fact]
        public void Should_Skip_RetentionPolicy_Drop()
        {
            // Arrange
            DropRetentionPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public"
            };

            // Act
            List<string> statements = RetentionPolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (retention policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_ReorderPolicy_Add

        [Fact]
        public void Should_Skip_ReorderPolicy_Add()
        {
            // Arrange
            AddReorderPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                IndexName = "ix_metrics_time"
            };

            // Act
            List<string> statements = ReorderPolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (reorder policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_ReorderPolicy_Drop

        [Fact]
        public void Should_Skip_ReorderPolicy_Drop()
        {
            // Arrange
            DropReorderPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public"
            };

            // Act
            List<string> statements = ReorderPolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (reorder policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_CompressionPolicy_Add

        [Fact]
        public void Should_Skip_CompressionPolicy_Add()
        {
            // Arrange
            AddCompressionPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public",
                After = "7 days"
            };

            // Act
            List<string> statements = CompressionPolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (compression policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_CompressionPolicy_Drop

        [Fact]
        public void Should_Skip_CompressionPolicy_Drop()
        {
            // Arrange
            DropCompressionPolicyOperation operation = new()
            {
                TableName = "metrics",
                Schema = "public"
            };

            // Act
            List<string> statements = CompressionPolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (compression policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_ContinuousAggregatePolicy_Add

        [Fact]
        public void Should_Skip_ContinuousAggregatePolicy_Add()
        {
            // Arrange
            AddContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "hourly_cagg",
                Schema = "public",
                StartOffset = "1 day",
                EndOffset = "1 hour"
            };

            // Act
            List<string> statements = ContinuousAggregatePolicySqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (continuous aggregate policy) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Skip_ContinuousAggregate_Create_With_Data

        [Fact]
        public void Should_Skip_ContinuousAggregate_Create_With_Data()
        {
            // Arrange
            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_cagg",
                Schema = "public",
                ParentName = "metrics",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "time",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["avg_val:Avg:value"],
                WithNoData = false
            };

            // Act
            List<string> statements = ContinuousAggregateSqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (continuous aggregate) - not available in Apache Edition", comment);
            Assert.DoesNotContain("CREATE MATERIALIZED VIEW", comment);
        }

        #endregion

        #region Should_Skip_ContinuousAggregate_Alter

        [Fact]
        public void Should_Skip_ContinuousAggregate_Alter()
        {
            // Arrange
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_cagg",
                Schema = "public",
                MaterializedOnly = true,
                OldMaterializedOnly = false
            };

            // Act
            List<string> statements = ContinuousAggregateSqlGenerator.Generate(operation, isApacheEdition: true);

            // Assert
            string comment = Assert.Single(statements);
            Assert.Equal("-- Skipping Community Edition feature (continuous aggregate) - not available in Apache Edition", comment);
        }

        #endregion

        #region Should_Not_Skip_ContinuousAggregate_Drop

        [Fact]
        public void Should_Not_Skip_ContinuousAggregate_Drop()
        {
            // Arrange
            DropContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_cagg",
                Schema = "public"
            };

            // Act
            List<string> statements = ContinuousAggregateSqlGenerator.Generate(operation);

            // Assert
            string statement = Assert.Single(statements);
            Assert.Equal("DROP MATERIALIZED VIEW IF EXISTS \"public\".\"hourly_cagg\";", statement);
            Assert.DoesNotContain("--", statement);
        }

        #endregion
    }
}

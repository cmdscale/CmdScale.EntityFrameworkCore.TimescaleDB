using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
#pragma warning disable EF1001 // Internal EF Core API usage.

    /// <summary>
    /// Tests for TimescaleCSharpMigrationOperationGenerator to ensure proper C# code generation.
    /// </summary>
    public class TimescaleCSharpMigrationOperationGeneratorTests
    {
        #region Empty Statements Guard Tests

        [Fact]
        public void Generate_CreateHypertable_WithValidOperation_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            CreateHypertableOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                TimeColumnName = "timestamp",
                ChunkTimeInterval = "7 days"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".CreateHypertable(", result);
            Assert.Contains("tableName:", result);
            Assert.Contains("timeColumnName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_CreateHypertable_WithMigrateData_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            CreateHypertableOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                TimeColumnName = "timestamp",
                ChunkTimeInterval = "7 days",
                MigrateData = true
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains(".CreateHypertable(", result);
            Assert.Contains("migrateData:", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterHypertable_WithNoChanges_GeneratesValidCSharpOrNoOp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterHypertableOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();

            if (!string.IsNullOrWhiteSpace(result))
            {
                Assert.DoesNotContain("migrationBuilder;", result.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
                if (result.Contains("migrationBuilder"))
                {
                    Assert.Contains(".AlterHypertable(", result);
                    Assert.DoesNotContain(".Sql(", result);
                }
            }
        }

        [Fact]
        public void Generate_AddReorderPolicy_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddReorderPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                IndexName = "sensor_data_time_idx"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddReorderPolicy(", result);
            Assert.Contains("tableName:", result);
            Assert.Contains("indexName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_DropReorderPolicy_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            DropReorderPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".DropReorderPolicy(", result);
            Assert.Contains("tableName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_CreateContinuousAggregate_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            CreateContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                ParentName = "sensor_data",
                TimeBucketWidth = "1 hour",
                TimeBucketSourceColumn = "timestamp",
                TimeBucketGroupBy = true,
                AggregateFunctions = ["total_count:Count:id"]
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".CreateContinuousAggregate(", result);
            Assert.Contains("materializedViewName:", result);
            Assert.Contains("parentName:", result);
            Assert.Contains("timeBucketWidth:", result);
            Assert.Contains("aggregateFunctions:", result);
            // Aggregate functions emit as typed entries showing the enum, not magic strings.
            Assert.Contains("ContinuousAggregateFunction(", result);
            Assert.Contains("EAggregateFunction.Count", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_DropContinuousAggregate_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            DropContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".DropContinuousAggregate(", result);
            Assert.Contains("materializedViewName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterReorderPolicy_WithIndexChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterReorderPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                IndexName = "new_index",
                OldIndexName = "old_index",
                InitialStart = DateTime.UtcNow,
                OldInitialStart = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterReorderPolicy(", result);
            Assert.Contains("indexName:", result);
            Assert.Contains("oldIndexName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterReorderPolicy_WithScheduleIntervalChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterReorderPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                IndexName = "sensor_data_idx",
                OldIndexName = "sensor_data_idx", // Same index name
                InitialStart = null,
                OldInitialStart = null, // Same initial start
                ScheduleInterval = "1 day",
                OldScheduleInterval = "4 days" // Different schedule interval
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterReorderPolicy(", result);
            Assert.Contains("scheduleInterval:", result);
            Assert.Contains("oldScheduleInterval:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterReorderPolicy_WithNoChanges_GeneratesValidCSharpOrNoOp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            // An alter operation with no actual changes
            AlterReorderPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                IndexName = "sensor_data_idx",
                OldIndexName = "sensor_data_idx",
                InitialStart = null,
                OldInitialStart = null,
                ScheduleInterval = null,
                OldScheduleInterval = null,
                MaxRuntime = null,
                OldMaxRuntime = null,
                MaxRetries = null,
                OldMaxRetries = null,
                RetryPeriod = null,
                OldRetryPeriod = null
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterReorderPolicy(", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
        }

        [Fact]
        public void Generate_AlterContinuousAggregate_WithChunkIntervalChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                ChunkInterval = "7 days",
                OldChunkInterval = "1 day",
                CreateGroupIndexes = true,
                OldCreateGroupIndexes = true,
                MaterializedOnly = false,
                OldMaterializedOnly = false
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterContinuousAggregate(", result);
            Assert.Contains("chunkInterval:", result);
            Assert.Contains("oldChunkInterval:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterContinuousAggregate_WithMaterializedOnlyChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                ChunkInterval = null,
                OldChunkInterval = null,
                CreateGroupIndexes = true,
                OldCreateGroupIndexes = true,
                MaterializedOnly = true,
                OldMaterializedOnly = false // Changed from false to true
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterContinuousAggregate(", result);
            Assert.Contains("materializedOnly:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterContinuousAggregate_WithCreateGroupIndexesChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                ChunkInterval = null,
                OldChunkInterval = null,
                CreateGroupIndexes = false,
                OldCreateGroupIndexes = true, // Changed from true to false
                MaterializedOnly = false,
                OldMaterializedOnly = false
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterContinuousAggregate(", result);
            Assert.Contains("oldCreateGroupIndexes:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterContinuousAggregate_WithNoChanges_GeneratesValidCSharpOrNoOp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            // An alter operation with no actual changes
            AlterContinuousAggregateOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                ChunkInterval = null,
                OldChunkInterval = null,
                CreateGroupIndexes = true,
                OldCreateGroupIndexes = true,
                MaterializedOnly = false,
                OldMaterializedOnly = false
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterContinuousAggregate(", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
        }

        #endregion

        #region ContinuousAggregatePolicyOperation Tests

        [Fact]
        public void Generate_AddContinuousAggregatePolicy_WithAllParameters_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                StartOffset = "1 month",
                EndOffset = "1 hour",
                ScheduleInterval = "1 hour",
                InitialStart = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                IfNotExists = true,
                IncludeTieredData = true,
                BucketsPerBatch = 5,
                MaxBatchesPerExecution = 10,
                RefreshNewestFirst = false
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddContinuousAggregatePolicy(", result);
            Assert.Contains("materializedViewName:", result);
            Assert.Contains("startOffset:", result);
            Assert.Contains("bucketsPerBatch:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AddContinuousAggregatePolicy_WithMinimalParameters_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                StartOffset = "1 month",
                EndOffset = "1 hour"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddContinuousAggregatePolicy(", result);
            Assert.Contains("materializedViewName:", result);
            Assert.Contains("startOffset:", result);
            Assert.Contains("endOffset:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AddContinuousAggregatePolicy_WithNullOffsets_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                StartOffset = null,
                EndOffset = null
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddContinuousAggregatePolicy(", result);
            Assert.Contains("materializedViewName:", result);
            Assert.DoesNotContain("startOffset:", result);
            Assert.DoesNotContain("endOffset:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AddContinuousAggregatePolicy_WithIntegerOffsets_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "sensor_data_cagg",
                Schema = "public",
                StartOffset = "1000",
                EndOffset = "100"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddContinuousAggregatePolicy(", result);
            Assert.Contains("startOffset:", result);
            Assert.Contains("endOffset:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_RemoveContinuousAggregatePolicy_BasicRemoval_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            RemoveContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".RemoveContinuousAggregatePolicy(", result);
            Assert.Contains("materializedViewName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_RemoveContinuousAggregatePolicy_WithIfExists_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            RemoveContinuousAggregatePolicyOperation operation = new()
            {
                MaterializedViewName = "hourly_stats",
                Schema = "public",
                IfExists = true
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".RemoveContinuousAggregatePolicy(", result);
            Assert.Contains("ifExists:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        #endregion

        #region RetentionPolicyOperation Tests

        [Fact]
        public void Generate_AddRetentionPolicy_WithDropAfter_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddRetentionPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                DropAfter = "30 days"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddRetentionPolicy(", result);
            Assert.Contains("tableName:", result);
            Assert.Contains("dropAfter:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AddRetentionPolicy_WithDropCreatedBefore_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AddRetentionPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                DropCreatedBefore = "60 days"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AddRetentionPolicy(", result);
            Assert.Contains("dropCreatedBefore:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterRetentionPolicy_WithDropAfterChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterRetentionPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                DropAfter = "60 days",
                OldDropAfter = "30 days" // <-- Changed from 30 days
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterRetentionPolicy(", result);
            Assert.Contains("dropAfter:", result);
            Assert.Contains("oldDropAfter:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterRetentionPolicy_WithScheduleIntervalChange_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            AlterRetentionPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public",
                DropAfter = "30 days",
                OldDropAfter = "30 days", // Same drop_after
                ScheduleInterval = "1 day",
                OldScheduleInterval = "4 days" // <-- Changed from 4 days
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".AlterRetentionPolicy(", result);
            Assert.Contains("scheduleInterval:", result);
            Assert.Contains("oldScheduleInterval:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_DropRetentionPolicy_GeneratesValidCSharp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            DropRetentionPolicyOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".DropRetentionPolicy(", result);
            Assert.Contains("tableName:", result);
            Assert.DoesNotContain(".Sql(", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        #endregion

        #region Generate_StandardCreateTableOperation_Falls_Through_To_Base

        [Fact]
        public void Generate_StandardCreateTableOperation_Falls_Through_To_Base()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            CreateTableOperation operation = new()
            {
                Name = "standard_table",
                Schema = "public",
                Columns = { new AddColumnOperation { Name = "id", Schema = "public", Table = "standard_table", ClrType = typeof(int) } }
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".CreateTable(", result);
        }

        #endregion

        #region Helper Methods

        private static CSharpMigrationOperationGeneratorDependencies CreateDependencies()
        {
            Mock<ICSharpHelper> mockCSharpHelper = new();
            return new CSharpMigrationOperationGeneratorDependencies(mockCSharpHelper.Object);
        }

        #endregion
    }

#pragma warning restore EF1001 // Internal EF Core API usage.
}

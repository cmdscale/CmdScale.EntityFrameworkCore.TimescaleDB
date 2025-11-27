using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
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
            // Note: We call the protected method via the public interface indirectly
            // by using reflection or by testing via the public Generate method
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("create_hypertable", result);
            Assert.Contains(";", result);
            Assert.DoesNotContain("migrationBuilder;", result); // This would be invalid C#
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
            Assert.Contains("migrate_data => true", result);
            Assert.DoesNotContain("migrationBuilder;", result);
        }

        [Fact]
        public void Generate_AlterHypertable_WithNoChanges_GeneratesValidCSharpOrNoOp()
        {
            // Arrange
            CSharpMigrationOperationGeneratorDependencies dependencies = CreateDependencies();
            TimescaleCSharpMigrationOperationGenerator generator = new(dependencies);
            IndentedStringBuilder builder = new();

            // An alter operation with no actual changes should still generate valid C#
            AlterHypertableOperation operation = new()
            {
                TableName = "sensor_data",
                Schema = "public"
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();

            // The result should either be empty (no operation generated) or contain valid C#
            // It should NEVER contain just "migrationBuilder;" without a method call
            if (!string.IsNullOrWhiteSpace(result))
            {
                Assert.DoesNotContain("migrationBuilder;", result.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
                // If there's content, it should have a proper method call
                if (result.Contains("migrationBuilder"))
                {
                    Assert.Contains(".Sql(@\"", result);
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
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("add_reorder_policy", result);
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
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("remove_reorder_policy", result);
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
                AggregateFunctions = ["COUNT(*)"]
            };

            // Act
            generator.Generate("migrationBuilder", [operation], builder);

            // Assert
            string result = builder.ToString();
            Assert.Contains("migrationBuilder", result);
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("CREATE MATERIALIZED VIEW", result);
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
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("DROP MATERIALIZED VIEW", result);
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
            Assert.Contains(".Sql(@\"", result);
            // When index changes, policy is dropped and recreated
            Assert.Contains("remove_reorder_policy", result);
            Assert.Contains("add_reorder_policy", result);
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
            Assert.Contains(".Sql(@\"", result);
            // When only schedule changes, uses alter_job
            Assert.Contains("alter_job", result);
            Assert.Contains("schedule_interval", result);
            // Should not drop and recreate
            Assert.DoesNotContain("remove_reorder_policy", result);
            Assert.DoesNotContain("add_reorder_policy", result);
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

            // The result should either be empty (no operation generated) or contain valid C#
            // It should NEVER contain just "migrationBuilder;" without a method call
            if (!string.IsNullOrWhiteSpace(result))
            {
                Assert.DoesNotContain("migrationBuilder;", result.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
                // If there's content, it should have a proper method call
                if (result.Contains("migrationBuilder"))
                {
                    Assert.Contains(".Sql(@\"", result);
                }
            }
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
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("ALTER MATERIALIZED VIEW", result);
            Assert.Contains("SET", result);
            Assert.Contains("timescaledb.chunk_interval", result);
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
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("ALTER MATERIALIZED VIEW", result);
            Assert.Contains("SET", result);
            Assert.Contains("timescaledb.materialized_only", result);
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
            Assert.Contains(".Sql(@\"", result);
            Assert.Contains("ALTER MATERIALIZED VIEW", result);
            Assert.Contains("SET", result);
            Assert.Contains("timescaledb.create_group_indexes", result);
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

            // The result should either be empty (no operation generated) or contain valid C#
            // It should NEVER contain just "migrationBuilder;" without a method call
            if (!string.IsNullOrWhiteSpace(result))
            {
                Assert.DoesNotContain("migrationBuilder;", result.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
                // If there's content, it should have a proper method call
                if (result.Contains("migrationBuilder"))
                {
                    Assert.Contains(".Sql(@\"", result);
                }
            }
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

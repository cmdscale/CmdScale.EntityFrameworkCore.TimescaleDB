using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators
{
    /// <summary>
    /// Tests the actual C# text emitted by <see cref="RetentionPolicyCSharpGenerator"/> using a
    /// real <see cref="ICSharpHelper"/>.
    /// </summary>
    public class RetentionPolicyCSharpGeneratorTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(AddRetentionPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new RetentionPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(AlterRetentionPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new RetentionPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region AddRetentionPolicy_EmitsDropAfter_OmitsNullDropCreatedBefore

        [Fact]
        public void AddRetentionPolicy_EmitsDropAfter_OmitsNullDropCreatedBefore()
        {
            // Arrange
            AddRetentionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                DropAfter = "30 days",
                DropCreatedBefore = null,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("dropAfter: \"30 days\"", result);
            Assert.DoesNotContain("dropCreatedBefore:", result);
        }

        #endregion

        #region AddRetentionPolicy_EmitsDropCreatedBefore

        [Fact]
        public void AddRetentionPolicy_EmitsDropCreatedBefore()
        {
            // Arrange
            AddRetentionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                DropCreatedBefore = "60 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("dropCreatedBefore: \"60 days\"", result);
            Assert.DoesNotContain("dropAfter:", result);
        }

        #endregion

        #region AlterRetentionPolicy_EmitsOldArgsOnlyWhenNonDefault

        [Fact]
        public void AlterRetentionPolicy_EmitsOldArgsOnlyWhenNonDefault()
        {
            // Arrange
            AlterRetentionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                DropAfter = "60 days",
                OldDropAfter = "30 days",
                OldScheduleInterval = "4 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("dropAfter: \"60 days\"", result);
            Assert.Contains("oldDropAfter: \"30 days\"", result);
            Assert.Contains("oldScheduleInterval: \"4 days\"", result);
            Assert.DoesNotContain("oldMaxRuntime:", result);
        }

        #endregion

        #region AddRetentionPolicy_FullyPopulated_EmitsAllOptionalArgs

        [Fact]
        public void AddRetentionPolicy_FullyPopulated_EmitsAllOptionalArgs()
        {
            // Arrange
            AddRetentionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "metrics",
                DropAfter = "30 days",
                DropCreatedBefore = "60 days",
                InitialStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ScheduleInterval = "1 day",
                MaxRuntime = "5 minutes",
                MaxRetries = 3,
                RetryPeriod = "10 minutes",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("schema: \"metrics\"", result);
            Assert.Contains("dropAfter: \"30 days\"", result);
            Assert.Contains("dropCreatedBefore: \"60 days\"", result);
            Assert.Contains("initialStart:", result);
            Assert.Contains("scheduleInterval: \"1 day\"", result);
            Assert.Contains("maxRuntime: \"5 minutes\"", result);
            Assert.Contains("maxRetries: 3", result);
            Assert.Contains("retryPeriod: \"10 minutes\"", result);
        }

        #endregion

        #region AlterRetentionPolicy_FullyPopulated_EmitsAllNewAndOldArgs

        [Fact]
        public void AlterRetentionPolicy_FullyPopulated_EmitsAllNewAndOldArgs()
        {
            // Arrange
            AlterRetentionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "metrics",
                DropAfter = "60 days",
                DropCreatedBefore = "90 days",
                InitialStart = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                ScheduleInterval = "2 days",
                MaxRuntime = "10 minutes",
                MaxRetries = 5,
                RetryPeriod = "15 minutes",
                OldDropAfter = "30 days",
                OldDropCreatedBefore = "45 days",
                OldInitialStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                OldScheduleInterval = "1 day",
                OldMaxRuntime = "5 minutes",
                OldMaxRetries = 3,
                OldRetryPeriod = "10 minutes",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("schema: \"metrics\"", result);
            Assert.Contains("dropAfter: \"60 days\"", result);
            Assert.Contains("dropCreatedBefore: \"90 days\"", result);
            Assert.Contains("initialStart:", result);
            Assert.Contains("scheduleInterval: \"2 days\"", result);
            Assert.Contains("maxRuntime: \"10 minutes\"", result);
            Assert.Contains("maxRetries: 5", result);
            Assert.Contains("retryPeriod: \"15 minutes\"", result);

            // Assert
            Assert.Contains("oldDropAfter: \"30 days\"", result);
            Assert.Contains("oldDropCreatedBefore: \"45 days\"", result);
            Assert.Contains("oldInitialStart:", result);
            Assert.Contains("oldScheduleInterval: \"1 day\"", result);
            Assert.Contains("oldMaxRuntime: \"5 minutes\"", result);
            Assert.Contains("oldMaxRetries: 3", result);
            Assert.Contains("oldRetryPeriod: \"10 minutes\"", result);
        }

        #endregion
    }
}

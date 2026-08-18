using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Features.CompressionPolicy
{
    /// <summary>
    /// Tests the actual C# text emitted by <see cref="CompressionPolicyCSharpGenerator"/> using a
    /// real <see cref="ICSharpHelper"/>.
    /// </summary>
    public class CompressionPolicyCSharpGeneratorTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(AddCompressionPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new CompressionPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(AlterCompressionPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new CompressionPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(DropCompressionPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new CompressionPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        // ── AddCompressionPolicy ───────────────────────────────────────────────

        #region AddCompressionPolicy_MinimalArgs_EmitsTableNameOnly

        [Fact]
        public void AddCompressionPolicy_MinimalArgs_EmitsTableNameOnly()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("AddCompressionPolicy", result);
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.DoesNotContain("schema:", result);
            Assert.DoesNotContain("after:", result);
            Assert.DoesNotContain("createdBefore:", result);
            Assert.DoesNotContain("scheduleInterval:", result);
            Assert.DoesNotContain("initialStart:", result);
            Assert.DoesNotContain("timezone:", result);
            Assert.DoesNotContain("ifNotExists:", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsAfter_WhenSet

        [Fact]
        public void AddCompressionPolicy_EmitsAfter_WhenSet()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "7 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("after: \"7 days\"", result);
            Assert.DoesNotContain("createdBefore:", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsCreatedBefore_WhenSet

        [Fact]
        public void AddCompressionPolicy_EmitsCreatedBefore_WhenSet()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                CreatedBefore = "30 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("createdBefore: \"30 days\"", result);
            Assert.DoesNotContain("after:", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsAllOptionalArgs

        [Fact]
        public void AddCompressionPolicy_EmitsAllOptionalArgs()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "metrics",
                After = "7 days",
                CreatedBefore = "30 days",
                ScheduleInterval = "12 hours",
                InitialStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Timezone = "Europe/Berlin",
                IfNotExists = true,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("schema: \"metrics\"", result);
            Assert.Contains("after: \"7 days\"", result);
            Assert.Contains("createdBefore: \"30 days\"", result);
            Assert.Contains("scheduleInterval: \"12 hours\"", result);
            Assert.Contains("initialStart:", result);
            Assert.Contains("timezone: \"Europe/Berlin\"", result);
            Assert.Contains("ifNotExists: true", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsSchema_WhenSet

        [Fact]
        public void AddCompressionPolicy_EmitsSchema_WhenSet()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "analytics",
                After = "14 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("schema: \"analytics\"", result);
        }

        #endregion

        #region AddCompressionPolicy_OmitsSchema_WhenEmpty

        [Fact]
        public void AddCompressionPolicy_OmitsSchema_WhenEmpty()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "",
                After = "7 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.DoesNotContain("schema:", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsScheduleInterval_WhenSet

        [Fact]
        public void AddCompressionPolicy_EmitsScheduleInterval_WhenSet()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                ScheduleInterval = "6 hours",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("scheduleInterval: \"6 hours\"", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsTimezone_WhenSet

        [Fact]
        public void AddCompressionPolicy_EmitsTimezone_WhenSet()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Timezone = "UTC",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("timezone: \"UTC\"", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsIfNotExists_WhenTrue

        [Fact]
        public void AddCompressionPolicy_EmitsIfNotExists_WhenTrue()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IfNotExists = true,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("ifNotExists: true", result);
        }

        #endregion

        #region AddCompressionPolicy_EmitsIfNotExists_WhenFalse

        [Fact]
        public void AddCompressionPolicy_EmitsIfNotExists_WhenFalse()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IfNotExists = false,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("ifNotExists: false", result);
        }

        #endregion

        #region AddCompressionPolicy_OmitsIfNotExists_WhenNull

        [Fact]
        public void AddCompressionPolicy_OmitsIfNotExists_WhenNull()
        {
            // Arrange
            AddCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IfNotExists = null,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.DoesNotContain("ifNotExists:", result);
        }

        #endregion

        // ── AlterCompressionPolicy ─────────────────────────────────────────────

        #region AlterCompressionPolicy_MinimalArgs_EmitsTableNameOnly

        [Fact]
        public void AlterCompressionPolicy_MinimalArgs_EmitsTableNameOnly()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("AlterCompressionPolicy", result);
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.DoesNotContain("after:", result);
            Assert.DoesNotContain("oldAfter:", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsNewAndOldAfter

        [Fact]
        public void AlterCompressionPolicy_EmitsNewAndOldAfter()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "14 days",
                OldAfter = "7 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("after: \"14 days\"", result);
            Assert.Contains("oldAfter: \"7 days\"", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsOldCreatedBefore

        [Fact]
        public void AlterCompressionPolicy_EmitsOldCreatedBefore()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                CreatedBefore = "60 days",
                OldCreatedBefore = "30 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("createdBefore: \"60 days\"", result);
            Assert.Contains("oldCreatedBefore: \"30 days\"", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsOldScheduleInterval

        [Fact]
        public void AlterCompressionPolicy_EmitsOldScheduleInterval()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "7 days",
                ScheduleInterval = "1 day",
                OldScheduleInterval = "4 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("scheduleInterval: \"1 day\"", result);
            Assert.Contains("oldScheduleInterval: \"4 days\"", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsOldInitialStart

        [Fact]
        public void AlterCompressionPolicy_EmitsOldInitialStart()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "7 days",
                InitialStart = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                OldInitialStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("initialStart:", result);
            Assert.Contains("oldInitialStart:", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsOldTimezone

        [Fact]
        public void AlterCompressionPolicy_EmitsOldTimezone()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "7 days",
                Timezone = "America/New_York",
                OldTimezone = "Europe/Berlin",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("timezone: \"America/New_York\"", result);
            Assert.Contains("oldTimezone: \"Europe/Berlin\"", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsOldIfNotExists

        [Fact]
        public void AlterCompressionPolicy_EmitsOldIfNotExists()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "7 days",
                IfNotExists = false,
                OldIfNotExists = true,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("ifNotExists: false", result);
            Assert.Contains("oldIfNotExists: true", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsAllNewAndOldArgs

        [Fact]
        public void AlterCompressionPolicy_EmitsAllNewAndOldArgs()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "metrics",
                After = "14 days",
                CreatedBefore = "60 days",
                ScheduleInterval = "1 day",
                InitialStart = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                Timezone = "UTC",
                IfNotExists = true,
                OldAfter = "7 days",
                OldCreatedBefore = "30 days",
                OldScheduleInterval = "12 hours",
                OldInitialStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                OldTimezone = "Europe/Berlin",
                OldIfNotExists = false,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("schema: \"metrics\"", result);
            Assert.Contains("after: \"14 days\"", result);
            Assert.Contains("createdBefore: \"60 days\"", result);
            Assert.Contains("scheduleInterval: \"1 day\"", result);
            Assert.Contains("initialStart:", result);
            Assert.Contains("timezone: \"UTC\"", result);
            Assert.Contains("ifNotExists: true", result);
            Assert.Contains("oldAfter: \"7 days\"", result);
            Assert.Contains("oldCreatedBefore: \"30 days\"", result);
            Assert.Contains("oldScheduleInterval: \"12 hours\"", result);
            Assert.Contains("oldInitialStart:", result);
            Assert.Contains("oldTimezone: \"Europe/Berlin\"", result);
            Assert.Contains("oldIfNotExists: false", result);
        }

        #endregion

        #region AlterCompressionPolicy_OmitsOldArgs_WhenAllNull

        [Fact]
        public void AlterCompressionPolicy_OmitsOldArgs_WhenAllNull()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                After = "7 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.DoesNotContain("oldAfter:", result);
            Assert.DoesNotContain("oldCreatedBefore:", result);
            Assert.DoesNotContain("oldScheduleInterval:", result);
            Assert.DoesNotContain("oldInitialStart:", result);
            Assert.DoesNotContain("oldTimezone:", result);
            Assert.DoesNotContain("oldIfNotExists:", result);
        }

        #endregion

        #region AlterCompressionPolicy_EmitsSchema_WhenSet

        [Fact]
        public void AlterCompressionPolicy_EmitsSchema_WhenSet()
        {
            // Arrange
            AlterCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "analytics",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("schema: \"analytics\"", result);
        }

        #endregion

        // ── DropCompressionPolicy ──────────────────────────────────────────────

        #region DropCompressionPolicy_MinimalArgs_EmitsTableNameOnly

        [Fact]
        public void DropCompressionPolicy_MinimalArgs_EmitsTableNameOnly()
        {
            // Arrange
            DropCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("DropCompressionPolicy", result);
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.DoesNotContain("schema:", result);
        }

        #endregion

        #region DropCompressionPolicy_EmitsSchema_WhenSet

        [Fact]
        public void DropCompressionPolicy_EmitsSchema_WhenSet()
        {
            // Arrange
            DropCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "metrics",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("schema: \"metrics\"", result);
        }

        #endregion

        #region DropCompressionPolicy_OmitsSchema_WhenEmpty

        [Fact]
        public void DropCompressionPolicy_OmitsSchema_WhenEmpty()
        {
            // Arrange
            DropCompressionPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = "",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.DoesNotContain("schema:", result);
        }

        #endregion
    }
}

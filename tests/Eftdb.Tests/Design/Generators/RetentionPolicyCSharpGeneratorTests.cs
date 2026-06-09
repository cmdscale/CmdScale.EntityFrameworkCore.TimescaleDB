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
    }
}

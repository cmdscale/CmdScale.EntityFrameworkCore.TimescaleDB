using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators
{
    /// <summary>
    /// Tests the actual C# text emitted by <see cref="ReorderPolicyCSharpGenerator"/> using a
    /// real <see cref="ICSharpHelper"/>.
    /// </summary>
    public class ReorderPolicyCSharpGeneratorTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(AddReorderPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ReorderPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(AlterReorderPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ReorderPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(DropReorderPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ReorderPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region AddReorderPolicy_EmitsRequiredArgsAndJobTuning

        [Fact]
        public void AddReorderPolicy_EmitsRequiredArgsAndJobTuning()
        {
            // Arrange
            AddReorderPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IndexName = "ix_ts",
                ScheduleInterval = "1 day",
                MaxRetries = 3,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("indexName: \"ix_ts\"", result);
            Assert.Contains("scheduleInterval: \"1 day\"", result);
            Assert.Contains("maxRetries: 3", result);
            Assert.DoesNotContain("maxRuntime:", result);
            Assert.DoesNotContain("retryPeriod:", result);
        }

        #endregion

        #region AlterReorderPolicy_EmitsOldArgsOnlyWhenNonDefault

        [Fact]
        public void AlterReorderPolicy_EmitsOldArgsOnlyWhenNonDefault()
        {
            // Arrange
            AlterReorderPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IndexName = "new_ix",
                OldIndexName = "old_ix",
                OldScheduleInterval = "4 days",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("indexName: \"new_ix\"", result);
            Assert.Contains("oldIndexName: \"old_ix\"", result);
            Assert.Contains("oldScheduleInterval: \"4 days\"", result);
            Assert.DoesNotContain("oldMaxRuntime:", result);
        }

        #endregion

        #region DropReorderPolicy_OmitsEmptySchema

        [Fact]
        public void DropReorderPolicy_OmitsEmptySchema()
        {
            // Arrange
            DropReorderPolicyOperation op = new()
            {
                TableName = "sensor_data",
                Schema = string.Empty,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.DoesNotContain("schema:", result);
        }

        #endregion
    }
}

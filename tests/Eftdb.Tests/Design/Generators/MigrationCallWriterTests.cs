using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators
{
    /// <summary>
    /// Tests for the internal <c>MigrationCallWriter</c> behavior. The type is internal in the
    /// <c>Eftdb.Design</c> assembly and that assembly does NOT expose InternalsVisibleTo to the
    /// test project, so it is exercised indirectly through the public
    /// <see cref="ReorderPolicyCSharpGenerator"/> (the simplest consumer).
    /// </summary>
    public class MigrationCallWriterTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string GenerateAddReorder(AddReorderPolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ReorderPolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region FirstArg_HasNoLeadingComma_LaterArgsCommaSeparatedOnNewLines

        [Fact]
        public void FirstArg_HasNoLeadingComma_LaterArgsCommaSeparatedOnNewLines()
        {
            // Arrange
            AddReorderPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IndexName = "ix_ts",
                ScheduleInterval = "1 day",
            };

            // Act
            string result = GenerateAddReorder(op);
            string[] lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            // Assert
            Assert.Contains(lines, l => l.TrimEnd().EndsWith(".AddReorderPolicy("));

            string trimmed = result.Replace("\r", string.Empty);
            int tableNameIndex = trimmed.IndexOf("tableName:");
            string beforeTableName = trimmed[..tableNameIndex];
            Assert.DoesNotContain(",", beforeTableName);

            Assert.Contains(",\n", trimmed);
        }

        #endregion

        #region Dispose_AppendsClosingParen

        [Fact]
        public void Dispose_AppendsClosingParen()
        {
            // Arrange
            AddReorderPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IndexName = "ix_ts",
            };

            // Act
            string result = GenerateAddReorder(op);

            // Assert
            Assert.EndsWith(")", result.TrimEnd());
        }

        #endregion

        #region NamedArgFormat_NameColonSpaceValue

        [Fact]
        public void NamedArgFormat_NameColonSpaceValue()
        {
            // Arrange
            AddReorderPolicyOperation op = new()
            {
                TableName = "sensor_data",
                IndexName = "ix_ts",
            };

            // Act
            string result = GenerateAddReorder(op);

            // Assert
            Assert.Contains("tableName: \"sensor_data\"", result);
            Assert.Contains("indexName: \"ix_ts\"", result);
        }

        #endregion
    }
}

using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Features.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators
{
    /// <summary>
    /// Tests for the internal <c>CSharpGeneratorHelper</c> behavior. The type is internal in the
    /// <c>Eftdb.Design</c> assembly which does NOT expose InternalsVisibleTo to the test project,
    /// so its two behaviors are exercised indirectly through public generators:
    /// <list type="bullet">
    /// <item><c>LiteralStringList</c> via <see cref="HypertableCSharpGenerator"/> string list args.</item>
    /// <item><c>StaticCall</c> (with int rendered unquoted via UnknownLiteral) via the
    /// <c>additionalDimensions</c> hash dimension emission.</item>
    /// </list>
    /// </summary>
    public class CSharpGeneratorHelperTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(CreateHypertableOperation operation)
        {
            IndentedStringBuilder builder = new();
            new HypertableCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region LiteralStringList_RendersBracketedQuotedCommaSeparated

        [Fact]
        public void LiteralStringList_RendersBracketedQuotedCommaSeparated()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "t",
                TimeColumnName = "ts",
                ChunkSkipColumns = ["a", "b"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("[\"a\", \"b\"]", result);
        }

        #endregion

        #region LiteralStringList_SingleElement

        [Fact]
        public void LiteralStringList_SingleElement()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "t",
                TimeColumnName = "ts",
                CompressionSegmentBy = ["device_id"],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("compressionSegmentBy: [\"device_id\"]", result);
        }

        #endregion

        #region StaticCall_RendersIntArgUnquoted

        [Fact]
        public void StaticCall_RendersIntArgUnquoted()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "t",
                TimeColumnName = "ts",
                AdditionalDimensions = [Dimension.CreateHash("hash_col", 4)],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains(".CreateHash(\"hash_col\", 4)", result);
            Assert.DoesNotContain("CreateHash(\"hash_col\", \"4\")", result);
        }

        #endregion

        #region StaticCall_RendersStringArgQuoted

        [Fact]
        public void StaticCall_RendersStringArgQuoted()
        {
            // Arrange
            CreateHypertableOperation op = new()
            {
                TableName = "t",
                TimeColumnName = "ts",
                AdditionalDimensions = [Dimension.CreateRange("range_col", "1 day")],
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains(".CreateRange(\"range_col\", \"1 day\")", result);
        }

        #endregion
    }
}

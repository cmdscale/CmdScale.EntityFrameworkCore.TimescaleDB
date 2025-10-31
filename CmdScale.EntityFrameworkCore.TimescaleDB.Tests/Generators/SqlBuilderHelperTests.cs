using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Moq;
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators
{
#pragma warning disable EF1001 // Internal EF Core API usage.

    public class SqlBuilderHelperTests
    {
        [Fact]
        public void Regclass_Runtime_ReturnsCorrectlyQuotedString()
        {
            // Arrange
            SqlBuilderHelper helper = new(quoteString: "\"");
            string tableName = "MyTable";
            string expected = "'\"MyTable\"'";

            // Act
            string result = helper.Regclass(tableName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void QualifiedIdentifier_Runtime_ReturnsCorrectlyQuotedString()
        {
            // Arrange
            SqlBuilderHelper helper = new(quoteString: "\"");
            string tableName = "MyTable";
            string expected = "\"MyTable\"";

            // Act
            string result = helper.QualifiedIdentifier(tableName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Regclass_DesignTime_ReturnsCorrectlyEscapedQuotedString()
        {
            // Arrange
            SqlBuilderHelper helper = new(quoteString: "\"\"");
            string tableName = "MyTable";
            string expected = "'\"\"MyTable\"\"'";

            // Act
            string result = helper.Regclass(tableName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void QualifiedIdentifier_DesignTime_ReturnsCorrectlyEscapedQuotedString()
        {
            // Arrange
            SqlBuilderHelper helper = new(quoteString: "\"\"");
            string tableName = "MyTable";
            string expected = "\"\"MyTable\"\"";

            // Act
            string result = helper.QualifiedIdentifier(tableName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuildQueryString_IndentedStringBuilder_WritesCorrectCSharpSql()
        {
            // Arrange
            List<string> statements = ["SELECT 1;", "SELECT 2;"];
            IndentedStringBuilder indentedBuilder = new();
            string expected = @".Sql(@""
                SELECT 1;
                SELECT 2;
            "")";

            // Act
            SqlBuilderHelper.BuildQueryString(statements, indentedBuilder);
            string result = indentedBuilder.ToString();

            // Assert
            Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        }

        [Fact]
        public void BuildQueryString_IndentedStringBuilder_WritesNothingForEmptyList()
        {
            // Arrange
            List<string> statements = [];
            IndentedStringBuilder indentedBuilder = new();

            // Act
            SqlBuilderHelper.BuildQueryString(statements, indentedBuilder);
            string result = indentedBuilder.ToString();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_AppendsAndEndsCommands()
        {
            // Arrange
            List<string> statements = ["SELECT 1;", "SELECT 2;"];
            MigrationsSqlGeneratorDependencies dependencies = new(
                Mock.Of<IRelationalCommandBuilderFactory>(),
                Mock.Of<IUpdateSqlGenerator>(),
                Mock.Of<ISqlGenerationHelper>(),
                Mock.Of<IRelationalTypeMappingSource>(),
                Mock.Of<ICurrentDbContext>(),
                Mock.Of<IModificationCommandFactory>(),
                Mock.Of<ILoggingOptions>(),
                Mock.Of<IRelationalCommandDiagnosticsLogger>(),
                Mock.Of<IDiagnosticsLogger<DbLoggerCategory.Migrations>>()
            );

            Mock<MigrationCommandListBuilder> mockBuilder = new(dependencies);

            mockBuilder.Setup(b => b.Append(It.IsAny<string>())).Returns(mockBuilder.Object);
            mockBuilder.Setup(b => b.EndCommand(It.IsAny<bool>())).Returns(mockBuilder.Object);

            // Act
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object);

            // Assert
            mockBuilder.Verify(b => b.Append("SELECT 1;"), Times.Once);
            mockBuilder.Verify(b => b.Append("SELECT 2;"), Times.Once);
            mockBuilder.Verify(b => b.EndCommand(It.IsAny<bool>()), Times.Exactly(2));
        }

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_WritesNothingForEmptyList()
        {
            // Arrange
            List<string> statements = [];
            MigrationsSqlGeneratorDependencies dependencies = new(
                Mock.Of<IRelationalCommandBuilderFactory>(),
                Mock.Of<IUpdateSqlGenerator>(),
                Mock.Of<ISqlGenerationHelper>(),
                Mock.Of<IRelationalTypeMappingSource>(),
                Mock.Of<ICurrentDbContext>(),
                Mock.Of<IModificationCommandFactory>(),
                Mock.Of<ILoggingOptions>(),
                Mock.Of<IRelationalCommandDiagnosticsLogger>(),
                Mock.Of<IDiagnosticsLogger<DbLoggerCategory.Migrations>>()
            );

            Mock<MigrationCommandListBuilder> mockBuilder = new(dependencies);

            // Act
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object);

            // Assert
            mockBuilder.Verify(b => b.Append(It.IsAny<string>()), Times.Never);
            mockBuilder.Verify(b => b.EndCommand(It.IsAny<bool>()), Times.Never);
        }
    }
#pragma warning restore EF1001 // Internal EF Core API usage.
}
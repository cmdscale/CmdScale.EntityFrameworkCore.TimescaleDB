using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
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
            string tableName = "MyTable";
            string expected = "'public.\"MyTable\"'";

            // Act
            string result = SqlBuilderHelper.Regclass(tableName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void QualifiedIdentifier_Runtime_ReturnsCorrectlyQuotedString()
        {
            // Arrange
            string tableName = "MyTable";
            string expected = "\"public\".\"MyTable\"";

            // Act
            string result = SqlBuilderHelper.QualifiedIdentifier(tableName);

            // Assert
            Assert.Equal(expected, result);
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
        #region EscapeStringLiteral

        [Fact]
        public void EscapeStringLiteral_DoublesSingleQuotes()
        {
            // Act
            string result = SqlBuilderHelper.EscapeStringLiteral("it's a 'test'");

            // Assert
            Assert.Equal("it''s a ''test''", result);
        }

        [Fact]
        public void EscapeStringLiteral_ReturnsUnchangedWithoutQuotes()
        {
            // Act
            string result = SqlBuilderHelper.EscapeStringLiteral("plain_value");

            // Assert
            Assert.Equal("plain_value", result);
        }

        #endregion

        #region IntervalOrBigint

        [Fact]
        public void IntervalOrBigint_NumericValue_EmitsBigintCast()
        {
            // Act
            string result = SqlBuilderHelper.IntervalOrBigint("604800000000");

            // Assert
            Assert.Equal("604800000000::bigint", result);
        }

        [Fact]
        public void IntervalOrBigint_IntervalString_EmitsEscapedIntervalLiteral()
        {
            // Act
            string result = SqlBuilderHelper.IntervalOrBigint("7 days");

            // Assert
            Assert.Equal("INTERVAL '7 days'", result);
        }

        [Fact]
        public void IntervalOrBigint_Iso8601Duration_EmitsIntervalLiteral()
        {
            // Act
            string result = SqlBuilderHelper.IntervalOrBigint("P7D");

            // Assert
            Assert.Equal("INTERVAL 'P7D'", result);
        }

        #endregion

        #region ReplaceSelectWithPerform_NonSelect_Returns_Unchanged

        [Fact]
        public void ReplaceSelectWithPerform_NonSelect_Returns_Unchanged()
        {
            // Arrange
            string input = "INSERT INTO x VALUES (1);";

            // Act
            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);

            // Assert
            Assert.Equal(input, result);
        }

        #endregion

        #region ReplaceSelectWithPerform

        [Fact]
        public void ReplaceSelectWithPerform_ReplacesLeadingSelect()
        {
            string input = "SELECT create_hypertable('public.\"Events\"', 'CapturedAt');";
            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);
            Assert.Equal("PERFORM create_hypertable('public.\"Events\"', 'CapturedAt');", result);
        }

        [Fact]
        public void ReplaceSelectWithPerform_PreservesLeadingWhitespace()
        {
            string input = "    SELECT add_dimension('public.\"Events\"', by_range('sensor_id'));";
            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);
            Assert.Equal("    PERFORM add_dimension('public.\"Events\"', by_range('sensor_id'));", result);
        }

        [Fact]
        public void ReplaceSelectWithPerform_PreservesNonSelectStatements()
        {
            string input = "ALTER TABLE \"public\".\"Events\" SET (timescaledb.compress = true);";
            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void ReplaceSelectWithPerform_PreservesDoBlocks()
        {
            string input = "DO $$ BEGIN EXECUTE 'SELECT 1'; END $$;";
            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);
            Assert.Equal(input, result);
        }

        [Fact]
        public void ReplaceSelectWithPerform_IsCaseInsensitive()
        {
            string input = "select remove_retention_policy('public.\"Events\"', if_exists => true);";
            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);
            Assert.StartsWith("PERFORM", result);
        }

        [Fact]
        public void ReplaceSelectWithPerform_HandlesMultiLineAlterJob()
        {
            string input = @"
                SELECT alter_job(job_id, schedule_interval => INTERVAL '2 days')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'TestTable';".Trim();

            string result = SqlBuilderHelper.ReplaceSelectWithPerform(input);

            Assert.StartsWith("PERFORM alter_job", result);
            Assert.Contains("FROM timescaledb_information.jobs", result);
            Assert.DoesNotContain("SELECT", result);
        }

        [Fact]
        public void ReplaceSelectWithPerformMultiLine_ReplacesAllSelectStatements()
        {
            string input = """
                SELECT create_hypertable('public."Events"', 'Time');
                SELECT add_dimension('public."Events"', by_range('sensor_id', 100::bigint));
                SELECT alter_job(job_id, schedule_interval => INTERVAL '1 day')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention';
            """;

            string result = SqlBuilderHelper.ReplaceSelectWithPerformMultiLine(input);

            Assert.DoesNotContain("SELECT create_hypertable", result);
            Assert.DoesNotContain("SELECT add_dimension", result);
            Assert.DoesNotContain("SELECT alter_job", result);
            Assert.Contains("PERFORM create_hypertable", result);
            Assert.Contains("PERFORM add_dimension", result);
            Assert.Contains("PERFORM alter_job", result);
            Assert.Contains("FROM timescaledb_information.jobs", result);
        }

        [Fact]
        public void ReplaceSelectWithPerformMultiLine_PreservesDoBlocks()
        {
            string input = """
                SELECT create_hypertable('public."Events"', 'Time');
                DO $$
                BEGIN
                    EXECUTE 'SELECT enable_chunk_skipping(...)';
                END $$;
            """;

            string result = SqlBuilderHelper.ReplaceSelectWithPerformMultiLine(input);

            Assert.Contains("PERFORM create_hypertable", result);
            Assert.Contains("DO $$", result);
            Assert.Contains("EXECUTE 'SELECT enable_chunk_skipping(...)'", result);
        }

        #endregion

        #region BuildQueryString_MigrationCommandListBuilder_UsePerform

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_UsePerform_ReplacesSelectWithPerform()
        {
            // Arrange
            List<string> statements = ["SELECT create_hypertable('public.\"Events\"', 'Time');"];
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
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object, usePerform: true);

            // Assert
            mockBuilder.Verify(b => b.Append(It.Is<string>(s => s.StartsWith("PERFORM"))), Times.Once);
            mockBuilder.Verify(b => b.Append(It.Is<string>(s => s.StartsWith("SELECT"))), Times.Never);
        }

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_UsePerform_False_PreservesSelect()
        {
            // Arrange
            List<string> statements = ["SELECT create_hypertable('public.\"Events\"', 'Time');"];
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
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object, usePerform: false);

            // Assert
            mockBuilder.Verify(b => b.Append(It.Is<string>(s => s.StartsWith("SELECT"))), Times.Once);
        }

        #endregion

        #region BuildQueryString_MigrationCommandListBuilder_Grouping

        private static Mock<MigrationCommandListBuilder> CreateMockBuilder()
        {
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
            return mockBuilder;
        }

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_GroupsNonSemicolonLinesIntoSingleCommand()
        {
            // Arrange
            List<string> statements =
            [
                "DO $$",
                "BEGIN",
                "    EXECUTE 'SELECT 1';",
                "END $$;"
            ];
            Mock<MigrationCommandListBuilder> mockBuilder = CreateMockBuilder();

            // Act
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object);

            // Assert
            mockBuilder.Verify(b => b.EndCommand(It.IsAny<bool>()), Times.Once);
            mockBuilder.Verify(
                b => b.Append(It.Is<string>(s => s.Contains("DO $$") && s.Contains("END $$;"))),
                Times.Once);
        }

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_SuppressTransaction_IsForwarded()
        {
            // Arrange
            List<string> statements = ["SELECT 1;", "SELECT 2;"];
            Mock<MigrationCommandListBuilder> mockBuilder = CreateMockBuilder();

            // Act
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object, suppressTransaction: true);

            // Assert
            mockBuilder.Verify(b => b.EndCommand(true), Times.Exactly(2));
            mockBuilder.Verify(b => b.EndCommand(false), Times.Never);
        }

        [Fact]
        public void BuildQueryString_MigrationCommandListBuilder_UsePerform_And_SuppressTransaction_Combined()
        {
            // Arrange
            List<string> statements = ["SELECT create_hypertable('public.\"Events\"', 'Time');"];
            Mock<MigrationCommandListBuilder> mockBuilder = CreateMockBuilder();

            // Act
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object, suppressTransaction: true, usePerform: true);

            // Assert
            mockBuilder.Verify(b => b.Append(It.Is<string>(s => s.StartsWith("PERFORM"))), Times.Once);
            mockBuilder.Verify(b => b.EndCommand(true), Times.Once);
        }

        #endregion

        #region BuildQueryString_Flushes_Trailing_Group_When_No_Terminating_Semicolon

        [Fact]
        public void BuildQueryString_Flushes_Trailing_Group_When_No_Terminating_Semicolon()
        {
            // Arrange
            List<string> statements =
            [
                "ALTER TABLE \"public\".\"events\" SET (timescaledb.compress = true)",
                "ALTER TABLE \"public\".\"events\" SET (timescaledb.compress_orderby = 'ts DESC')"
            ];
            Mock<MigrationCommandListBuilder> mockBuilder = CreateMockBuilder();

            // Act
            SqlBuilderHelper.BuildQueryString(statements, mockBuilder.Object);

            // Assert
            mockBuilder.Verify(b => b.EndCommand(It.IsAny<bool>()), Times.Once);
            mockBuilder.Verify(
                b => b.Append(It.Is<string>(s => s.Contains("timescaledb.compress = true") && s.Contains("timescaledb.compress_orderby"))),
                Times.Once);
        }

        #endregion
    }
#pragma warning restore EF1001 // Internal EF Core API usage.
}

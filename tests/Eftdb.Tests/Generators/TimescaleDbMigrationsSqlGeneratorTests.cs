using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators;

public class TimescaleDbMigrationsSqlGeneratorTests
{
    private class TestContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();
    }

    private static string GenerateSql(
        List<MigrationOperation> operations,
        MigrationsSqlGenerationOptions? options = null)
    {
        using TestContext context = new();
        IMigrationsSqlGenerator sqlGenerator = context.GetService<IMigrationsSqlGenerator>();

        IReadOnlyList<MigrationCommand> commands = options.HasValue
            ? sqlGenerator.Generate(operations, context.Model, options.Value)
            : sqlGenerator.Generate(operations, context.Model);

        return string.Join("\n", commands.Select(c => c.CommandText));
    }

    #region Should_Use_Perform_For_CreateHypertable_In_Idempotent_Mode

    [Fact]
    public void Should_Use_Perform_For_CreateHypertable_In_Idempotent_Mode()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "IdempotentHypertable",
            Schema = "public",
            TimeColumnName = "time"
        };
        List<MigrationOperation> operations = [operation];

        // Act
        string sql = GenerateSql(operations, MigrationsSqlGenerationOptions.Idempotent | MigrationsSqlGenerationOptions.Script);

        // Assert
        Assert.Contains("PERFORM create_hypertable", sql);
        Assert.DoesNotContain("SELECT create_hypertable", sql);
    }

    #endregion

    #region Should_Use_Select_For_CreateHypertable_In_NonIdempotent_Mode

    [Fact]
    public void Should_Use_Select_For_CreateHypertable_In_NonIdempotent_Mode()
    {
        // Arrange
        CreateHypertableOperation operation = new()
        {
            TableName = "NonIdempotentHypertable",
            Schema = "public",
            TimeColumnName = "time"
        };
        List<MigrationOperation> operations = [operation];

        // Act
        string sql = GenerateSql(operations);

        // Assert
        Assert.Contains("SELECT create_hypertable", sql);
        Assert.DoesNotContain("PERFORM", sql);
    }

    #endregion

    #region Should_Use_Perform_For_SqlOperation_In_Idempotent_Mode

    [Fact]
    public void Should_Use_Perform_For_SqlOperation_In_Idempotent_Mode()
    {
        // Arrange
        SqlOperation operation = new()
        {
            Sql = "SELECT add_retention_policy('public.\"TestTable\"', drop_after => INTERVAL '30 days');"
        };
        List<MigrationOperation> operations = [operation];

        // Act
        string sql = GenerateSql(operations, MigrationsSqlGenerationOptions.Idempotent | MigrationsSqlGenerationOptions.Script);

        // Assert
        Assert.Contains("PERFORM add_retention_policy", sql);
        Assert.DoesNotContain("SELECT add_retention", sql);
    }

    #endregion

    #region Should_Preserve_Select_For_DDL_SqlOperation_In_Idempotent_Mode

    [Fact]
    public void Should_Preserve_Select_For_DDL_SqlOperation_In_Idempotent_Mode()
    {
        // Arrange
        SqlOperation operation = new()
        {
            Sql = "CREATE MATERIALIZED VIEW daily_agg AS\nSELECT time_bucket('1 day', time) AS bucket, avg(value) FROM metrics GROUP BY bucket;"
        };
        List<MigrationOperation> operations = [operation];

        // Act
        string sql = GenerateSql(operations, MigrationsSqlGenerationOptions.Idempotent | MigrationsSqlGenerationOptions.Script);

        // Assert — DDL statement passes through unchanged; SELECT inside CREATE ... AS SELECT must not be replaced
        Assert.Contains("SELECT", sql);
        Assert.Contains("CREATE MATERIALIZED VIEW", sql);
    }

    #endregion

    #region Should_Use_Select_For_SqlOperation_In_NonIdempotent_Mode

    [Fact]
    public void Should_Use_Select_For_SqlOperation_In_NonIdempotent_Mode()
    {
        // Arrange
        SqlOperation operation = new()
        {
            Sql = "SELECT add_retention_policy('public.\"TestTable\"', drop_after => INTERVAL '30 days');"
        };
        List<MigrationOperation> operations = [operation];

        // Act
        string sql = GenerateSql(operations);

        // Assert
        Assert.Contains("SELECT add_retention_policy", sql);
        Assert.DoesNotContain("PERFORM", sql);
    }

    #endregion
}

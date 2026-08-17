using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Generators;

public class CompressionPolicyOperationGeneratorTests
{
    private static string GetGeneratedCode(dynamic operation)
    {
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);
        return string.Join("\n", statements);
    }

    #region Generate_Add_After_with_minimal_config_creates_add_policy_sql

    [Fact]
    public void Generate_Add_After_with_minimal_config_creates_add_policy_sql()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days"
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '7 days');
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_Numeric_After_emits_bigint_for_integer_time_columns

    [Fact]
    public void Generate_Add_Numeric_After_emits_bigint_for_integer_time_columns()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "604800000000"
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => 604800000000::bigint);
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_CreatedBefore_creates_created_before_sql

    [Fact]
    public void Generate_Add_CreatedBefore_creates_created_before_sql()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            CreatedBefore = "30 days"
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', created_before => INTERVAL '30 days');
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        Assert.DoesNotContain("after =>", result);
    }

    #endregion

    #region Generate_Add_After_with_ScheduleInterval_includes_schedule_interval_arg

    [Fact]
    public void Generate_Add_After_with_ScheduleInterval_includes_schedule_interval_arg()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days",
            ScheduleInterval = "12 hours"
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '7 days', schedule_interval => INTERVAL '12 hours');
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_with_InitialStart_includes_iso_8601_timestamp

    [Fact]
    public void Generate_Add_with_InitialStart_includes_iso_8601_timestamp()
    {
        // Arrange
        DateTime testDate = new(2025, 10, 20, 12, 30, 0, DateTimeKind.Utc);
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days",
            InitialStart = testDate
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '7 days', initial_start => '2025-10-20T12:30:00.0000000Z');
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_with_Timezone_includes_timezone_arg

    [Fact]
    public void Generate_Add_with_Timezone_includes_timezone_arg()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days",
            Timezone = "Europe/Berlin"
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '7 days', timezone => 'Europe/Berlin');
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_with_IfNotExists_includes_if_not_exists_arg

    [Fact]
    public void Generate_Add_with_IfNotExists_includes_if_not_exists_arg()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days",
            IfNotExists = true
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '7 days', if_not_exists => true);
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_with_all_optional_args_creates_fully_qualified_sql

    [Fact]
    public void Generate_Add_with_all_optional_args_creates_fully_qualified_sql()
    {
        // Arrange
        DateTime testDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "14 days",
            ScheduleInterval = "12 hours",
            InitialStart = testDate,
            Timezone = "UTC",
            IfNotExists = true
        };

        string expected = @"
            CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '14 days', schedule_interval => INTERVAL '12 hours', initial_start => '2025-01-01T00:00:00.0000000Z', timezone => 'UTC', if_not_exists => true);
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_with_custom_schema_uses_correct_regclass_quoting

    [Fact]
    public void Generate_Add_with_custom_schema_uses_correct_regclass_quoting()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "analytics",
            TableName = "EventLogs",
            After = "7 days"
        };

        string expected = @"
            CALL add_columnstore_policy('analytics.""EventLogs""', after => INTERVAL '7 days');
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Alter_creates_remove_then_add_sql

    [Fact]
    public void Generate_Alter_creates_remove_then_add_sql()
    {
        // Arrange
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "14 days",
            OldAfter = "7 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Contains("remove_columnstore_policy", statements[0]);
        Assert.Contains("add_columnstore_policy", statements[1]);
    }

    #endregion

    #region Generate_Alter_remove_uses_if_exists_true

    [Fact]
    public void Generate_Alter_remove_uses_if_exists_true()
    {
        // Arrange
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "14 days",
            OldAfter = "7 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Contains("if_exists => true", statements[0]);
    }

    #endregion

    #region Generate_Alter_After_creates_correct_add_sql

    [Fact]
    public void Generate_Alter_After_creates_correct_add_sql()
    {
        // Arrange
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "14 days",
            OldAfter = "7 days"
        };

        string expectedRemove = @"CALL remove_columnstore_policy('public.""TestTable""', if_exists => true);";
        string expectedAdd = @"CALL add_columnstore_policy('public.""TestTable""', after => INTERVAL '14 days');";

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Equal(SqlHelper.NormalizeSql(expectedRemove), SqlHelper.NormalizeSql(statements[0]));
        Assert.Equal(SqlHelper.NormalizeSql(expectedAdd), SqlHelper.NormalizeSql(statements[1]));
    }

    #endregion

    #region Generate_Alter_switches_to_CreatedBefore

    [Fact]
    public void Generate_Alter_switches_to_CreatedBefore()
    {
        // Arrange
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = null,
            OldAfter = "7 days",
            CreatedBefore = "30 days",
            OldCreatedBefore = null
        };

        string expectedAdd = @"CALL add_columnstore_policy('public.""TestTable""', created_before => INTERVAL '30 days');";

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Equal(SqlHelper.NormalizeSql(expectedAdd), SqlHelper.NormalizeSql(statements[1]));
        Assert.DoesNotContain("after =>", statements[1]);
    }

    #endregion

    #region Generate_Alter_with_all_optional_args

    [Fact]
    public void Generate_Alter_with_all_optional_args()
    {
        // Arrange
        DateTime testDate = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "14 days",
            OldAfter = "7 days",
            ScheduleInterval = "6 hours",
            InitialStart = testDate,
            Timezone = "Europe/Berlin",
            IfNotExists = true
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Contains("after => INTERVAL '14 days'", statements[1]);
        Assert.Contains("schedule_interval => INTERVAL '6 hours'", statements[1]);
        Assert.Contains("2026-06-15T00:00:00.0000000Z", statements[1]);
        Assert.Contains("timezone => 'Europe/Berlin'", statements[1]);
        Assert.Contains("if_not_exists => true", statements[1]);
    }

    #endregion

    #region Generate_Alter_with_custom_schema_uses_correct_regclass_quoting

    [Fact]
    public void Generate_Alter_with_custom_schema_uses_correct_regclass_quoting()
    {
        // Arrange
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "analytics",
            TableName = "EventLogs",
            After = "14 days",
            OldAfter = "7 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.All(statements, s => Assert.Contains("'analytics.\"EventLogs\"'", s));
    }

    #endregion

    #region Generate_Drop_creates_correct_remove_policy_sql

    [Fact]
    public void Generate_Drop_creates_correct_remove_policy_sql()
    {
        // Arrange
        DropCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable"
        };

        string expected = @"
            CALL remove_columnstore_policy('public.""TestTable""', if_exists => true);
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Drop_with_custom_schema_uses_correct_regclass_quoting

    [Fact]
    public void Generate_Drop_with_custom_schema_uses_correct_regclass_quoting()
    {
        // Arrange
        DropCompressionPolicyOperation operation = new()
        {
            Schema = "analytics",
            TableName = "EventLogs"
        };

        string expected = @"
            CALL remove_columnstore_policy('analytics.""EventLogs""', if_exists => true);
        ";

        // Act
        string result = GetGeneratedCode(operation);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Generate_Add_After_does_not_emit_created_before

    [Fact]
    public void Generate_Add_After_does_not_emit_created_before()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        string statement = Assert.Single(statements);
        Assert.Contains("after => INTERVAL '7 days'", statement);
        Assert.DoesNotContain("created_before =>", statement);
    }

    #endregion

    #region Generate_Add_CreatedBefore_does_not_emit_after

    [Fact]
    public void Generate_Add_CreatedBefore_does_not_emit_after()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            CreatedBefore = "30 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        string statement = Assert.Single(statements);
        Assert.Contains("created_before => INTERVAL '30 days'", statement);
        Assert.DoesNotContain("after => INTERVAL", statement);
    }

    #endregion

    #region Generate_Add_uses_single_quote_regclass

    [Fact]
    public void Generate_Add_uses_single_quote_regclass()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Contains("'public.\"TestTable\"'", Assert.Single(statements));
    }

    #endregion

    #region Generate_Drop_uses_single_quote_regclass

    [Fact]
    public void Generate_Drop_uses_single_quote_regclass()
    {
        // Arrange
        DropCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation);

        // Assert
        Assert.Contains("'public.\"TestTable\"'", Assert.Single(statements));
    }

    #endregion

    // ── Legacy mode ───────────────────────────────────────────────────────────

    #region Legacy_Generate_Add_After_emits_select_add_compression_policy

    [Fact]
    public void Legacy_Generate_Add_After_emits_select_add_compression_policy()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "7 days"
        };

        string expected = @"
            SELECT add_compression_policy('public.""TestTable""', compress_after => INTERVAL '7 days');
        ";

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation, useLegacyCompressionNames: true);
        string result = string.Join("\n", statements);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Legacy_Generate_Add_CreatedBefore_emits_compress_created_before

    [Fact]
    public void Legacy_Generate_Add_CreatedBefore_emits_compress_created_before()
    {
        // Arrange
        AddCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            CreatedBefore = "30 days"
        };

        string expected = @"
            SELECT add_compression_policy('public.""TestTable""', compress_created_before => INTERVAL '30 days');
        ";

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation, useLegacyCompressionNames: true);
        string result = string.Join("\n", statements);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
        Assert.DoesNotContain("compress_after", result);
    }

    #endregion

    #region Legacy_Generate_Drop_emits_select_remove_compression_policy

    [Fact]
    public void Legacy_Generate_Drop_emits_select_remove_compression_policy()
    {
        // Arrange
        DropCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable"
        };

        string expected = @"
            SELECT remove_compression_policy('public.""TestTable""', if_exists => true);
        ";

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation, useLegacyCompressionNames: true);
        string result = string.Join("\n", statements);

        // Assert
        Assert.Equal(SqlHelper.NormalizeSql(expected), SqlHelper.NormalizeSql(result));
    }

    #endregion

    #region Legacy_Generate_Alter_emits_legacy_remove_then_add

    [Fact]
    public void Legacy_Generate_Alter_emits_legacy_remove_then_add()
    {
        // Arrange
        AlterCompressionPolicyOperation operation = new()
        {
            Schema = "public",
            TableName = "TestTable",
            After = "14 days",
            OldAfter = "7 days"
        };

        // Act
        List<string> statements = CompressionPolicySqlGenerator.Generate(operation, useLegacyCompressionNames: true);

        // Assert
        Assert.Equal(2, statements.Count);
        Assert.Contains("SELECT remove_compression_policy", statements[0]);
        Assert.Contains("SELECT add_compression_policy", statements[1]);
        Assert.Contains("compress_after => INTERVAL '14 days'", statements[1]);
    }

    #endregion
}

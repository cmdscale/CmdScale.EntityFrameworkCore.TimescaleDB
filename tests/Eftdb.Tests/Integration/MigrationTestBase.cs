using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Base class for migration lifecycle tests.
/// Provides helper methods to simulate EF Core migration workflows.
/// </summary>
public abstract class MigrationTestBase
{
    protected static IReadOnlyList<MigrationOperation> GenerateMigrationOperations(
        DbContext? sourceContext,
        DbContext targetContext)
    {
        IMigrationsModelDiffer differ = targetContext.GetService<IMigrationsModelDiffer>();

        IRelationalModel? sourceModel = sourceContext?.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        IRelationalModel targetModel = targetContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        return differ.GetDifferences(sourceModel, targetModel);
    }

    protected static async Task ApplyMigrationAsync(
        DbContext context,
        IReadOnlyList<MigrationOperation> operations)
    {
        IMigrationsSqlGenerator sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        IReadOnlyList<MigrationCommand> commands = sqlGenerator.Generate(operations, context.Model);

        // Group commands: when we encounter a SET command, batch it with the next command
        // to preserve session state (PostgreSQL SET commands are session-scoped)
        List<string> currentBatch = [];

        foreach (MigrationCommand command in commands)
        {
            string sql = command.CommandText.Trim();

            if (sql.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
            {
                currentBatch.Add(sql.TrimEnd(';'));
            }
            else
            {
                currentBatch.Add(sql.TrimEnd(';'));

                string batchSql = string.Join(";\n", currentBatch);
                await context.Database.ExecuteSqlRawAsync(batchSql);

                currentBatch.Clear();
            }
        }

        // Execute any remaining SET commands (shouldn't happen, but handle edge case)
        if (currentBatch.Count > 0)
        {
            string batchSql = string.Join(";\n", currentBatch);
            await context.Database.ExecuteSqlRawAsync(batchSql);
        }
    }

    protected static async Task ExecuteMigrationAsync(
        DbContext? sourceContext,
        DbContext targetContext)
    {
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(sourceContext, targetContext);
        await ApplyMigrationAsync(targetContext, operations);
    }

    /// <summary>
    /// Creates the database schema without using EnsureCreated (uses migration pipeline instead).
    /// </summary>
    protected static async Task CreateDatabaseViaMigrationAsync(DbContext context)
    {
        await ExecuteMigrationAsync(null, context);
    }

    protected static async Task AlterDatabaseViaMigrationAsync(
        DbContext oldContext,
        DbContext newContext)
    {
        await ExecuteMigrationAsync(oldContext, newContext);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
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
    /// <summary>
    /// Simulates "dotnet ef migrations add" by comparing two models and generating operations.
    /// </summary>
    protected static IReadOnlyList<MigrationOperation> GenerateMigrationOperations(
        DbContext? sourceContext,
        DbContext targetContext)
    {
        IMigrationsModelDiffer differ = targetContext.GetService<IMigrationsModelDiffer>();

        IRelationalModel? sourceModel = sourceContext?.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        IRelationalModel targetModel = targetContext.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        return differ.GetDifferences(sourceModel, targetModel);
    }

    /// <summary>
    /// Simulates "dotnet ef database update" by generating SQL from operations and executing them.
    /// Groups commands with SET statements to maintain PostgreSQL session state.
    /// </summary>
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

            // Check if this is a SET command
            if (sql.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
            {
                // Add SET to current batch
                currentBatch.Add(sql.TrimEnd(';'));
            }
            else
            {
                // Add command to batch
                currentBatch.Add(sql.TrimEnd(';'));

                // Execute the batch (SET + command, or just command if no SET)
                string batchSql = string.Join(";\n", currentBatch);
                await context.Database.ExecuteSqlRawAsync(batchSql);

                // Clear batch for next iteration
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

    /// <summary>
    /// Complete migration workflow: generate operations and apply them.
    /// </summary>
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
        // Generate operations from null (empty database) to current model
        await ExecuteMigrationAsync(null, context);
    }

    /// <summary>
    /// Simulates altering the database by comparing two different contexts.
    /// </summary>
    protected static async Task AlterDatabaseViaMigrationAsync(
        DbContext oldContext,
        DbContext newContext)
    {
        await ExecuteMigrationAsync(oldContext, newContext);
    }
}

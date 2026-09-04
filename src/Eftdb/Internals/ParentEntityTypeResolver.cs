using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Resolves a continuous aggregate's parent entity type from its <c>ParentName</c> annotation value.
    /// The value may hold the CLR class name (code-first), the EF Core short name, the database table
    /// name (scaffolding), or the view name (hierarchical aggregates whose parent is itself a
    /// continuous aggregate).
    /// </summary>
    internal static class ParentEntityTypeResolver
    {
        public static IEntityType? Resolve(IModel model, string? parentName)
            => string.IsNullOrWhiteSpace(parentName)
                ? null
                : model.GetEntityTypes().FirstOrDefault(e =>
                    e.ClrType?.Name == parentName
                    || e.ShortName() == parentName
                    || e.GetTableName() == parentName
                    || e.GetViewName() == parentName);
    }
}

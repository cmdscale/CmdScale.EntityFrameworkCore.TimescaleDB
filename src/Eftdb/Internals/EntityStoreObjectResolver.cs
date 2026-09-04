using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Resolves the store object (table or view) an entity maps to, and a human-readable display
    /// name for diagnostics.
    /// </summary>
    internal static class EntityStoreObjectResolver
    {
        /// <summary>
        /// Returns the table store object when the entity maps to a table, otherwise the view store
        /// object, or <c>null</c> when the entity maps to neither.
        /// </summary>
        public static StoreObjectIdentifier? GetStoreObjectIdentifier(IEntityType entityType)
        {
            string? tableName = entityType.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                return StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            }

            string? viewName = entityType.GetViewName();
            if (!string.IsNullOrWhiteSpace(viewName))
            {
                return StoreObjectIdentifier.View(viewName, entityType.GetViewSchema() ?? entityType.GetSchema());
            }

            return null;
        }

        /// <summary>
        /// Returns the CLR type name for diagnostics, falling back to the EF entity-type name for
        /// shared-type or keyless entities without a distinct CLR type.
        /// </summary>
        public static string DisplayName(IEntityType entityType) => entityType.ClrType?.Name ?? entityType.Name;
    }
}

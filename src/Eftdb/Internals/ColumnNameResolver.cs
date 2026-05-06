using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Resolves a name to a database column name on a given entity, accepting either
    /// the CLR property name (canonical for code-first usage including EFCore.NamingConventions)
    /// or the database column name itself (form emitted by the design-time scaffolder).
    /// </summary>
    internal static class ColumnNameResolver
    {
        /// <summary>
        /// Returns the database column name for <paramref name="nameOrColumn"/> on
        /// <paramref name="entityType"/>, or <c>null</c> if no matching property exists.
        /// </summary>
        /// <remarks>
        /// Resolution is two-step: first by CLR property name (so naming-convention plugins
        /// translate to the actual store column), then by reverse lookup against each
        /// property's resolved column name (so a value already in column-name form is
        /// recognised). Both steps consult <c>GetColumnName(StoreObjectIdentifier)</c>,
        /// which honours all registered conventions.
        /// </remarks>
        public static string? Resolve(IEntityType entityType, string? nameOrColumn, StoreObjectIdentifier storeIdentifier)
        {
            if (string.IsNullOrWhiteSpace(nameOrColumn))
            {
                return null;
            }

            string? viaClrName = entityType.FindProperty(nameOrColumn)?.GetColumnName(storeIdentifier);
            if (!string.IsNullOrWhiteSpace(viaClrName))
            {
                return viaClrName;
            }

            foreach (IProperty property in entityType.GetProperties())
            {
                string? columnName = property.GetColumnName(storeIdentifier);
                if (string.Equals(columnName, nameOrColumn, StringComparison.Ordinal))
                {
                    return columnName;
                }
            }

            return null;
        }
    }
}

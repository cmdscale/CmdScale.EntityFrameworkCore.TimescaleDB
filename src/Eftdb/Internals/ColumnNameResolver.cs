using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{
    /// <summary>
    /// Resolves a name to a database column on a given entity, accepting the CLR property name
    /// (canonical for code-first usage including EFCore.NamingConventions), a dot-separated path
    /// through complex-type properties (e.g. <c>"Param1.Value"</c>), or the database column name
    /// itself (form emitted by the design-time scaffolder).
    /// </summary>
    internal static class ColumnNameResolver
    {
        /// <summary>
        /// Returns the database column name for <paramref name="nameOrColumn"/> on
        /// <paramref name="entityType"/>, or <c>null</c> if no matching property exists.
        /// </summary>
        /// <remarks>
        /// Resolution is two-step: first by CLR property name or complex-type path (so
        /// naming-convention plugins translate to the actual store column), then by reverse
        /// lookup against each property's resolved column name including complex-type
        /// properties (so a value already in column-name form is recognised). Both steps
        /// consult <c>GetColumnName(StoreObjectIdentifier)</c>, which honours all registered
        /// conventions. Properties without a column in the store object (e.g. complex types
        /// mapped to JSON) resolve to <c>null</c>.
        /// </remarks>
        public static string? Resolve(IEntityType entityType, string? nameOrColumn, StoreObjectIdentifier storeIdentifier)
        {
            if (string.IsNullOrWhiteSpace(nameOrColumn))
            {
                return null;
            }

            string? viaClrPath = FindPropertyByPath(entityType, nameOrColumn, ignoreCase: false)?.GetColumnName(storeIdentifier);
            if (!string.IsNullOrWhiteSpace(viaClrPath))
            {
                return viaClrPath;
            }

            return FindPropertyByColumnName(entityType, nameOrColumn, storeIdentifier, StringComparison.Ordinal)?.GetColumnName(storeIdentifier);
        }

        /// <summary>
        /// Returns the <see cref="IProperty"/> for <paramref name="nameOrColumn"/> on
        /// <paramref name="entityType"/>, or <c>null</c> if no matching property exists.
        /// Reverse column-name lookup requires <paramref name="storeIdentifier"/> and is
        /// skipped when it is <c>null</c>.
        /// </summary>
        public static IProperty? ResolveProperty(IEntityType entityType, string? nameOrColumn, StoreObjectIdentifier? storeIdentifier, bool ignoreCase = false)
        {
            if (string.IsNullOrWhiteSpace(nameOrColumn))
            {
                return null;
            }

            IProperty? viaPath = FindPropertyByPath(entityType, nameOrColumn, ignoreCase);
            if (viaPath != null)
            {
                return viaPath;
            }

            if (storeIdentifier == null)
            {
                return null;
            }

            StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return FindPropertyByColumnName(entityType, nameOrColumn, storeIdentifier.Value, comparison);
        }

        private static IProperty? FindPropertyByPath(ITypeBase typeBase, string path, bool ignoreCase)
        {
            string[] segments = path.Split('.');
            ITypeBase current = typeBase;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                IComplexProperty? complexProperty = FindComplexProperty(current, segments[i], ignoreCase);
                if (complexProperty == null || complexProperty.IsCollection)
                {
                    return null;
                }

                current = complexProperty.ComplexType;
            }

            return FindScalarProperty(current, segments[^1], ignoreCase);
        }

        private static IProperty? FindScalarProperty(ITypeBase typeBase, string name, bool ignoreCase)
        {
            IProperty? exact = typeBase.FindProperty(name);
            if (exact != null || !ignoreCase)
            {
                return exact;
            }

            return typeBase.GetProperties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static IComplexProperty? FindComplexProperty(ITypeBase typeBase, string name, bool ignoreCase)
        {
            IComplexProperty? exact = typeBase.FindComplexProperty(name);
            if (exact != null || !ignoreCase)
            {
                return exact;
            }

            return typeBase.GetComplexProperties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static IProperty? FindPropertyByColumnName(ITypeBase typeBase, string columnName, StoreObjectIdentifier storeIdentifier, StringComparison comparison)
        {
            foreach (IProperty property in typeBase.GetProperties())
            {
                if (string.Equals(property.GetColumnName(storeIdentifier), columnName, comparison))
                {
                    return property;
                }
            }

            foreach (IComplexProperty complexProperty in typeBase.GetComplexProperties())
            {
                if (complexProperty.IsCollection)
                {
                    continue;
                }

                IProperty? nested = FindPropertyByColumnName(complexProperty.ComplexType, columnName, storeIdentifier, comparison);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}

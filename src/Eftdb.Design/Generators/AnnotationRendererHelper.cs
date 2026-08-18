using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Hhelpers shared by <see cref="IFeatureAnnotationRenderer"/> implementations:
    /// annotation lookup and consumption, column list parsing, and database-column to CLR-property
    /// resolution.
    /// </summary>
    internal static class AnnotationRendererHelper
    {
        public static PropertyAccessorCodeFragment PropertyAccessor(string property) => new("x", [property]);

        /// <summary>
        /// Maps a scaffolded database column name to its CLR property name on the entity, so generated code
        /// references the property (e.g. <c>DeviceId</c>) rather than the raw column (e.g. <c>device_id</c>).
        /// </summary>
        public static string ResolvePropertyName(IEntityType entityType, string columnName)
        {
            TryResolvePropertyName(entityType, columnName, out string propertyName);
            return propertyName;
        }

        /// <summary>
        /// Maps a scaffolded database column name to its CLR property name. Returns <c>false</c> with the
        /// raw value when no column mapping or property resolves, so callers can avoid emitting
        /// <c>nameof(...)</c> references to members that do not exist.
        /// </summary>
        public static bool TryResolvePropertyName(IEntityType entityType, string columnName, out string propertyName)
        {
            StoreObjectIdentifier? store =
                StoreObjectIdentifier.Create(entityType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(entityType, StoreObjectType.View);

            if (store is StoreObjectIdentifier id)
            {
                foreach (IProperty property in entityType.GetProperties())
                {
                    if (string.Equals(property.GetColumnName(id), columnName, StringComparison.Ordinal))
                    {
                        propertyName = property.Name;
                        return true;
                    }
                }
            }

            if (entityType.FindProperty(columnName) is IProperty direct)
            {
                propertyName = direct.Name;
                return true;
            }

            propertyName = columnName;
            return false;
        }

        public static string[] ResolveColumns(IEntityType entityType, string? value)
            => [.. SplitColumns(value).Select(column => ResolvePropertyName(entityType, column))];

        /// <summary>
        /// References a column as <c>nameof(Property)</c> when it resolves to a CLR property on the entity;
        /// falls back to the raw string for unmapped columns, where a <c>nameof</c> would not compile.
        /// </summary>
        public static object ColumnReference(IEntityType entityType, string column, string suffix = "")
            => TryResolvePropertyName(entityType, column, out string property)
                ? new NameOfCodeFragment(property, suffix)
                : suffix.Length == 0 ? column : column + suffix;

        /// <summary>
        /// Splits a <c>"column [ASC|DESC] [NULLS ...]"</c> entry into a property reference plus literal suffix.
        /// </summary>
        public static object OrderByReference(IEntityType entityType, string entry)
        {
            int space = entry.IndexOf(' ');
            return space < 0
                ? ColumnReference(entityType, entry)
                : ColumnReference(entityType, entry[..space], entry[space..]);
        }

        /// <summary>
        /// Keeps mixed reference arrays as-is so <c>nameof</c> fragments render, and narrows
        /// all-string arrays to <c>string[]</c> so the base helper emits a plain array literal.
        /// </summary>
        public static object ToArgumentArray(object[] entries)
            => Array.Exists(entries, entry => entry is NameOfCodeFragment)
                ? entries
                : Array.ConvertAll(entries, entry => (string)entry);

        public static IAnnotation? Find(IDictionary<string, IAnnotation> annotations, string key)
            => annotations.TryGetValue(key, out IAnnotation? annotation) ? annotation : null;

        public static string? GetString(IDictionary<string, IAnnotation> annotations, string key)
            => Find(annotations, key)?.Value as string;

        public static string[] SplitColumns(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? []
                : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        public static void Consume(IDictionary<string, IAnnotation> annotations, params string[] keys)
        {
            foreach (string key in keys)
            {
                annotations.Remove(key);
            }
        }
    }
}

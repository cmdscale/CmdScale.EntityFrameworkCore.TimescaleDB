using NpgsqlTypes;
using System.Linq.Expressions;
using System.Reflection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    /// <summary>
    /// Provides a fluent API for configuring a high-performance bulk copy operation
    /// into a PostgreSQL or TimescaleDB (hyper)table.
    /// </summary>
    /// <typeparam name="T">The entity or POCO type that is being ingested.</typeparam>
    /// <remarks>
    /// This class automatically discovers and maps the public properties of type <c>T</c> upon instantiation.
    /// You can then use the fluent methods to override defaults, such as the table name, or use the
    /// <c>MapColumn</c> method to define a specific column order or handle properties that
    /// could not be mapped automatically.
    /// </remarks>
    public class TimescaleCopyConfig<T>
    {
        /// <summary>
        /// The name of the database table to which data will be copied.
        /// Defaults to the name of the generic type <c>T</c>.
        /// </summary>
        public string TableName { get; private set; } = typeof(T).Name;

        /// <summary>
        /// The number of parallel workers to use for ingesting data.
        /// Defaults to 4.
        /// </summary>
        public int NumberOfWorkers { get; private set; } = 4;

        /// <summary>
        /// The maximum number of rows each worker will send to the database in a single batch.
        /// Defaults to 10,000.
        /// </summary>
        public int MaxBatchSize { get; private set; } = 10_000;

        /// <summary>
        /// Stores the mappings between database column names and the corresponding C# model properties.
        /// </summary>
        /// <remarks>
        /// The order of the properties in this dictionary is critical. It must precisely match the
        /// column order in the SQL COPY command (and in the database) to ensure a successful binary copy operation.
        /// </remarks>
        public Dictionary<string, (Func<T, object?> Getter, NpgsqlDbType DbType)> ColumnMappings { get; } = [];

        public TimescaleCopyConfig()
        {
            // Get all public instance properties of the generic type T
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                // Skip properties that can't be read or are complex types without a clear mapping
                if (!property.CanRead) continue;

                // Attempt to map the C# type to an NpgsqlDbType
                if (MapClrTypeToNpgsqlDbType(property.PropertyType, out NpgsqlDbType dbType))
                {
                    // Auto-discover properties and create compiled getters for them.
                    var parameter = Expression.Parameter(typeof(T), "x");
                    var member = Expression.Property(parameter, property);
                    var conversion = Expression.Convert(member, typeof(object));
                    var lambda = Expression.Lambda<Func<T, object>>(conversion, parameter);

                    ColumnMappings[property.Name] = (lambda.Compile(), dbType);
                }
            }
        }

        /// <summary>
        /// Sets the name of the database table for the bulk copy operation.
        /// </summary>
        /// <param name="tableName">The name of the target database table.</param>
        /// <returns>The same configuration instance for fluent chaining.</returns>
        public TimescaleCopyConfig<T> ToTable(string tableName)
        {
            TableName = tableName;
            return this;
        }

        /// <summary>
        /// Sets the number of parallel workers (threads) to use for the ingestion.
        /// </summary>
        /// <param name="numberOfWorkers">The number of concurrent workers. Must be at least 1.</param>
        /// <returns>The same configuration instance for fluent chaining.</returns>
        public TimescaleCopyConfig<T> WithWorkers(int numberOfWorkers)
        {
            NumberOfWorkers = Math.Max(1, numberOfWorkers);
            return this;
        }

        /// <summary>
        /// Sets the maximum number of rows to include in a single batch per worker.
        /// </summary>
        /// <param name="maxBatchSize">The size of each batch. Must be at least 1.</param>
        /// <returns>The same configuration instance for fluent chaining.</returns>
        public TimescaleCopyConfig<T> WithBatchSize(int maxBatchSize)
        {
            MaxBatchSize = Math.Max(1, maxBatchSize);
            return this;
        }

        /// <summary>
        /// Manually maps a database column to a C# model property.
        /// </summary>
        /// <remarks>
        /// Use this method to override the default mappings discovered by the constructor or to define a specific column order for the COPY command.
        /// </remarks>
        /// <param name="columnName">The name of the database column.</param>
        /// <param name="propertySelector">A lambda expression selecting the C# property (e.g., `x => x.PropertyName`).</param>
        /// <param name="dbType">The <see cref="NpgsqlDbType"/> of the column.</param>
        /// <returns>The same configuration instance for fluent chaining.</returns>
        public TimescaleCopyConfig<T> MapColumn(string columnName, Expression<Func<T, object>> propertySelector, NpgsqlDbType dbType)
        {
            Func<T, object> getter = propertySelector.Compile();
            ColumnMappings[columnName] = (getter, dbType);
            return this;
        }

        /// <summary>
        /// Maps a C# Type to its corresponding NpgsqlDbType.
        /// </summary>
        private static bool MapClrTypeToNpgsqlDbType(Type clrType, out NpgsqlDbType dbType)
        {
            // Handle nullable value types by getting the underlying type
            var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            // This map contains the default CLR type to NpgsqlDbType mappings
            // based on the Npgsql "Write Mappings" documentation.
            Dictionary<Type, NpgsqlDbType> typeMap = new()
            {
                // Numeric Types
                { typeof(short), NpgsqlDbType.Smallint },
                { typeof(int), NpgsqlDbType.Integer },
                { typeof(long), NpgsqlDbType.Bigint },
                { typeof(float), NpgsqlDbType.Real },
                { typeof(double), NpgsqlDbType.Double },
                { typeof(decimal), NpgsqlDbType.Numeric },
                { typeof(byte), NpgsqlDbType.Smallint },
                { typeof(sbyte), NpgsqlDbType.Smallint },

                // Text Types
                { typeof(string), NpgsqlDbType.Text },
                { typeof(char), NpgsqlDbType.Text },
                { typeof(char[]), NpgsqlDbType.Text },

                // Date/Time Types
                { typeof(DateTime), NpgsqlDbType.TimestampTz },
                { typeof(DateTimeOffset), NpgsqlDbType.TimestampTz },
                { typeof(DateOnly), NpgsqlDbType.Date },
                { typeof(TimeOnly), NpgsqlDbType.Time },
                { typeof(TimeSpan), NpgsqlDbType.Interval },

                // Other Types
                { typeof(bool), NpgsqlDbType.Boolean },
                { typeof(Guid), NpgsqlDbType.Uuid },
                { typeof(byte[]), NpgsqlDbType.Bytea },

                // Network Address Types
                { typeof(System.Net.IPAddress), NpgsqlDbType.Inet },
                { typeof(System.Net.NetworkInformation.PhysicalAddress), NpgsqlDbType.MacAddr },

                // Bit String Types
                { typeof(System.Collections.BitArray), NpgsqlDbType.Varbit }
            };

            if (typeMap.TryGetValue(underlyingType, out dbType))
            {
                return true;
            }

            dbType = NpgsqlDbType.Unknown;
            return false;
        }
    }
}

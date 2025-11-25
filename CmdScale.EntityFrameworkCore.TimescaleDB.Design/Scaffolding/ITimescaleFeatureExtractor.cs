using System.Data.Common;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Interface for extracting TimescaleDB feature metadata from a database connection.
    /// </summary>
    public interface ITimescaleFeatureExtractor
    {
        /// <summary>
        /// Extracts feature metadata from the database and returns a dictionary keyed by (schema, tableName).
        /// </summary>
        Dictionary<(string Schema, string TableName), object> Extract(DbConnection connection);
    }
}

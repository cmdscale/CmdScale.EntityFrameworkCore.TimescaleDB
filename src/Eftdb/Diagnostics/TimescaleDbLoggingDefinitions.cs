using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql.EntityFrameworkCore.PostgreSQL.Diagnostics.Internal;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics
{
#pragma warning disable EF1001 // NpgsqlLoggingDefinitions and EventDefinitionBase are internal EF/Npgsql APIs; subclassing the provider's registered LoggingDefinitions is the intended way to add provider event-definition caches (mirrors how NpgsqlLoggingDefinitions extends RelationalLoggingDefinitions).
    /// <summary>
    /// The central home for all TimescaleDB warning definitions. Extends the <see cref="LoggingDefinitions"/>
    /// instance Npgsql registers so that Npgsql's own event-definition caches remain intact while TimescaleDB
    /// adds its own.
    /// </summary>
    public class TimescaleDbLoggingDefinitions : NpgsqlLoggingDefinitions
    {
        /// <summary>
        /// Cached definition for <see cref="TimescaleDbEventId.CommunityFeatureSkipped"/>.
        /// </summary>
        public EventDefinitionBase? LogCommunityFeatureSkipped;

        /// <summary>
        /// Cached definition for <see cref="TimescaleDbEventId.TimeBucketColumnUnmapped"/>.
        /// </summary>
        public EventDefinitionBase? LogTimeBucketColumnUnmapped;
    }
#pragma warning restore EF1001
}

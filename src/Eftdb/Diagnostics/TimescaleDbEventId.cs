using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics
{
    /// <summary>
    /// Event IDs for TimescaleDB provider events that are logged to an <see cref="ILogger"/> and dispatched
    /// through Entity Framework Core's diagnostics pipeline (ILogger sinks, <c>LogTo</c>, and
    /// <see cref="System.Diagnostics.DiagnosticSource"/>).
    /// </summary>
    /// <remarks>
    /// These IDs are also used with <see cref="Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder"/>
    /// (via <c>ConfigureWarnings</c>) to silence, elevate, or throw on a given warning, e.g.
    /// <c>options.ConfigureWarnings(w =&gt; w.Ignore(TimescaleDbEventId.CommunityFeatureSkipped))</c>.
    /// <para>
    /// The numeric base is <c>63000</c>. EF Core reserves the 10000s (<c>CoreEventId</c>), EF Relational the
    /// 20000s (<c>RelationalEventId</c>), and the Npgsql provider the 35000s (<c>NpgsqlEfEventId</c>).
    /// </para>
    /// </remarks>
    public static class TimescaleDbEventId
    {
        // Base chosen to clear EF Core (10000s), EF Relational (20000s), and Npgsql (35000s) ranges.
        private const int Base = 63000;

        private enum Id
        {
            CommunityFeatureSkipped = Base,
            TimeBucketColumnUnmapped,
        }

        /// <summary>
        /// A Community/TSL-only TimescaleDB feature was skipped because the provider is running in Apache
        /// edition mode (<c>UseApacheEdition()</c>). Raised at migration SQL generation time, once per
        /// skipped feature, in the <see cref="DbLoggerCategory.Migrations"/> category.
        /// </summary>
        public static readonly EventId CommunityFeatureSkipped = MakeMigrationsId(Id.CommunityFeatureSkipped);

        /// <summary>
        /// A continuous aggregate exposes its time-bucket column but no property maps to that column, so the
        /// bucket cannot be queried through the entity. Raised at model validation time in the
        /// <see cref="DbLoggerCategory.Model.Validation"/> category.
        /// </summary>
        public static readonly EventId TimeBucketColumnUnmapped = MakeValidationId(Id.TimeBucketColumnUnmapped);

        private static readonly string MigrationsPrefix = DbLoggerCategory.Migrations.Name + ".";
        private static readonly string ValidationPrefix = DbLoggerCategory.Model.Validation.Name + ".";

        private static EventId MakeMigrationsId(Id id) => new((int)id, MigrationsPrefix + id);

        private static EventId MakeValidationId(Id id) => new((int)id, ValidationPrefix + id);
    }
}

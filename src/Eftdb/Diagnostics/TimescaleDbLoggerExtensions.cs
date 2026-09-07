using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics
{
#pragma warning disable EF1001 // EventDefinition, EventData, NonCapturingLazyInitializer and the IDiagnosticsLogger dispatch helpers are internal EF APIs; they are the intended provider logging surface (mirrors CoreLoggerExtensions/NpgsqlLoggerExtensions).
    /// <summary>
    /// The single place that emits TimescaleDB provider warnings. Each warning is dispatched the way EF Core
    /// dispatches its own events: through <see cref="EventDefinition"/> to the <see cref="ILogger"/> sinks
    /// (<c>UseLoggerFactory</c>) and, via <see cref="IDiagnosticsLogger.DispatchEventData"/>, to the
    /// <see cref="IDbContextLogger"/> (<c>LogTo</c>) and <see cref="System.Diagnostics.DiagnosticSource"/>.
    /// </summary>
    internal static class TimescaleDbLoggerExtensions
    {
        /// <summary>
        /// Warns that a Community/TSL-only feature was skipped in Apache edition mode.
        /// </summary>
        /// <param name="diagnostics">The migrations diagnostics logger.</param>
        /// <param name="feature">The human-readable description of the skipped feature.</param>
        public static void CommunityFeatureSkipped(this IDiagnosticsLogger<DbLoggerCategory.Migrations> diagnostics, string feature)
        {
            EventDefinition<string> definition = LogCommunityFeatureSkipped(diagnostics);

            if (diagnostics.ShouldLog(definition))
            {
                definition.Log(diagnostics, feature);
            }

            if (diagnostics.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
            {
                EventData eventData = new(
                    definition,
                    (d, _) => ((EventDefinition<string>)d).GenerateMessage(feature));
                diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }
        }

        /// <summary>
        /// Warns that a continuous aggregate's time-bucket column has no mapped property, so the bucket cannot
        /// be queried through the entity.
        /// </summary>
        /// <param name="diagnostics">The model-validation diagnostics logger.</param>
        /// <param name="aggregate">The continuous aggregate entity display name.</param>
        /// <param name="materializedView">The materialized view name.</param>
        /// <param name="bucketColumn">The unmapped bucket column name.</param>
        public static void TimeBucketColumnUnmapped(
            this IDiagnosticsLogger<DbLoggerCategory.Model.Validation> diagnostics,
            string aggregate,
            string materializedView,
            string bucketColumn)
        {
            EventDefinition<string, string, string> definition = LogTimeBucketColumnUnmapped(diagnostics);

            if (diagnostics.ShouldLog(definition))
            {
                definition.Log(diagnostics, aggregate, materializedView, bucketColumn);
            }

            if (diagnostics.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
            {
                EventData eventData = new(
                    definition,
                    (d, _) => ((EventDefinition<string, string, string>)d).GenerateMessage(aggregate, materializedView, bucketColumn));
                diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }
        }

        private static EventDefinition<string> LogCommunityFeatureSkipped(IDiagnosticsLogger diagnostics)
        {
            TimescaleDbLoggingDefinitions definitions = GetDefinitions(diagnostics);
            EventDefinitionBase? definition = definitions.LogCommunityFeatureSkipped;
            if (definition == null)
            {
                EventDefinition<string> created = new(
                    diagnostics.Options,
                    TimescaleDbEventId.CommunityFeatureSkipped,
                    LogLevel.Warning,
                    "TimescaleDbEventId.CommunityFeatureSkipped",
                    static level => LoggerMessage.Define<string>(
                        level,
                        TimescaleDbEventId.CommunityFeatureSkipped,
                        "{SkippedCommunityFeature}"));

                definition = Interlocked.CompareExchange(ref definitions.LogCommunityFeatureSkipped, created, null) ?? created;
            }

            return (EventDefinition<string>)definition;
        }

        private static EventDefinition<string, string, string> LogTimeBucketColumnUnmapped(IDiagnosticsLogger diagnostics)
        {
            TimescaleDbLoggingDefinitions definitions = GetDefinitions(diagnostics);
            EventDefinitionBase? definition = definitions.LogTimeBucketColumnUnmapped;
            if (definition == null)
            {
                EventDefinition<string, string, string> created = new(
                    diagnostics.Options,
                    TimescaleDbEventId.TimeBucketColumnUnmapped,
                    LogLevel.Warning,
                    "TimescaleDbEventId.TimeBucketColumnUnmapped",
                    static level => LoggerMessage.Define<string, string, string>(
                        level,
                        TimescaleDbEventId.TimeBucketColumnUnmapped,
                        "The continuous aggregate '{Aggregate}' (materialized view '{MaterializedView}') exposes its bucket column as " +
                        "'{BucketColumn}', but no property maps to that column, so the bucket cannot be queried through the entity. " +
                        "Designate the bucket property with WithTimeBucketProperty(...), annotate a property with [TimeBucket], or map a " +
                        "property to that column with HasColumnName."));

                definition = Interlocked.CompareExchange(ref definitions.LogTimeBucketColumnUnmapped, created, null) ?? created;
            }

            return (EventDefinition<string, string, string>)definition;
        }

        // Fail loud rather than fall back: EF and Npgsql hard-cast the provider's LoggingDefinitions with no
        // graceful path, so a non-TimescaleDB instance here means the Replace registration was lost — a wiring
        // bug that must surface, not a runtime condition to tolerate.
        private static TimescaleDbLoggingDefinitions GetDefinitions(IDiagnosticsLogger diagnostics)
            => diagnostics.Definitions as TimescaleDbLoggingDefinitions
               ?? throw new InvalidOperationException(
                   $"Expected the registered {nameof(LoggingDefinitions)} to be {nameof(TimescaleDbLoggingDefinitions)}, but found " +
                   $"'{diagnostics.Definitions.GetType().Name}'. Ensure UseTimescaleDb() is called after UseNpgsql() so its service " +
                   $"registration is applied.");
    }
#pragma warning restore EF1001
}

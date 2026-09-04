namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// Raw smart-meter measurement emitted by a power meter on the electrical grid.
    /// This is the source hypertable at the base of the hierarchical continuous
    /// aggregate rollup chain <c>reading &rarr; hourly &rarr; daily</c>.
    /// </summary>
    public class PowerMeterReading
    {
        /// <summary>
        /// The precise UTC timestamp at which the meter emitted the sample.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Identifier of the physical meter that produced the reading.
        /// Used as the grouping dimension across every level of the rollup chain.
        /// </summary>
        public string MeterId { get; set; } = string.Empty;

        /// <summary>
        /// Instantaneous active power draw in kilowatts at <see cref="Timestamp"/>.
        /// </summary>
        public double PowerKw { get; set; }
    }
}

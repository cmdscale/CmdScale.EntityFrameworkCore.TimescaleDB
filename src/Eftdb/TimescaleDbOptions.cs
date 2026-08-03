namespace CmdScale.EntityFrameworkCore.TimescaleDB
{
    /// <summary>
    /// Provider-level options for TimescaleDB migrations.
    /// </summary>
    public sealed class TimescaleDbOptions
    {
        /// <summary>
        /// When <see langword="true"/>, migration SQL uses the pre-2.18 compression API
        /// (<c>timescaledb.compress</c>, <c>timescaledb.compress_segmentby</c>,
        /// <c>timescaledb.compress_orderby</c>, <c>add_compression_policy</c>,
        /// <c>remove_compression_policy</c>).
        /// When <see langword="false"/> (default), migration SQL uses the 2.18+ columnstore API
        /// (<c>timescaledb.enable_columnstore</c>, <c>timescaledb.segmentby</c>,
        /// <c>timescaledb.orderby</c>, <c>CALL add_columnstore_policy</c>,
        /// <c>CALL remove_columnstore_policy</c>).
        /// </summary>
        public bool UseLegacyCompressionNames { get; private set; }

        /// <summary>
        /// Configures the provider to emit pre-2.18 compression SQL names.
        /// Use this when targeting a TimescaleDB version earlier than 2.18.
        /// </summary>
        public TimescaleDbOptions UseLegacyCompressionSql()
        {
            UseLegacyCompressionNames = true;
            return this;
        }
    }
}

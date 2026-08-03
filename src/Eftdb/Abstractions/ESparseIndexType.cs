namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    /// <summary>
    /// Identifies the kind of sparse index function.
    /// </summary>
    public enum ESparseIndexType
    {
        /// <summary>Bloom-filter sparse index</summary>
        Bloom,

        /// <summary>Min/max range sparse index</summary>
        MinMax,
    }
}

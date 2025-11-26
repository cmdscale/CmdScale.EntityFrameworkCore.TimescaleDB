namespace CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions
{
    public enum EDimensionType
    {
        /// <summary>
        /// For a second range partition.
        /// </summary>
        Range,

        /// <summary>
        /// To enable parallelization across multiple disks.
        /// </summary>
        Hash
    }
}

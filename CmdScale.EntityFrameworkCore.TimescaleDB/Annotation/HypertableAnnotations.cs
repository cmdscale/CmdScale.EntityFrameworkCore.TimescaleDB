namespace CmdScale.EntityFrameworkCore.TimescaleDB.Annotation
{
    /// <summary>
    /// Contains constants for annotations used by the TimescaleDB provider extension.
    /// </summary>
    public static class HypertableAnnotations
    {
        public const string IsHypertable = "TimescaleDB:IsHypertable";
        public const string HypertableTimeColumn = "TimescaleDB:TimeColumnName";
    }
}

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    public static class ReorderPolicyAnnotations
    {
        public const string HasReorderPolicy = "TimescaleDB:HasReorderPolicy";
        public const string IndexName = "TimescaleDB:ReorderPolicy:IndexName";
        public const string InitialStart = "TimescaleDB:ReorderPolicy:InitialStart";
    }
}

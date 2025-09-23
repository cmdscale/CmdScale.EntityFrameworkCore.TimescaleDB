namespace CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ReorderPolicyAttribute : Attribute
    {
        public string IndexName { get; set; } = string.Empty;

        public ReorderPolicyAttribute(string indexName)
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("IndexName must be provided.", nameof(indexName));
            }

            IndexName = indexName;
        }
    }
}

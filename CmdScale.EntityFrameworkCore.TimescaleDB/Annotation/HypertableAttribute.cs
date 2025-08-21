namespace CmdScale.EntityFrameworkCore.TimescaleDB.Annotation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HypertableAttribute : Attribute
    {
        /// <summary>
        /// The name of the column that contains the time-series data.
        /// This is typically a DateTime, DateTimeOffset, or similar type.
        /// </summary>
        public string TimeColumnName { get; } = string.Empty;

        public HypertableAttribute(string timeColumnName)
        {
            if (string.IsNullOrWhiteSpace(timeColumnName))
            {
                throw new ArgumentException("Time column name must be provided.", nameof(timeColumnName));
            }

            TimeColumnName = timeColumnName;
        }
    }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Samples.Shared.Models
{
    /// <summary>
    /// Represents a single measurement channel owned by a sensor reading.
    /// Declared with <see cref="ComplexTypeAttribute"/> so EF Core maps its scalar
    /// properties as columns directly on the owning table rather than a separate table.
    /// Default column names follow EF Core's complex-type convention:
    /// <c>{PropertyName}_{MemberName}</c> (e.g. <c>Primary_Name</c>, <c>Primary_Value</c>).
    /// Under a snake_case naming convention the columns become
    /// <c>primary_name</c> / <c>primary_value</c> etc.
    /// </summary>
    [ComplexType]
    public class SensorChannel
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}

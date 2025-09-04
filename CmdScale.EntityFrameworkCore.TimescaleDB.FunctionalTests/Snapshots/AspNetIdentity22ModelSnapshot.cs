// EF Core 2.2 ASP.NET Identity Model Snapshot (similar to 2.1 but with minor differences)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Snapshots
{
    public class AspNetIdentity22ModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            // Reuse 2.1 structure but with 2.2 product version
            AspNetIdentity21ModelSnapshot builder21 = new();
            builder21.GetType().GetMethod("BuildModel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(builder21, new object[] { modelBuilder });

            // Override the product version for 2.2
            modelBuilder.HasAnnotation("ProductVersion", "2.2.0-preview1");
        }
    }
}
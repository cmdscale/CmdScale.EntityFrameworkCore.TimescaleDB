using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;

#pragma warning disable EF1001 // ModelCodeGeneratorSelector is the documented extension point for generator selection.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Prefers <see cref="TimescaleCSharpModelGenerator"/> over the built-in C# generator.
    /// </summary>
    public class TimescaleModelCodeGeneratorSelector(IEnumerable<IModelCodeGenerator> services)
        : ModelCodeGeneratorSelector(services)
    {
        public override IModelCodeGenerator Select(ModelCodeGenerationOptions options)
        {
            IModelCodeGenerator selected = base.Select(options);

            return selected.GetType() == typeof(CSharpModelGenerator)
                ? Services.OfType<TimescaleCSharpModelGenerator>().FirstOrDefault() ?? selected
                : selected;
        }
    }
}
#pragma warning restore EF1001

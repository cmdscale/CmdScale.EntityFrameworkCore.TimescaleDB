using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;

#pragma warning disable EF1001 // ModelCodeGeneratorSelector is the documented extension point for generator selection.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Prefers <see cref="TimescaleCSharpModelGenerator"/> over the built-in C# generator.
    /// </summary>
    /// <remarks>
    /// The design-time tooling registers provider services before the EF Core defaults, so the default
    /// last-registration-wins selection would always pick the built-in <see cref="CSharpModelGenerator"/>
    /// over the provider's. User T4 templates (<see cref="TemplatedModelGenerator"/>) keep priority.
    /// </remarks>
    public class TimescaleModelCodeGeneratorSelector(IEnumerable<IModelCodeGenerator> services)
        : ModelCodeGeneratorSelector(services)
    {
        public override IModelCodeGenerator Select(ModelCodeGenerationOptions options)
        {
            IModelCodeGenerator selected = base.Select(options);

            return selected.GetType() == typeof(CSharpModelGenerator)
                ? Services.OfType<TimescaleCSharpModelGenerator>().LastOrDefault() ?? selected
                : selected;
        }
    }
}
#pragma warning restore EF1001

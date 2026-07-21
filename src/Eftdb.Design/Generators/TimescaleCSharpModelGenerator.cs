using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;

#pragma warning disable EF1001 // CSharpModelGenerator is the documented extension point for scaffolded output post-processing.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Extends the built-in C# scaffolding generator to add the using directives required by the
    /// library's DataAnnotation attributes.
    /// </summary>
    /// <remarks>
    /// The built-in entity-type template hardcodes its using list and never inspects the
    /// <see cref="AttributeCodeFragment"/>s an <see cref="IAnnotationCodeGenerator"/> returns, so custom
    /// attribute types (<c>[Hypertable]</c>, <c>[Dimension]</c>) and their argument types
    /// (<c>EDimensionType</c>) would not resolve in the generated entity files. Selected over the
    /// built-in generator because the design-time service registration order makes the last matching
    /// <see cref="IModelCodeGenerator"/> win.
    /// </remarks>
    public class TimescaleCSharpModelGenerator(
        ModelCodeGeneratorDependencies dependencies,
        IOperationReporter reporter,
        IServiceProvider serviceProvider,
        IAnnotationCodeGenerator annotationCodeGenerator)
        : CSharpModelGenerator(dependencies, reporter, serviceProvider)
    {
        private readonly IAnnotationCodeGenerator _annotationCodeGenerator = annotationCodeGenerator;

        public override ScaffoldedModel GenerateModel(IModel model, ModelCodeGenerationOptions options)
        {
            TimescaleDbAnnotationCodeGenerator? tsGen = _annotationCodeGenerator as TimescaleDbAnnotationCodeGenerator;

            try
            {
                if (tsGen is not null)
                {
                    tsGen.ScaffoldMode = true;
                    tsGen.ScaffoldDataAnnotationsMode = options.UseDataAnnotations;
                }

                ScaffoldedModel scaffoldedModel = base.GenerateModel(model, options);

                scaffoldedModel.ContextFile.Code = RemoveDesignUsings(scaffoldedModel.ContextFile.Code);
                foreach (ScaffoldedFile additionalFile in scaffoldedModel.AdditionalFiles)
                {
                    additionalFile.Code = RemoveDesignUsings(additionalFile.Code);
                }

                scaffoldedModel.ContextFile.Code = AddMissingUsings(
                    scaffoldedModel.ContextFile.Code,
                    [typeof(TimescaleDbContextOptionsBuilderExtensions).Namespace!]);

                if (!options.UseDataAnnotations)
                {
                    return scaffoldedModel;
                }

                Dictionary<string, ScaffoldedFile> entityFiles = [];
                foreach (ScaffoldedFile file in scaffoldedModel.AdditionalFiles)
                {
                    entityFiles.TryAdd(Path.GetFileNameWithoutExtension(file.Path), file);
                }

                foreach (IEntityType entityType in model.GetEntityTypes())
                {
                    List<string> namespaces = CollectAttributeNamespaces(entityType);
                    if (namespaces.Count > 0 && entityFiles.TryGetValue(entityType.Name, out ScaffoldedFile? file))
                    {
                        file.Code = AddMissingUsings(file.Code, namespaces);
                    }
                }

                return scaffoldedModel;
            }
            finally
            {
                tsGen?.ResetScaffoldState();
            }
        }

        private static string RemoveDesignUsings(string code)
        {
            const string designUsingPrefix = "using CmdScale.EntityFrameworkCore.TimescaleDB.Design";

            string newLine = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            IEnumerable<string> kept = code.Split(newLine).Where(line =>
                !line.StartsWith(designUsingPrefix + ";", StringComparison.Ordinal)
                && !line.StartsWith(designUsingPrefix + ".", StringComparison.Ordinal));

            return string.Join(newLine, kept);
        }

        /// <summary>
        /// Re-runs attribute generation on a scratch copy of the entity's annotations to discover which
        /// namespaces the rendered attributes and their arguments require.
        /// </summary>
        private List<string> CollectAttributeNamespaces(IEntityType entityType)
        {
            Dictionary<string, IAnnotation> annotations = entityType.GetAnnotations().ToDictionary(a => a.Name, a => a);

            List<string> namespaces = [];
            foreach (AttributeCodeFragment fragment in _annotationCodeGenerator.GenerateDataAnnotationAttributes(entityType, annotations))
            {
                AddNamespace(namespaces, fragment.Type);

                foreach (object? argument in fragment.Arguments)
                {
                    AddArgumentNamespace(namespaces, argument);
                }

                foreach (object? argument in fragment.NamedArguments.Values)
                {
                    AddArgumentNamespace(namespaces, argument);
                }
            }

            foreach (IProperty property in entityType.GetProperties())
            {
                Dictionary<string, IAnnotation> propAnnotations = property.GetAnnotations().ToDictionary(a => a.Name, a => a);
                foreach (AttributeCodeFragment fragment in _annotationCodeGenerator.GenerateDataAnnotationAttributes(property, propAnnotations))
                {
                    AddNamespace(namespaces, fragment.Type);
                    foreach (object? argument in fragment.Arguments) AddArgumentNamespace(namespaces, argument);
                    foreach (object? argument in fragment.NamedArguments.Values) AddArgumentNamespace(namespaces, argument);
                }
            }

            return namespaces;
        }

        private static void AddArgumentNamespace(List<string> namespaces, object? argument)
        {
            if (argument is Enum)
            {
                AddNamespace(namespaces, argument.GetType());
            }
            else if (argument is Type type)
            {
                AddNamespace(namespaces, type);
            }
        }

        private static void AddNamespace(List<string> namespaces, Type type)
        {
            if (type.Namespace is string ns && !namespaces.Contains(ns))
            {
                namespaces.Add(ns);
            }
        }

        /// <summary>
        /// Merges the required namespaces into the file's leading using block, preserving the
        /// System-first ordering the built-in template produces.
        /// </summary>
        private static string AddMissingUsings(string code, IReadOnlyList<string> namespaces)
        {
            string newLine = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string[] lines = code.Split(newLine);

            int blockEnd = 0;
            List<string> usings = [];
            while (blockEnd < lines.Length && lines[blockEnd].StartsWith("using ", StringComparison.Ordinal) && lines[blockEnd].EndsWith(';'))
            {
                usings.Add(lines[blockEnd]["using ".Length..^1]);
                blockEnd++;
            }

            if (!namespaces.Except(usings).Any())
            {
                return code;
            }

            List<string> merged = [..
                usings.Union(namespaces)
                      .OrderBy(ns => ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
                      .ThenBy(ns => ns, StringComparer.Ordinal)];

            IEnumerable<string> usingLines = merged.Select(ns => $"using {ns};");
            IEnumerable<string> rest = lines.Skip(blockEnd);

            // A file without a leading using block gets one inserted, separated by a blank line.
            return blockEnd == 0
                ? string.Join(newLine, [.. usingLines, string.Empty, .. rest])
                : string.Join(newLine, [.. usingLines, .. rest]);
        }
    }
}
#pragma warning restore EF1001

using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

#pragma warning disable EF1001 // NpgsqlAnnotationCodeGenerator lives in *.Internal but is the documented base for provider annotation code generation.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators
{
    /// <summary>
    /// Converts the <c>TimescaleDB:*</c> annotations produced by scaffolding into the library's Fluent API
    /// calls (default) or DataAnnotation attributes (<c>--data-annotations</c>).
    /// </summary>
    /// <remarks>
    /// Subclasses Npgsql's generator so its own annotation handling (arrays, identity, comments) is preserved.
    /// Feature-specific rendering is delegated to one <see cref="IFeatureAnnotationRenderer"/> per feature,
    /// mirroring the per-feature extractor/applier pairs in <c>TimescaleDatabaseModelFactory</c>. Annotations
    /// no renderer consumes fall back to <c>.HasAnnotation(...)</c>, keeping the generated model complete.
    /// </remarks>
    public sealed class TimescaleDbAnnotationCodeGenerator(AnnotationCodeGeneratorDependencies dependencies)
        : NpgsqlAnnotationCodeGenerator(dependencies)
    {
        private static readonly IReadOnlyList<IFeatureAnnotationRenderer> Renderers =
        [
            new HypertableAnnotationRenderer(),
        ];

        public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            List<MethodCallCodeFragment> calls = [.. base.GenerateFluentApiCalls(entityType, annotations)];

            foreach (IFeatureAnnotationRenderer renderer in Renderers)
            {
                calls.AddRange(renderer.GenerateFluentApiCalls(entityType, annotations));
            }

            return calls;
        }

        public override IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations)
        {
            List<AttributeCodeFragment> attributes = [.. base.GenerateDataAnnotationAttributes(entityType, annotations)];

            foreach (IFeatureAnnotationRenderer renderer in Renderers)
            {
                attributes.AddRange(renderer.GenerateDataAnnotationAttributes(entityType, annotations));
            }

            return attributes;
        }
    }
}
#pragma warning restore EF1001

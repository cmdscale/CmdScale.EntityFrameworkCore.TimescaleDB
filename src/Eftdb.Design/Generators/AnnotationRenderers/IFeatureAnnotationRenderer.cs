using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators.AnnotationRenderers
{
    /// <summary>
    /// Renders the scaffolded <c>TimescaleDB:*</c> annotations of a single feature as Fluent API
    /// calls (default scaffold mode) or DataAnnotation attributes (<c>--data-annotations</c>).
    /// </summary>
    /// <remarks>
    /// Implementations must be stateless; per-call state travels in the annotations dictionary.
    /// Annotations that are rendered must be removed from the dictionary (consumed) - anything left
    /// falls back to <c>.HasAnnotation(...)</c>, keeping the generated model complete. In-place
    /// rewrites of unconsumed annotations are allowed (e.g. translating database column names to
    /// CLR property names before the fallback renders them).
    /// </remarks>
    internal interface IFeatureAnnotationRenderer
    {
        /// <summary>
        /// Renders the feature's Fluent API fragments for the entity. Each returned fragment is
        /// chained onto the scaffolded <c>entity...</c> builder statement in list order.
        /// </summary>
        IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations);

        /// <summary>
        /// Renders the feature's DataAnnotation attributes for the entity.
        /// </summary>
        IReadOnlyList<AttributeCodeFragment> GenerateDataAnnotationAttributes(
            IEntityType entityType, IDictionary<string, IAnnotation> annotations);

        /// <summary>
        /// Consumes the feature's annotations without generating output. Used in data-annotations
        /// scaffold mode where the feature is expressed as attributes on the entity file and must not
        /// fall back to <c>.HasAnnotation(...)</c> in <c>OnModelCreating</c>. Annotations that cannot
        /// be rendered (e.g. an unparseable view definition) are left in place so the fallback
        /// preserves them.
        /// </summary>
        void ConsumeFeatureAnnotations(IEntityType entityType, IDictionary<string, IAnnotation> annotations);
    }
}

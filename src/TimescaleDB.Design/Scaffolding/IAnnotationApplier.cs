using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Interface for applying TimescaleDB feature annotations to scaffolded database tables.
    /// </summary>
    internal interface IAnnotationApplier
    {
        /// <summary>
        /// Applies annotations to the database table based on the feature metadata.
        /// </summary>
        void ApplyAnnotations(DatabaseTable table, object featureInfo);
    }
}

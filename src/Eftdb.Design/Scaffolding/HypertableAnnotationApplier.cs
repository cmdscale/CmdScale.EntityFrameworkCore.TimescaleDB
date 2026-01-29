using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using System.Text.Json;
using static CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding.HypertableScaffoldingExtractor;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design.Scaffolding
{
    /// <summary>
    /// Applies hypertable annotations to scaffolded database tables.
    /// </summary>
    public sealed class HypertableAnnotationApplier : IAnnotationApplier
    {
        public void ApplyAnnotations(DatabaseTable table, object featureInfo)
        {
            if (featureInfo is not HypertableInfo info)
            {
                throw new ArgumentException($"Expected {nameof(HypertableInfo)}, got {featureInfo.GetType().Name}", nameof(featureInfo));
            }

            table[HypertableAnnotations.IsHypertable] = true;
            table[HypertableAnnotations.HypertableTimeColumn] = info.TimeColumnName;
            table[HypertableAnnotations.ChunkTimeInterval] = info.ChunkTimeInterval;
            table[HypertableAnnotations.EnableCompression] = info.CompressionEnabled;

            if (info.ChunkSkipColumns.Count > 0)
            {
                table[HypertableAnnotations.ChunkSkipColumns] = string.Join(",", info.ChunkSkipColumns);
            }

            // Apply SegmentBy annotation if present
            if (info.CompressionSegmentBy.Count > 0)
            {
                table[HypertableAnnotations.CompressionSegmentBy] = string.Join(", ", info.CompressionSegmentBy);
            }

            // Apply OrderBy annotation if present
            if (info.CompressionOrderBy.Count > 0)
            {
                table[HypertableAnnotations.CompressionOrderBy] = string.Join(", ", info.CompressionOrderBy);
            }

            if (info.AdditionalDimensions.Count > 0)
            {
                table[HypertableAnnotations.AdditionalDimensions] = JsonSerializer.Serialize(info.AdditionalDimensions);
            }
        }
    }
}

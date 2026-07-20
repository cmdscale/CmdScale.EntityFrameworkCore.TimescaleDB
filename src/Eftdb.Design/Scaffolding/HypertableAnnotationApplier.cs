using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
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

            RemoveAutoCreatedIndexes(table, info);

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

        /// <summary>
        /// Removes the indexes TimescaleDB creates automatically for a hypertable — the descending time
        /// index (<c>{table}_{timeColumn}_idx</c>) and one composite per additional dimension
        /// (<c>{table}_{dimensionColumn}_{timeColumn}_idx</c>) — so they do not scaffold as explicit
        /// index attributes the hand-written model never declared. The shape check (exact name, exact
        /// columns, non-unique) keeps user-defined indexes intact.
        /// </summary>
        private static void RemoveAutoCreatedIndexes(DatabaseTable table, HypertableInfo info)
        {
            List<DatabaseIndex> autoCreated = [];
            foreach (DatabaseIndex index in table.Indexes)
            {
                if (index.IsUnique)
                {
                    continue;
                }

                if (index.Name == $"{table.Name}_{info.TimeColumnName}_idx"
                    && index.Columns.Count == 1
                    && index.Columns[0].Name == info.TimeColumnName)
                {
                    autoCreated.Add(index);
                    continue;
                }

                foreach (Dimension dimension in info.AdditionalDimensions)
                {
                    if (index.Name == $"{table.Name}_{dimension.ColumnName}_{info.TimeColumnName}_idx"
                        && index.Columns.Count == 2
                        && index.Columns[0].Name == dimension.ColumnName
                        && index.Columns[1].Name == info.TimeColumnName)
                    {
                        autoCreated.Add(index);
                        break;
                    }
                }
            }

            foreach (DatabaseIndex index in autoCreated)
            {
                table.Indexes.Remove(index);
            }
        }
    }
}

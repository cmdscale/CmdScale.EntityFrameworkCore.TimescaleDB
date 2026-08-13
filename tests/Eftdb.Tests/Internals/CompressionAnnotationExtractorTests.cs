using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Tests for <see cref="CompressionAnnotationExtractor"/>.
/// </summary>
public class CompressionAnnotationExtractorTests
{
    private static (IEntityType EntityType, StoreObjectIdentifier StoreIdentifier) GetEntityAndStore<TContext>(
        TContext context, string tableName) where TContext : DbContext
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType entityType = model.GetEntityTypes().Single(e => e.GetTableName() == tableName);
        StoreObjectIdentifier storeIdentifier = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        return (entityType, storeIdentifier);
    }

    // ── ExtractSparseIndex: paren-less entry passes through verbatim ──────────

    #region Should_Pass_Through_Paren_Less_Entry_Verbatim

    private class ParenLessEntity
    {
        public DateTime Ts { get; set; }
        public double Value { get; set; }
    }

    private class ParenLessContext : DbContext
    {
        public DbSet<ParenLessEntity> Metrics => Set<ParenLessEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParenLessEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("annex_paren_less");
                entity.HasAnnotation(HypertableAnnotations.CompressionSparseIndex, "bloom");
            });
        }
    }

    [Fact]
    public void Should_Pass_Through_Paren_Less_Entry_Verbatim()
    {
        // Arrange
        using ParenLessContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStore(context, "annex_paren_less");

        // Act
        string? result = CompressionAnnotationExtractor.ExtractSparseIndex(entityType, storeIdentifier);

        // Assert
        Assert.Equal("bloom", result);
    }

    #endregion
}

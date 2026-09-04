using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify the policy conventions parse the InitialStart attribute string into a
/// Utc-kind DateTime annotation, independent of the host time zone.
/// </summary>
public class PolicyInitialStartConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static DateTime AssertUtcDateTime(IEntityType entityType, string annotationKey)
    {
        object? value = entityType.FindAnnotation(annotationKey)?.Value;
        Assert.NotNull(value);
        Assert.IsType<DateTime>(value);
        DateTime dateTime = (DateTime)value;
        Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
        return dateTime;
    }

    // ── Reorder policy attribute ───────────────────────────────────────────────

    #region Reorder_Attribute_Z_Suffix_Produces_Utc_Annotation

    [Hypertable("Timestamp")]
    [ReorderPolicy("reorder_attr_idx", InitialStart = "2025-09-23T09:15:19Z")]
    private class ReorderAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReorderAttributeContext : DbContext
    {
        public DbSet<ReorderAttributeEntity> Entities => Set<ReorderAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReorderAttributeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reorder_attr");
            });
        }
    }

    [Fact]
    public void Reorder_Attribute_Z_Suffix_Produces_Utc_Annotation()
    {
        // Arrange
        using ReorderAttributeContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(ReorderAttributeEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, ReorderPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    // ── Retention policy attribute ─────────────────────────────────────────────

    #region Retention_Attribute_Z_Suffix_Produces_Utc_Annotation

    [Hypertable("Timestamp")]
    [RetentionPolicy(DropAfter = "7 days", InitialStart = "2025-09-23T09:15:19Z")]
    private class RetentionAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RetentionAttributeContext : DbContext
    {
        public DbSet<RetentionAttributeEntity> Entities => Set<RetentionAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetentionAttributeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("retention_attr");
            });
        }
    }

    [Fact]
    public void Retention_Attribute_Z_Suffix_Produces_Utc_Annotation()
    {
        // Arrange
        using RetentionAttributeContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(RetentionAttributeEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, RetentionPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion

    // ── Compression policy attribute ───────────────────────────────────────────

    #region Compression_Attribute_Z_Suffix_Produces_Utc_Annotation

    [Hypertable("Timestamp")]
    [CompressionPolicy(After = "7 days", InitialStart = "2025-09-23T09:15:19Z")]
    private class CompressionAttributeEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionAttributeContext : DbContext
    {
        public DbSet<CompressionAttributeEntity> Entities => Set<CompressionAttributeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionAttributeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_attr");
            });
        }
    }

    [Fact]
    public void Compression_Attribute_Z_Suffix_Produces_Utc_Annotation()
    {
        // Arrange
        using CompressionAttributeContext context = new();

        // Act
        IEntityType entityType = GetModel(context).FindEntityType(typeof(CompressionAttributeEntity))!;

        // Assert
        DateTime stored = AssertUtcDateTime(entityType, CompressionPolicyAnnotations.InitialStart);
        Assert.Equal(new DateTime(2025, 9, 23, 9, 15, 19, DateTimeKind.Utc), stored);
    }

    #endregion
}

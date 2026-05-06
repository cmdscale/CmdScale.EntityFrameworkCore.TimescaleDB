using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Tests that verify <see cref="ColumnNameResolver"/> resolves either a CLR property
/// name or a raw column name back to the database column name on a given entity.
/// Reverse lookup is the path used by the design-time scaffolder, which emits
/// already-translated column names into TimescaleDB annotations.
/// </summary>
public class ColumnNameResolverTests
{
    private static (IEntityType entityType, StoreObjectIdentifier storeIdentifier) GetEntityAndStoreIdentifier<TContext>(TContext context, string tableName)
        where TContext : DbContext
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType entityType = model.GetEntityTypes().Single(e => e.GetTableName() == tableName);
        StoreObjectIdentifier storeIdentifier = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        return (entityType, storeIdentifier);
    }

    #region Should_Resolve_Clr_Property_Name_To_Column_Name_With_Default_Convention

    private class DefaultConventionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultConventionContext : DbContext
    {
        public DbSet<DefaultConventionMetric> Metrics => Set<DefaultConventionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultConventionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Clr_Property_Name_To_Column_Name_With_Default_Convention()
    {
        // Arrange
        using DefaultConventionContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "Metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Timestamp", storeIdentifier);

        // Assert
        Assert.Equal("Timestamp", resolved);
    }

    #endregion

    #region Should_Resolve_Clr_Property_Name_Under_Snake_Case_Convention

    private class SnakeCaseClrMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class SnakeCaseClrContext : DbContext
    {
        public DbSet<SnakeCaseClrMetric> Metrics => Set<SnakeCaseClrMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SnakeCaseClrMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Clr_Property_Name_Under_Snake_Case_Convention()
    {
        // Arrange — passing the CLR property name should yield the snake_case column.
        using SnakeCaseClrContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "Metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Timestamp", storeIdentifier);

        // Assert
        Assert.Equal("timestamp", resolved);
    }

    #endregion

    #region Should_Resolve_Value_Already_In_Column_Name_Form_Via_Reverse_Lookup

    private class ReverseLookupMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ReverseLookupContext : DbContext
    {
        public DbSet<ReverseLookupMetric> Metrics => Set<ReverseLookupMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReverseLookupMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Value_Already_In_Column_Name_Form_Via_Reverse_Lookup()
    {
        // Arrange — feeding the snake_case column name (as the scaffolder emits) must
        // still resolve to the matching column via reverse lookup.
        using ReverseLookupContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "Metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "timestamp", storeIdentifier);

        // Assert
        Assert.Equal("timestamp", resolved);
    }

    #endregion

    #region Should_Return_Null_For_Unknown_Name

    private class UnknownNameMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class UnknownNameContext : DbContext
    {
        public DbSet<UnknownNameMetric> Metrics => Set<UnknownNameMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnknownNameMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Fact]
    public void Should_Return_Null_For_Unknown_Name()
    {
        // Arrange
        using UnknownNameContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "Metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "DoesNotExist", storeIdentifier);

        // Assert
        Assert.Null(resolved);
    }

    #endregion

    #region Should_Return_Null_For_Null_Or_Whitespace_Input

    private class NullOrWhitespaceInputMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NullOrWhitespaceInputContext : DbContext
    {
        public DbSet<NullOrWhitespaceInputMetric> Metrics => Set<NullOrWhitespaceInputMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullOrWhitespaceInputMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
            });
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Return_Null_For_Null_Or_Whitespace_Input(string? input)
    {
        // Arrange
        using NullOrWhitespaceInputContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "Metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, input, storeIdentifier);

        // Assert
        Assert.Null(resolved);
    }

    #endregion
}

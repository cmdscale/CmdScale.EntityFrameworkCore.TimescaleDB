using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System.ComponentModel.DataAnnotations.Schema;

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
        // Arrange
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
        // Arrange
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

    // ── Complex-type support ──

    #region Should_Resolve_Dotted_Path_On_ComplexType_Property_To_Default_Column

    [ComplexType]
    private class MeasurementParams1
    {
        public double Value { get; set; }
    }

    private class ComplexFwdMetric
    {
        public DateTime Timestamp { get; set; }
        public MeasurementParams1 Param1 { get; set; } = new();
    }

    private class ComplexFwdContext : DbContext
    {
        public DbSet<ComplexFwdMetric> Metrics => Set<ComplexFwdMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexFwdMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_fwd_metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Dotted_Path_On_ComplexType_Property_To_Default_Column()
    {
        // Arrange
        using ComplexFwdContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "complex_fwd_metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Param1.Value", storeIdentifier);

        // Assert
        Assert.Equal("Param1_Value", resolved);
    }

    #endregion

    #region Should_Resolve_Dotted_Path_On_ComplexType_Property_Under_SnakeCase

    [ComplexType]
    private class MeasurementParams2
    {
        public double Value { get; set; }
    }

    private class ComplexSnakeCaseMetric
    {
        public DateTime Timestamp { get; set; }
        public MeasurementParams2 Param1 { get; set; } = new();
    }

    private class ComplexSnakeCaseContext : DbContext
    {
        public DbSet<ComplexSnakeCaseMetric> Metrics => Set<ComplexSnakeCaseMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexSnakeCaseMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_snake_metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Dotted_Path_On_ComplexType_Property_Under_SnakeCase()
    {
        // Arrange
        using ComplexSnakeCaseContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "complex_snake_metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Param1.Value", storeIdentifier);

        // Assert
        Assert.Equal("param1_value", resolved);
    }

    #endregion

    #region Should_Resolve_Column_Form_Of_ComplexType_Property_Via_Reverse_Lookup

    [ComplexType]
    private class MeasurementParams3
    {
        public double Value { get; set; }
    }

    private class ComplexReverseMetric
    {
        public DateTime Timestamp { get; set; }
        public MeasurementParams3 Param1 { get; set; } = new();
    }

    private class ComplexReverseContext : DbContext
    {
        public DbSet<ComplexReverseMetric> Metrics => Set<ComplexReverseMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ComplexReverseMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("complex_rev_metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Column_Form_Of_ComplexType_Property_Via_Reverse_Lookup()
    {
        // Arrange
        using ComplexReverseContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "complex_rev_metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Param1_Value", storeIdentifier);

        // Assert
        Assert.Equal("Param1_Value", resolved);
    }

    #endregion

    #region Should_Resolve_Forward_And_Reverse_For_Nested_Complex_Within_Complex

    [ComplexType]
    private class DeepInnerComplex
    {
        public double Value { get; set; }
    }

    [ComplexType]
    private class DeepOuterComplex
    {
        public DeepInnerComplex Inner { get; set; } = new();
    }

    private class NestedComplexMetric
    {
        public DateTime Timestamp { get; set; }
        public DeepOuterComplex Outer { get; set; } = new();
    }

    private class NestedComplexContext : DbContext
    {
        public DbSet<NestedComplexMetric> Metrics => Set<NestedComplexMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NestedComplexMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("nested_complex_metrics");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Forward_And_Reverse_For_Nested_Complex_Within_Complex()
    {
        // Arrange
        using NestedComplexContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "nested_complex_metrics");

        // Act
        string? forwardResolved = ColumnNameResolver.Resolve(entityType, "Outer.Inner.Value", storeIdentifier);
        string? reverseResolved = ColumnNameResolver.Resolve(entityType, "Outer_Inner_Value", storeIdentifier);

        // Assert
        Assert.Equal("Outer_Inner_Value", forwardResolved);
        Assert.Equal("Outer_Inner_Value", reverseResolved);
    }

    #endregion

    #region Should_Return_Null_For_Unresolvable_Dotted_Path

    [ComplexType]
    private class GhostParams
    {
        public double Value { get; set; }
    }

    private class UnresolvablePathMetric
    {
        public DateTime Timestamp { get; set; }
        public GhostParams Param1 { get; set; } = new();
    }

    private class UnresolvablePathContext : DbContext
    {
        public DbSet<UnresolvablePathMetric> Metrics => Set<UnresolvablePathMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnresolvablePathMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("unresolvable_path_metrics");
            });
        }
    }

    [Fact]
    public void Should_Return_Null_For_Unresolvable_Dotted_Path()
    {
        // Arrange
        using UnresolvablePathContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "unresolvable_path_metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Param1.Ghost", storeIdentifier);

        // Assert
        Assert.Null(resolved);
    }

    #endregion

    #region Should_ResolveProperty_With_IgnoreCase_Mixed_Case_Name

    [ComplexType]
    private class IgnoreCaseParams
    {
        public double Value { get; set; }
    }

    private class IgnoreCaseMetric
    {
        public DateTime Timestamp { get; set; }
        public IgnoreCaseParams Param1 { get; set; } = new();
    }

    private class IgnoreCaseContext : DbContext
    {
        public DbSet<IgnoreCaseMetric> Metrics => Set<IgnoreCaseMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IgnoreCaseMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ignore_case_metrics");
            });
        }
    }

    [Fact]
    public void Should_ResolveProperty_With_IgnoreCase_Mixed_Case_Name()
    {
        // Arrange
        using IgnoreCaseContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "ignore_case_metrics");

        // Act
        IProperty? propertyViaMixedCase = ColumnNameResolver.ResolveProperty(entityType, "PARAM1.VALUE", storeIdentifier, ignoreCase: true);
        IProperty? propertyViaColumnMixedCase = ColumnNameResolver.ResolveProperty(entityType, "PARAM1_VALUE", storeIdentifier, ignoreCase: true);

        // Assert
        Assert.NotNull(propertyViaMixedCase);
        Assert.Equal("Value", propertyViaMixedCase.Name);
        Assert.NotNull(propertyViaColumnMixedCase);
        Assert.Equal("Value", propertyViaColumnMixedCase.Name);
    }

    #endregion

    #region Should_ResolveProperty_Return_Null_For_Null_Or_Whitespace_Input

    private class NullInputMetric
    {
        public DateTime Timestamp { get; set; }
    }

    private class NullInputContext : DbContext
    {
        public DbSet<NullInputMetric> Metrics => Set<NullInputMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullInputMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("null_input_metrics");
            });
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ResolveProperty_Return_Null_For_Null_Or_Whitespace_Input(string? input)
    {
        // Arrange
        using NullInputContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "null_input_metrics");

        // Act
        IProperty? resolved = ColumnNameResolver.ResolveProperty(entityType, input, storeIdentifier);

        // Assert
        Assert.Null(resolved);
    }

    #endregion

    #region Should_Return_Null_When_Intermediate_Segment_Is_Not_A_Complex_Property

    [ComplexType]
    private class BrokenSegmentParams
    {
        public double Value { get; set; }
    }

    private class BrokenSegmentMetric
    {
        public DateTime Timestamp { get; set; }
        public BrokenSegmentParams Param1 { get; set; } = new();
    }

    private class BrokenSegmentContext : DbContext
    {
        public DbSet<BrokenSegmentMetric> Metrics => Set<BrokenSegmentMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BrokenSegmentMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("broken_segment_metrics");
            });
        }
    }

    [Fact]
    public void Should_Return_Null_When_Intermediate_Segment_Is_Not_A_Complex_Property()
    {
        // Arrange
        using BrokenSegmentContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "broken_segment_metrics");

        // Act & Assert
        Assert.Null(ColumnNameResolver.Resolve(entityType, "Missing.Value", storeIdentifier));
        Assert.Null(ColumnNameResolver.Resolve(entityType, "Timestamp.Value", storeIdentifier));
    }

    #endregion

    #region Should_Return_Null_For_Path_Through_Complex_Collection

    private class CollectionChannel
    {
        public double Value { get; set; }
    }

    private class CollectionPathMetric
    {
        public DateTime Timestamp { get; set; }
        public List<CollectionChannel> Channels { get; set; } = [];
    }

    private class CollectionPathContext : DbContext
    {
        public DbSet<CollectionPathMetric> Metrics => Set<CollectionPathMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CollectionPathMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("collection_path_metrics");
                entity.ComplexCollection(x => x.Channels).ToJson();
            });
        }
    }

    [Fact]
    public void Should_Return_Null_For_Path_Through_Complex_Collection()
    {
        // Arrange
        using CollectionPathContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "collection_path_metrics");

        // Act
        string? resolved = ColumnNameResolver.Resolve(entityType, "Channels.Value", storeIdentifier);

        // Assert
        Assert.Null(resolved);
    }

    #endregion

    #region Should_Skip_Complex_Collection_During_Reverse_Lookup

    private class ReverseSkipChannel
    {
        public double Value { get; set; }
    }

    [ComplexType]
    private class ReverseSkipParams
    {
        public double Value { get; set; }
    }

    private class ReverseSkipMetric
    {
        public DateTime Timestamp { get; set; }
        public List<ReverseSkipChannel> Channels { get; set; } = [];
        public ReverseSkipParams Params { get; set; } = new();
    }

    private class ReverseSkipContext : DbContext
    {
        public DbSet<ReverseSkipMetric> Metrics => Set<ReverseSkipMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReverseSkipMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("reverse_skip_metrics");
                entity.ComplexCollection(x => x.Channels).ToJson();
            });
        }
    }

    [Fact]
    public void Should_Skip_Complex_Collection_During_Reverse_Lookup()
    {
        // Arrange
        using ReverseSkipContext context = new();
        (IEntityType entityType, StoreObjectIdentifier storeIdentifier) = GetEntityAndStoreIdentifier(context, "reverse_skip_metrics");

        // Act
        string? resolvedPastCollection = ColumnNameResolver.Resolve(entityType, "Params_Value", storeIdentifier);
        string? unresolvable = ColumnNameResolver.Resolve(entityType, "ghost_column", storeIdentifier);

        // Assert
        Assert.Equal("Params_Value", resolvedPastCollection);
        Assert.Null(unresolvable);
    }

    #endregion
}

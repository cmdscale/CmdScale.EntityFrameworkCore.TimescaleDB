using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

public class SparseIndexValidationConventionTests
{
    private static IModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    // ── SplitSparseIndexEntries ──

    #region Should_Split_Simple_Entries

    [Fact]
    public void Should_Split_Simple_Entries()
    {
        // Arrange
        string input = "bloom(a),minmax(b)";

        // Act
        List<string> entries = [.. CompressionAnnotationExtractor.SplitSparseIndexEntries(input)];

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Equal("bloom(a)", entries[0]);
        Assert.Equal("minmax(b)", entries[1]);
    }

    #endregion

    #region Should_Not_Split_Comma_Inside_Parens

    [Fact]
    public void Should_Not_Split_Comma_Inside_Parens()
    {
        // Arrange
        string input = "bloom(a, b),minmax(c)";

        // Act
        List<string> entries = [.. CompressionAnnotationExtractor.SplitSparseIndexEntries(input)];

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Equal("bloom(a, b)", entries[0]);
        Assert.Equal("minmax(c)", entries[1]);
    }

    #endregion

    #region Should_Return_Single_Entry_When_No_Top_Level_Comma

    [Fact]
    public void Should_Return_Single_Entry_When_No_Top_Level_Comma()
    {
        // Arrange
        string input = "bloom(a,b,c)";

        // Act
        List<string> entries = [.. CompressionAnnotationExtractor.SplitSparseIndexEntries(input)];

        // Assert
        Assert.Equal("bloom(a,b,c)", Assert.Single(entries));
    }

    #endregion

    #region Should_Return_Single_Entry_For_Single_Token

    [Fact]
    public void Should_Return_Single_Entry_For_Single_Token()
    {
        // Arrange
        string input = "bloom(col)";

        // Act
        List<string> entries = [.. CompressionAnnotationExtractor.SplitSparseIndexEntries(input)];

        // Assert
        Assert.Equal("bloom(col)", Assert.Single(entries));
    }

    #endregion

    #region Should_Return_Empty_Sequence_For_Empty_String

    [Fact]
    public void Should_Return_Empty_Sequence_For_Empty_String()
    {
        // Arrange
        string input = string.Empty;

        // Act
        List<string> entries = [.. CompressionAnnotationExtractor.SplitSparseIndexEntries(input)];

        // Assert
        Assert.Empty(entries);
    }

    #endregion

    // ── Absent annotation / empty string ──

    #region Should_Not_Throw_When_SparseIndex_Annotation_Absent

    private class NoSparseAnnotationEntity { public DateTime Ts { get; set; } }

    private class NoSparseAnnotationContext : DbContext
    {
        public DbSet<NoSparseAnnotationEntity> Metrics => Set<NoSparseAnnotationEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoSparseAnnotationEntity>(e =>
            {
                e.ToTable("val_sparse_absent");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    [Fact]
    public void Should_Not_Throw_When_SparseIndex_Annotation_Absent()
    {
        // Arrange / Act
        using NoSparseAnnotationContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(NoSparseAnnotationEntity))!;
        Assert.Null(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    #region Should_Throw_When_SparseIndex_Set_Without_CompressionOrderBy

    private class SparseNoOrderByEntity { public DateTime Ts { get; set; } }

    private class SparseNoOrderByContext : DbContext
    {
        public DbSet<SparseNoOrderByEntity> Metrics => Set<SparseNoOrderByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseNoOrderByEntity>(e =>
            {
                e.ToTable("val_sparse_no_orderby");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithSparseIndex("bloom(col)");
            });
    }

    [Fact]
    public void Should_Throw_When_SparseIndex_Set_Without_CompressionOrderBy()
    {
        // Arrange
        using SparseNoOrderByContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("sparse_index requires compress_orderby to be configured", exception.Message);
    }

    #endregion

    #region Should_Throw_When_EmptySparseIndex_Set_Without_CompressionOrderBy

    private class SparseEmptyNoOrderByEntity { public DateTime Ts { get; set; } }

    private class SparseEmptyNoOrderByContext : DbContext
    {
        public DbSet<SparseEmptyNoOrderByEntity> Metrics => Set<SparseEmptyNoOrderByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseEmptyNoOrderByEntity>(e =>
            {
                e.ToTable("val_sparse_empty_no_orderby");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithSparseIndex(string.Empty);
            });
    }

    [Fact]
    public void Should_Throw_When_EmptySparseIndex_Set_Without_CompressionOrderBy()
    {
        // Arrange
        using SparseEmptyNoOrderByContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("sparse_index requires compress_orderby to be configured", exception.Message);
    }

    #endregion

    #region Should_Not_Throw_When_SparseIndex_Is_Empty_String

    private class SparseEmptyStringEntity { public DateTime Ts { get; set; } }

    private class SparseEmptyStringContext : DbContext
    {
        public DbSet<SparseEmptyStringEntity> Metrics => Set<SparseEmptyStringEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseEmptyStringEntity>(e =>
            {
                e.ToTable("val_sparse_empty");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(string.Empty);
            });
    }

    [Fact]
    public void Should_Not_Throw_When_SparseIndex_Is_Empty_String()
    {
        // Arrange / Act
        using SparseEmptyStringContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(SparseEmptyStringEntity))!;
        Assert.Equal(string.Empty, entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex)?.Value);
    }

    #endregion

    // ── Valid configurations that must NOT throw ──

    #region Should_Not_Throw_For_Valid_Mixed_Entries

    private class ValidMixedEntity
    {
        public DateTime Ts { get; set; }
        public int TenantId { get; set; }
        public double Temperature { get; set; }
    }

    private class ValidMixedContext : DbContext
    {
        public DbSet<ValidMixedEntity> Metrics => Set<ValidMixedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ValidMixedEntity>(e =>
            {
                e.ToTable("val_sparse_valid_mixed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(TenantId), minmax(Temperature)");
            });
    }

    [Fact]
    public void Should_Not_Throw_For_Valid_Mixed_Entries()
    {
        // Arrange / Act
        using ValidMixedContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(ValidMixedEntity))!;
        Assert.NotNull(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    #region Should_Not_Throw_For_Entries_On_Columns_Without_Segmentby_Or_Orderby

    private class UnrelatedColumnsEntity
    {
        public DateTime Ts { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class UnrelatedColumnsContext : DbContext
    {
        public DbSet<UnrelatedColumnsEntity> Metrics => Set<UnrelatedColumnsEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnrelatedColumnsEntity>(e =>
            {
                e.ToTable("val_sparse_unrelated");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(DeviceId), minmax(Value)");
            });
    }

    [Fact]
    public void Should_Not_Throw_For_Entries_On_Columns_Without_Segmentby_Or_Orderby()
    {
        // Arrange / Act
        using UnrelatedColumnsContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(UnrelatedColumnsEntity))!;
        Assert.NotNull(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    // ── Rule 1: bloom including a segmentby column ──

    #region Should_Throw_Rule1_When_Bloom_Includes_SegmentBy_Column

    private class BloomSegmentByEntity
    {
        public DateTime Ts { get; set; }
        public int TenantId { get; set; }
        public double Value { get; set; }
    }

    private class BloomSegmentByContext : DbContext
    {
        public DbSet<BloomSegmentByEntity> Metrics => Set<BloomSegmentByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BloomSegmentByEntity>(e =>
            {
                e.ToTable("val_rule1_bloom_segmentby");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionSegmentBy(x => x.TenantId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(TenantId)");
            });
    }

    [Fact]
    public void Should_Throw_Rule1_When_Bloom_Includes_SegmentBy_Column()
    {
        // Arrange
        using BloomSegmentByContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("includes compress_segmentby column", exception.Message);
    }

    #endregion

    #region Should_Throw_Rule1_When_Composite_Bloom_Includes_SegmentBy_Column

    private class CompositeBloomSegmentByEntity
    {
        public DateTime Ts { get; set; }
        public int TenantId { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class CompositeBloomSegmentByContext : DbContext
    {
        public DbSet<CompositeBloomSegmentByEntity> Metrics => Set<CompositeBloomSegmentByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeBloomSegmentByEntity>(e =>
            {
                e.ToTable("val_rule1_composite_bloom_segmentby");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionSegmentBy(x => x.TenantId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(DeviceId,TenantId)");
            });
    }

    [Fact]
    public void Should_Throw_Rule1_When_Composite_Bloom_Includes_SegmentBy_Column()
    {
        // Arrange
        using CompositeBloomSegmentByContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("includes compress_segmentby column", exception.Message);
    }

    #endregion

    // ── Rule 2: single-column bloom on an orderby column ──

    #region Should_Throw_Rule2_When_Single_Bloom_On_OrderBy_Column

    private class SingleBloomOrderByEntity
    {
        public DateTime Ts { get; set; }
        public double Value { get; set; }
    }

    private class SingleBloomOrderByContext : DbContext
    {
        public DbSet<SingleBloomOrderByEntity> Metrics => Set<SingleBloomOrderByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleBloomOrderByEntity>(e =>
            {
                e.ToTable("val_rule2_single_bloom_orderby");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Value)])
                    .WithSparseIndex("bloom(Value)");
            });
    }

    [Fact]
    public void Should_Throw_Rule2_When_Single_Bloom_On_OrderBy_Column()
    {
        // Arrange
        using SingleBloomOrderByContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("is redundant because", exception.Message);
        Assert.Contains("compress_orderby column", exception.Message);
    }

    #endregion

    #region Should_Not_Throw_Rule2_Exempt_Composite_Bloom_With_OrderBy_Column

    private class CompositeBloomOrderByExemptEntity
    {
        public DateTime Ts { get; set; }
        public int Region { get; set; }
        public double Value { get; set; }
    }

    private class CompositeBloomOrderByExemptContext : DbContext
    {
        public DbSet<CompositeBloomOrderByExemptEntity> Metrics => Set<CompositeBloomOrderByExemptEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeBloomOrderByExemptEntity>(e =>
            {
                e.ToTable("val_rule2_exempt_composite");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Value)])
                    .WithSparseIndex("bloom(Region,Value)");
            });
    }

    [Fact]
    public void Should_Not_Throw_Rule2_Exempt_Composite_Bloom_With_OrderBy_Column()
    {
        // Arrange / Act
        using CompositeBloomOrderByExemptContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(CompositeBloomOrderByExemptEntity))!;
        Assert.NotNull(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    // ── Rule 3: minmax on a segmentby column ──

    #region Should_Throw_Rule3_When_Minmax_On_SegmentBy_Column

    private class MinmaxSegmentByEntity
    {
        public DateTime Ts { get; set; }
        public int TenantId { get; set; }
        public double Value { get; set; }
    }

    private class MinmaxSegmentByContext : DbContext
    {
        public DbSet<MinmaxSegmentByEntity> Metrics => Set<MinmaxSegmentByEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MinmaxSegmentByEntity>(e =>
            {
                e.ToTable("val_rule3_minmax_segmentby");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionSegmentBy(x => x.TenantId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("minmax(TenantId)");
            });
    }

    [Fact]
    public void Should_Throw_Rule3_When_Minmax_On_SegmentBy_Column()
    {
        // Arrange
        using MinmaxSegmentByContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("includes compress_segmentby column", exception.Message);
    }

    #endregion

    // ── Rule 4: minmax on an orderby column is ALLOWED ──

    #region Should_Not_Throw_Rule4_Minmax_On_OrderBy_Column_Is_Allowed

    private class MinmaxOrderByAllowedEntity
    {
        public DateTime Ts { get; set; }
        public double Value { get; set; }
    }

    private class MinmaxOrderByAllowedContext : DbContext
    {
        public DbSet<MinmaxOrderByAllowedEntity> Metrics => Set<MinmaxOrderByAllowedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MinmaxOrderByAllowedEntity>(e =>
            {
                e.ToTable("val_rule4_minmax_orderby_ok");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Value)])
                    .WithSparseIndex("minmax(Value)");
            });
    }

    [Fact]
    public void Should_Not_Throw_Rule4_Minmax_On_OrderBy_Column_Is_Allowed()
    {
        // Arrange / Act
        using MinmaxOrderByAllowedContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(MinmaxOrderByAllowedEntity))!;
        Assert.NotNull(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    // ── Rule 5: duplicate single-column entries ──

    #region Should_Throw_Rule5_When_Two_Single_Column_Entries_Target_Same_Column

    private class DuplicateSingleColumnEntity
    {
        public DateTime Ts { get; set; }
        public double Value { get; set; }
    }

    private class DuplicateSingleColumnContext : DbContext
    {
        public DbSet<DuplicateSingleColumnEntity> Metrics => Set<DuplicateSingleColumnEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DuplicateSingleColumnEntity>(e =>
            {
                e.ToTable("val_rule5_duplicate_single");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(Value), minmax(Value)");
            });
    }

    [Fact]
    public void Should_Throw_Rule5_When_Two_Single_Column_Entries_Target_Same_Column()
    {
        // Arrange
        using DuplicateSingleColumnContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("duplicate single-column sparse index entries", exception.Message);
    }

    #endregion

    #region Should_Not_Throw_Rule5_Exempt_Composite_Entry_Plus_SingleColumn_Same_Column

    private class CompositePlusSingleExemptEntity
    {
        public DateTime Ts { get; set; }
        public int Region { get; set; }
        public int Dev { get; set; }
        public double Value { get; set; }
    }

    private class CompositePlusSingleExemptContext : DbContext
    {
        public DbSet<CompositePlusSingleExemptEntity> Metrics => Set<CompositePlusSingleExemptEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositePlusSingleExemptEntity>(e =>
            {
                e.ToTable("val_rule5_composite_exempt");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(Region,Dev), bloom(Dev)");
            });
    }

    [Fact]
    public void Should_Not_Throw_Rule5_Exempt_Composite_Entry_Plus_SingleColumn_Same_Column()
    {
        // Arrange / Act
        using CompositePlusSingleExemptContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(CompositePlusSingleExemptEntity))!;
        Assert.NotNull(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion

    // ── Rule 6: malformed entries ──

    #region Should_Throw_Rule6_When_Entry_Missing_Parens

    private class MissingParensEntity { public DateTime Ts { get; set; } }

    private class MissingParensContext : DbContext
    {
        public DbSet<MissingParensEntity> Metrics => Set<MissingParensEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MissingParensEntity>(e =>
            {
                e.ToTable("val_rule6_missing_parens");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom_col");
            });
    }

    [Fact]
    public void Should_Throw_Rule6_When_Entry_Missing_Parens()
    {
        // Arrange
        using MissingParensContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("missing or unbalanced parentheses", exception.Message);
    }

    #endregion

    #region Should_Throw_Rule6_When_Entry_Has_Unknown_Function

    private class UnknownFunctionEntity { public DateTime Ts { get; set; } }

    private class UnknownFunctionContext : DbContext
    {
        public DbSet<UnknownFunctionEntity> Metrics => Set<UnknownFunctionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnknownFunctionEntity>(e =>
            {
                e.ToTable("val_rule6_unknown_func");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("brin(col)");
            });
    }

    [Fact]
    public void Should_Throw_Rule6_When_Entry_Has_Unknown_Function()
    {
        // Arrange
        using UnknownFunctionContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("only 'bloom' and 'minmax' are supported", exception.Message);
    }

    #endregion

    #region Should_Throw_Rule6_When_Entry_Has_Empty_Argument_List

    private class EmptyArgListEntity { public DateTime Ts { get; set; } }

    private class EmptyArgListContext : DbContext
    {
        public DbSet<EmptyArgListEntity> Metrics => Set<EmptyArgListEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EmptyArgListEntity>(e =>
            {
                e.ToTable("val_rule6_empty_args");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom()");
            });
    }

    [Fact]
    public void Should_Throw_Rule6_When_Entry_Has_Empty_Argument_List()
    {
        // Arrange
        using EmptyArgListContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("empty argument list", exception.Message);
    }

    #endregion

    // ── Column name resolution: CLR names on segmentby/orderby, column names in sparse index ──

    #region Should_Resolve_Column_Names_When_SegmentBy_Uses_CLR_Name_And_SparseIndex_Uses_Column_Name

    private class ClrVsColumnNameEntity
    {
        public DateTime Ts { get; set; }
        public int TenantId { get; set; }
        public double Value { get; set; }
    }

    private class ClrVsColumnNameContext : DbContext
    {
        public DbSet<ClrVsColumnNameEntity> Metrics => Set<ClrVsColumnNameEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ClrVsColumnNameEntity>(e =>
            {
                e.ToTable("val_clr_vs_col");
                e.HasNoKey();
                e.Property(x => x.TenantId).HasColumnName("tenant_id");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionSegmentBy(x => x.TenantId)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(tenant_id)");
            });
    }

    [Fact]
    public void Should_Resolve_Column_Names_When_SegmentBy_Uses_CLR_Name_And_SparseIndex_Uses_Column_Name()
    {
        // Arrange
        using ClrVsColumnNameContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("includes compress_segmentby column", exception.Message);
        Assert.Contains("tenant_id", exception.Message);
    }

    #endregion

    #region Should_Resolve_Column_Names_When_SparseIndex_Uses_CLR_Name_And_SegmentBy_Uses_Column_Name

    private class SparseClrSegmentByColNameEntity
    {
        public DateTime Ts { get; set; }
        public int TenantId { get; set; }
        public double Value { get; set; }
    }

    private class SparseClrSegmentByColNameContext : DbContext
    {
        public DbSet<SparseClrSegmentByColNameEntity> Metrics => Set<SparseClrSegmentByColNameEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseClrSegmentByColNameEntity>(e =>
            {
                e.ToTable("val_sparse_clr_seg_col");
                e.HasNoKey();
                e.Property(x => x.TenantId).HasColumnName("tenant_id");
                e.IsHypertable(x => x.Ts)
                    .HasAnnotation(HypertableAnnotations.CompressionSegmentBy, "tenant_id")
                    .HasAnnotation(HypertableAnnotations.CompressionOrderBy, "Ts DESC NULLS LAST")
                    .HasAnnotation(HypertableAnnotations.EnableCompression, true)
                    .WithSparseIndex("bloom(TenantId)");
            });
    }

    [Fact]
    public void Should_Resolve_Column_Names_When_SparseIndex_Uses_CLR_Name_And_SegmentBy_Uses_Column_Name()
    {
        // Arrange
        using SparseClrSegmentByColNameContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("includes compress_segmentby column", exception.Message);
        Assert.Contains("tenant_id", exception.Message);
    }

    #endregion

    #region Should_Resolve_OrderBy_Direction_Suffix_When_Checking_Rule2

    private class OrderByDirectionSuffixEntity
    {
        public DateTime Ts { get; set; }
        public double Value { get; set; }
    }

    private class OrderByDirectionSuffixContext : DbContext
    {
        public DbSet<OrderByDirectionSuffixEntity> Metrics => Set<OrderByDirectionSuffixEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByDirectionSuffixEntity>(e =>
            {
                e.ToTable("val_orderby_direction_suffix");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .HasAnnotation(HypertableAnnotations.CompressionOrderBy, "Value DESC NULLS LAST")
                    .HasAnnotation(HypertableAnnotations.EnableCompression, true)
                    .WithSparseIndex("bloom(Value)");
            });
    }

    [Fact]
    public void Should_Resolve_OrderBy_Direction_Suffix_When_Checking_Rule2()
    {
        // Arrange
        using OrderByDirectionSuffixContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("is redundant because", exception.Message);
        Assert.Contains("compress_orderby column", exception.Message);
    }

    #endregion

    #region Should_Resolve_OrderBy_Column_With_HasColumnName_When_Checking_Rule2

    private class OrderByColumnNameMappingEntity
    {
        public DateTime Ts { get; set; }
        public double SensorValue { get; set; }
    }

    private class OrderByColumnNameMappingContext : DbContext
    {
        public DbSet<OrderByColumnNameMappingEntity> Metrics => Set<OrderByColumnNameMappingEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OrderByColumnNameMappingEntity>(e =>
            {
                e.ToTable("val_orderby_col_mapping");
                e.HasNoKey();
                e.Property(x => x.SensorValue).HasColumnName("sensor_value");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.SensorValue)])
                    .WithSparseIndex("bloom(sensor_value)");
            });
    }

    [Fact]
    public void Should_Resolve_OrderBy_Column_With_HasColumnName_When_Checking_Rule2()
    {
        // Arrange
        using OrderByColumnNameMappingContext context = new();

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => GetModel(context));

        // Assert
        Assert.Contains("is redundant because", exception.Message);
        Assert.Contains("sensor_value", exception.Message);
    }

    #endregion

    // ── Non-hypertable entities are skipped ──

    #region Should_Not_Validate_Non_Hypertable_Entity

    private class PlainTableEntity { public int Id { get; set; } }

    private class PlainTableContext : DbContext
    {
        public DbSet<PlainTableEntity> Items => Set<PlainTableEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PlainTableEntity>(e =>
            {
                e.ToTable("val_plain_table");
                e.HasKey(x => x.Id);
                e.HasAnnotation(HypertableAnnotations.CompressionSparseIndex, "invalid_no_parens");
            });
    }

    [Fact]
    public void Should_Not_Validate_Non_Hypertable_Entity()
    {
        // Arrange / Act
        using PlainTableContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(PlainTableEntity))!;
        Assert.Null(entity.FindAnnotation(HypertableAnnotations.IsHypertable));
    }

    #endregion

    // ── Empty/whitespace entries in the raw value are silently skipped ────────

    #region Should_Skip_Whitespace_Only_Entry_Between_Valid_Entries

    private class WhitespaceEntryEntity
    {
        public DateTime Ts { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class WhitespaceEntryContext : DbContext
    {
        public DbSet<WhitespaceEntryEntity> Metrics => Set<WhitespaceEntryEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<WhitespaceEntryEntity>(e =>
            {
                e.ToTable("val_whitespace_entry");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .HasAnnotation(HypertableAnnotations.CompressionSparseIndex,
                        "bloom(DeviceId),   ,minmax(Value)");
            });
    }

    [Fact]
    public void Should_Skip_Whitespace_Only_Entry_Between_Valid_Entries()
    {
        // Arrange / Act
        using WhitespaceEntryContext context = new();
        IModel model = GetModel(context);

        // Assert
        IEntityType entity = model.FindEntityType(typeof(WhitespaceEntryEntity))!;
        Assert.NotNull(entity.FindAnnotation(HypertableAnnotations.CompressionSparseIndex));
    }

    #endregion
}

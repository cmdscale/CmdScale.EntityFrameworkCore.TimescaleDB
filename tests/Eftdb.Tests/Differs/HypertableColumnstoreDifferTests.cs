using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class HypertableColumnstoreDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

    // ── SparseIndex: null → value ──

    #region Should_Detect_SparseIndex_Added

    private class SparseIndexAddedEntity { public DateTime Ts { get; set; } }

    private class SparseIndexAddedSourceContext : DbContext
    {
        public DbSet<SparseIndexAddedEntity> Metrics => Set<SparseIndexAddedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexAddedEntity>(e =>
            {
                e.ToTable("diff_sparse_added");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    private class SparseIndexAddedTargetContext : DbContext
    {
        public DbSet<SparseIndexAddedEntity> Metrics => Set<SparseIndexAddedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexAddedEntity>(e =>
            {
                e.ToTable("diff_sparse_added");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    [Fact]
    public void Should_Detect_SparseIndex_Added()
    {
        // Arrange
        using SparseIndexAddedSourceContext sourceContext = new();
        using SparseIndexAddedTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Null(alterOp.OldCompressionSparseIndex);
        Assert.Equal("bloom(device_id)", alterOp.CompressionSparseIndex);
    }

    #endregion

    // ── SparseIndex: value → value2 ──

    #region Should_Detect_SparseIndex_Changed

    private class SparseIndexChangedEntity { public DateTime Ts { get; set; } }

    private class SparseIndexChangedSourceContext : DbContext
    {
        public DbSet<SparseIndexChangedEntity> Metrics => Set<SparseIndexChangedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexChangedEntity>(e =>
            {
                e.ToTable("diff_sparse_changed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    private class SparseIndexChangedTargetContext : DbContext
    {
        public DbSet<SparseIndexChangedEntity> Metrics => Set<SparseIndexChangedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexChangedEntity>(e =>
            {
                e.ToTable("diff_sparse_changed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id), minmax(temperature)");
            });
    }

    [Fact]
    public void Should_Detect_SparseIndex_Changed()
    {
        // Arrange
        using SparseIndexChangedSourceContext sourceContext = new();
        using SparseIndexChangedTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("bloom(device_id)", alterOp.OldCompressionSparseIndex);
        Assert.Equal("bloom(device_id), minmax(temperature)", alterOp.CompressionSparseIndex);
    }

    #endregion

    // ── SparseIndex: value → null ──

    #region Should_Detect_SparseIndex_Removed

    private class SparseIndexRemovedEntity { public DateTime Ts { get; set; } }

    private class SparseIndexRemovedSourceContext : DbContext
    {
        public DbSet<SparseIndexRemovedEntity> Metrics => Set<SparseIndexRemovedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexRemovedEntity>(e =>
            {
                e.ToTable("diff_sparse_removed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    private class SparseIndexRemovedTargetContext : DbContext
    {
        public DbSet<SparseIndexRemovedEntity> Metrics => Set<SparseIndexRemovedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexRemovedEntity>(e =>
            {
                e.ToTable("diff_sparse_removed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    [Fact]
    public void Should_Detect_SparseIndex_Removed()
    {
        // Arrange
        using SparseIndexRemovedSourceContext sourceContext = new();
        using SparseIndexRemovedTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("bloom(device_id)", alterOp.OldCompressionSparseIndex);
        Assert.Null(alterOp.CompressionSparseIndex);
    }

    #endregion

    // ── SparseIndex: value → "" (disable auto-created) ──

    #region Should_Detect_SparseIndex_Set_To_Empty_String

    private class SparseIndexToEmptyEntity { public DateTime Ts { get; set; } }

    private class SparseIndexToEmptySourceContext : DbContext
    {
        public DbSet<SparseIndexToEmptyEntity> Metrics => Set<SparseIndexToEmptyEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexToEmptyEntity>(e =>
            {
                e.ToTable("diff_sparse_to_empty");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    private class SparseIndexToEmptyTargetContext : DbContext
    {
        public DbSet<SparseIndexToEmptyEntity> Metrics => Set<SparseIndexToEmptyEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexToEmptyEntity>(e =>
            {
                e.ToTable("diff_sparse_to_empty");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex(string.Empty);
            });
    }

    [Fact]
    public void Should_Detect_SparseIndex_Set_To_Empty_String()
    {
        // Arrange
        using SparseIndexToEmptySourceContext sourceContext = new();
        using SparseIndexToEmptyTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("bloom(device_id)", alterOp.OldCompressionSparseIndex);
        Assert.Equal(string.Empty, alterOp.CompressionSparseIndex);
    }

    #endregion

    // ── SparseIndex: no change (both null) ──

    #region Should_Not_Emit_Alter_When_SparseIndex_Both_Null

    private class SparseIndexBothNullEntity { public DateTime Ts { get; set; } }

    private class SparseIndexBothNullContext : DbContext
    {
        public DbSet<SparseIndexBothNullEntity> Metrics => Set<SparseIndexBothNullEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexBothNullEntity>(e =>
            {
                e.ToTable("diff_sparse_both_null");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    [Fact]
    public void Should_Not_Emit_Alter_When_SparseIndex_Both_Null()
    {
        // Arrange
        using SparseIndexBothNullContext sourceContext = new();
        using SparseIndexBothNullContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    // ── SparseIndex: no change (both same value) ──

    #region Should_Not_Emit_Alter_When_SparseIndex_Unchanged

    private class SparseIndexUnchangedEntity { public DateTime Ts { get; set; } }

    private class SparseIndexUnchangedContext : DbContext
    {
        public DbSet<SparseIndexUnchangedEntity> Metrics => Set<SparseIndexUnchangedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SparseIndexUnchangedEntity>(e =>
            {
                e.ToTable("diff_sparse_unchanged");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)");
            });
    }

    [Fact]
    public void Should_Not_Emit_Alter_When_SparseIndex_Unchanged()
    {
        // Arrange
        using SparseIndexUnchangedContext sourceContext = new();
        using SparseIndexUnchangedContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    // ── CompressChunkTimeInterval: null → value ──

    #region Should_Detect_CompressChunkTimeInterval_Added

    private class CctiAddedEntity { public DateTime Ts { get; set; } }

    private class CctiAddedSourceContext : DbContext
    {
        public DbSet<CctiAddedEntity> Metrics => Set<CctiAddedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiAddedEntity>(e =>
            {
                e.ToTable("diff_ccti_added");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    private class CctiAddedTargetContext : DbContext
    {
        public DbSet<CctiAddedEntity> Metrics => Set<CctiAddedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiAddedEntity>(e =>
            {
                e.ToTable("diff_ccti_added");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("24 hours");
            });
    }

    [Fact]
    public void Should_Detect_CompressChunkTimeInterval_Added()
    {
        // Arrange
        using CctiAddedSourceContext sourceContext = new();
        using CctiAddedTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Null(alterOp.OldCompressChunkTimeInterval);
        Assert.Equal("24 hours", alterOp.CompressChunkTimeInterval);
    }

    #endregion

    // ── CompressChunkTimeInterval: value → value2 ──

    #region Should_Detect_CompressChunkTimeInterval_Changed

    private class CctiChangedEntity { public DateTime Ts { get; set; } }

    private class CctiChangedSourceContext : DbContext
    {
        public DbSet<CctiChangedEntity> Metrics => Set<CctiChangedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiChangedEntity>(e =>
            {
                e.ToTable("diff_ccti_changed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("24 hours");
            });
    }

    private class CctiChangedTargetContext : DbContext
    {
        public DbSet<CctiChangedEntity> Metrics => Set<CctiChangedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiChangedEntity>(e =>
            {
                e.ToTable("diff_ccti_changed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("7 days");
            });
    }

    [Fact]
    public void Should_Detect_CompressChunkTimeInterval_Changed()
    {
        // Arrange
        using CctiChangedSourceContext sourceContext = new();
        using CctiChangedTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("24 hours", alterOp.OldCompressChunkTimeInterval);
        Assert.Equal("7 days", alterOp.CompressChunkTimeInterval);
    }

    #endregion

    // ── CompressChunkTimeInterval: value → null ──

    #region Should_Detect_CompressChunkTimeInterval_Removed

    private class CctiRemovedEntity { public DateTime Ts { get; set; } }

    private class CctiRemovedSourceContext : DbContext
    {
        public DbSet<CctiRemovedEntity> Metrics => Set<CctiRemovedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiRemovedEntity>(e =>
            {
                e.ToTable("diff_ccti_removed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("24 hours");
            });
    }

    private class CctiRemovedTargetContext : DbContext
    {
        public DbSet<CctiRemovedEntity> Metrics => Set<CctiRemovedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiRemovedEntity>(e =>
            {
                e.ToTable("diff_ccti_removed");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    [Fact]
    public void Should_Detect_CompressChunkTimeInterval_Removed()
    {
        // Arrange
        using CctiRemovedSourceContext sourceContext = new();
        using CctiRemovedTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("24 hours", alterOp.OldCompressChunkTimeInterval);
        Assert.Null(alterOp.CompressChunkTimeInterval);
    }

    #endregion

    // ── CompressChunkTimeInterval: no change (both null) ──

    #region Should_Not_Emit_Alter_When_CompressChunkTimeInterval_Both_Null

    private class CctiBothNullEntity { public DateTime Ts { get; set; } }

    private class CctiBothNullContext : DbContext
    {
        public DbSet<CctiBothNullEntity> Metrics => Set<CctiBothNullEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiBothNullEntity>(e =>
            {
                e.ToTable("diff_ccti_both_null");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).EnableCompression();
            });
    }

    [Fact]
    public void Should_Not_Emit_Alter_When_CompressChunkTimeInterval_Both_Null()
    {
        // Arrange
        using CctiBothNullContext sourceContext = new();
        using CctiBothNullContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    // ── CompressChunkTimeInterval: no change (both same value) ──

    #region Should_Not_Emit_Alter_When_CompressChunkTimeInterval_Unchanged

    private class CctiUnchangedEntity { public DateTime Ts { get; set; } }

    private class CctiUnchangedContext : DbContext
    {
        public DbSet<CctiUnchangedEntity> Metrics => Set<CctiUnchangedEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CctiUnchangedEntity>(e =>
            {
                e.ToTable("diff_ccti_unchanged");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts).WithCompressChunkTimeInterval("24 hours");
            });
    }

    [Fact]
    public void Should_Not_Emit_Alter_When_CompressChunkTimeInterval_Unchanged()
    {
        // Arrange
        using CctiUnchangedContext sourceContext = new();
        using CctiUnchangedContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    // ── CreateHypertableOperation carries both settings ──

    #region Should_Populate_Both_Fields_On_CreateHypertableOperation

    private class BothSettingsCreateEntity { public DateTime Ts { get; set; } }

    private class BothSettingsCreateSourceContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder) { }
    }

    private class BothSettingsCreateTargetContext : DbContext
    {
        public DbSet<BothSettingsCreateEntity> Metrics => Set<BothSettingsCreateEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BothSettingsCreateEntity>(e =>
            {
                e.ToTable("diff_both_create");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(device_id)")
                    .WithCompressChunkTimeInterval("24 hours");
            });
    }

    [Fact]
    public void Should_Populate_Both_Fields_On_CreateHypertableOperation()
    {
        // Arrange
        using BothSettingsCreateSourceContext sourceContext = new();
        using BothSettingsCreateTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        CreateHypertableOperation? createOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
        Assert.Equal("bloom(device_id)", createOp.CompressionSparseIndex);
        Assert.Equal("24 hours", createOp.CompressChunkTimeInterval);
    }

    #endregion

    // ── SparseIndex: column name resolution via HasColumnName ──

    #region Should_Resolve_HasColumnName_When_Building_SparseIndex_Operation_Value

    private class ColNameResolutionEntity
    {
        public DateTime Ts { get; set; }
        public int DeviceId { get; set; }
    }

    private class ColNameResolutionSourceContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder) { }
    }

    private class ColNameResolutionTargetContext : DbContext
    {
        public DbSet<ColNameResolutionEntity> Metrics => Set<ColNameResolutionEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ColNameResolutionEntity>(e =>
            {
                e.ToTable("diff_col_name_resolution");
                e.HasNoKey();
                e.Property(x => x.DeviceId).HasColumnName("device_id");
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(DeviceId)");
            });
    }

    [Fact]
    public void Should_Resolve_HasColumnName_When_Building_SparseIndex_Operation_Value()
    {
        // Arrange
        using ColNameResolutionSourceContext sourceContext = new();
        using ColNameResolutionTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        CreateHypertableOperation? createOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
        Assert.Equal("bloom(device_id)", createOp.CompressionSparseIndex);
    }

    #endregion

    // ── SparseIndex: canonicalization of whitespace inside args ──

    #region Should_Not_Emit_Alter_When_SparseIndex_Differs_Only_In_Whitespace

    private class SpaceCanonEntity { public DateTime Ts { get; set; } }

    private class SpaceCanonSourceContext : DbContext
    {
        public DbSet<SpaceCanonEntity> Metrics => Set<SpaceCanonEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SpaceCanonEntity>(e =>
            {
                e.ToTable("diff_space_canon");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(a, b)");
            });
    }

    private class SpaceCanonTargetContext : DbContext
    {
        public DbSet<SpaceCanonEntity> Metrics => Set<SpaceCanonEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SpaceCanonEntity>(e =>
            {
                e.ToTable("diff_space_canon");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(a,b)");
            });
    }

    [Fact]
    public void Should_Not_Emit_Alter_When_SparseIndex_Differs_Only_In_Whitespace()
    {
        // Arrange
        using SpaceCanonSourceContext sourceContext = new();
        using SpaceCanonTargetContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion

    // ── RewriteSource passes both settings through unchanged ──

    #region Should_Not_Emit_Alter_When_Both_Settings_Identical_After_Rewrite

    private class RewritePassthroughEntity { public DateTime Ts { get; set; } }

    private class RewritePassthroughContext : DbContext
    {
        public DbSet<RewritePassthroughEntity> Metrics => Set<RewritePassthroughEntity>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RewritePassthroughEntity>(e =>
            {
                e.ToTable("diff_rewrite_passthrough");
                e.HasNoKey();
                e.IsHypertable(x => x.Ts)
                    .WithCompressionOrderBy(s => [s.ByDescending(x => x.Ts)])
                    .WithSparseIndex("bloom(col)")
                    .WithCompressChunkTimeInterval("7 days");
            });
    }

    [Fact]
    public void Should_Not_Emit_Alter_When_Both_Settings_Identical_After_Rewrite()
    {
        // Arrange
        using RewritePassthroughContext sourceContext = new();
        using RewritePassthroughContext targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        // Assert
        Assert.Empty(operations);
    }

    #endregion
}

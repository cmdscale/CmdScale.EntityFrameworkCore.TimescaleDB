using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Differs;

public class HypertableDifferTests
{
    private static IRelationalModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }

    #region Should_Detect_New_Hypertable

    private class MetricEntity1
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class EmptyContext1 : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }

    private class BasicHypertableContext1 : DbContext
    {
        public DbSet<MetricEntity1> Metrics => Set<MetricEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity1>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Detect_New_Hypertable()
    {
        using EmptyContext1 emptyContext = new();
        using BasicHypertableContext1 hypertableContext = new();

        IRelationalModel sourceModel = GetModel(emptyContext);
        IRelationalModel targetModel = GetModel(hypertableContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        CreateHypertableOperation? createOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
        Assert.Equal("Metrics", createOp.TableName);
        Assert.Equal("Timestamp", createOp.TimeColumnName);
        Assert.Equal("7 days", createOp.ChunkTimeInterval);
    }

    #endregion

    #region Should_Detect_Multiple_New_Hypertables

    private class MetricEntity2
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LogEntity2
    {
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
    }

    private class EmptyContext2 : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }

    private class MultipleHypertablesContext2 : DbContext
    {
        public DbSet<MetricEntity2> Metrics => Set<MetricEntity2>();
        public DbSet<LogEntity2> Logs => Set<LogEntity2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity2>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<LogEntity2>(entity =>
            {
                entity.ToTable("Logs");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_New_Hypertables()
    {
        using EmptyContext2 emptyContext = new();
        using MultipleHypertablesContext2 multiContext = new();

        IRelationalModel sourceModel = GetModel(emptyContext);
        IRelationalModel targetModel = GetModel(multiContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        List<CreateHypertableOperation> createOps = [.. operations.OfType<CreateHypertableOperation>()];
        Assert.Equal(2, createOps.Count);
        Assert.Contains(createOps, op => op.TableName == "Metrics");
        Assert.Contains(createOps, op => op.TableName == "Logs");
    }

    #endregion

    #region Should_Detect_Hypertable_With_Custom_ChunkInterval

    private class MetricEntity3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class EmptyContext3 : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }

    private class CustomChunkIntervalContext3 : DbContext
    {
        public DbSet<MetricEntity3> Metrics => Set<MetricEntity3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity3>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public void Should_Detect_Hypertable_With_Custom_ChunkInterval()
    {
        using EmptyContext3 emptyContext = new();
        using CustomChunkIntervalContext3 customContext = new();

        IRelationalModel sourceModel = GetModel(emptyContext);
        IRelationalModel targetModel = GetModel(customContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        CreateHypertableOperation? createOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
        Assert.Equal("1 day", createOp.ChunkTimeInterval);
    }

    #endregion

    #region Should_Detect_ChunkTimeInterval_Change

    private class MetricEntity4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity4>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class CustomChunkIntervalContext4 : DbContext
    {
        public DbSet<MetricEntity4> Metrics => Set<MetricEntity4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity4>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public void Should_Detect_ChunkTimeInterval_Change()
    {
        using BasicHypertableContext4 sourceContext = new();
        using CustomChunkIntervalContext4 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("Metrics", alterOp.TableName);
        Assert.Equal("7 days", alterOp.OldChunkTimeInterval);
        Assert.Equal("1 day", alterOp.ChunkTimeInterval);
    }

    #endregion

    #region Should_Detect_EnableCompression_Change

    private class MetricEntity5
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class CompressionEnabledContext5 : DbContext
    {
        public DbSet<MetricEntity5> Metrics => Set<MetricEntity5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity5>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public void Should_Detect_EnableCompression_Change()
    {
        using BasicHypertableContext5 sourceContext = new();
        using CompressionEnabledContext5 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.False(alterOp.OldEnableCompression);
        Assert.True(alterOp.EnableCompression);
    }

    #endregion

    #region Should_Detect_ChunkSkipColumns_Added

    private class MetricEntity6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity6>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class ChunkSkippingEnabledContext6 : DbContext
    {
        public DbSet<MetricEntity6> Metrics => Set<MetricEntity6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity6>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value);
            });
        }
    }

    [Fact]
    public void Should_Detect_ChunkSkipColumns_Added()
    {
        using BasicHypertableContext6 sourceContext = new();
        using ChunkSkippingEnabledContext6 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Null(alterOp.OldChunkSkipColumns);
        Assert.NotNull(alterOp.ChunkSkipColumns);
        Assert.Contains("Value", alterOp.ChunkSkipColumns);
    }

    #endregion

    #region Should_Detect_ChunkSkipColumns_Modified

    private class MetricEntity7
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class ChunkSkippingEnabledContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity7>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value);
            });
        }
    }

    private class ChunkSkippingModifiedContext7 : DbContext
    {
        public DbSet<MetricEntity7> Metrics => Set<MetricEntity7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity7>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value, x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Detect_ChunkSkipColumns_Modified()
    {
        using ChunkSkippingEnabledContext7 sourceContext = new();
        using ChunkSkippingModifiedContext7 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Single(alterOp.OldChunkSkipColumns!);
        Assert.Equal(2, alterOp.ChunkSkipColumns!.Count);
    }

    #endregion

    #region Should_Detect_AdditionalDimensions_Added

    private class MetricEntity8
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class BasicHypertableContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class AdditionalDimensionsContext8 : DbContext
    {
        public DbSet<MetricEntity8> Metrics => Set<MetricEntity8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity8>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void Should_Detect_AdditionalDimensions_Added()
    {
        using BasicHypertableContext8 sourceContext = new();
        using AdditionalDimensionsContext8 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Null(alterOp.OldAdditionalDimensions);
        Assert.NotNull(alterOp.AdditionalDimensions);
        Dimension dimension = Assert.Single(alterOp.AdditionalDimensions);
        Assert.Equal("DeviceId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
    }

    #endregion

    #region Should_Detect_Multiple_Property_Changes

    private class MetricEntity9
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class BasicHypertableContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity9>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class FullyConfiguredHypertableContext9 : DbContext
    {
        public DbSet<MetricEntity9> Metrics => Set<MetricEntity9>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity9>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day")
                      .EnableCompression()
                      .WithChunkSkipping(x => x.Value)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void Should_Detect_Multiple_Property_Changes()
    {
        using BasicHypertableContext9 sourceContext = new();
        using FullyConfiguredHypertableContext9 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.Equal("1 day", alterOp.ChunkTimeInterval);
        Assert.True(alterOp.EnableCompression);
        Assert.NotNull(alterOp.ChunkSkipColumns);
        Assert.NotNull(alterOp.AdditionalDimensions);
    }

    #endregion

    #region Should_Not_Generate_Operations_When_No_Changes

    private class MetricEntity10
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext10 : DbContext
    {
        public DbSet<MetricEntity10> Metrics => Set<MetricEntity10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity10>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Not_Generate_Operations_When_No_Changes()
    {
        using BasicHypertableContext10 sourceContext = new();
        using BasicHypertableContext10 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Not_Detect_Change_When_ChunkSkipColumns_Order_Different

    private class MetricEntity11
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class ChunkSkippingTwoColumnsContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity11>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value, x => x.DeviceId);
            });
        }
    }

    private class ChunkSkippingTwoColumnsReorderedContext11 : DbContext
    {
        public DbSet<MetricEntity11> Metrics => Set<MetricEntity11>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity11>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.DeviceId, x => x.Value);
            });
        }
    }

    [Fact]
    public void Should_Not_Detect_Change_When_ChunkSkipColumns_Order_Different()
    {
        using ChunkSkippingTwoColumnsContext11 sourceContext = new();
        using ChunkSkippingTwoColumnsReorderedContext11 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Handle_Null_Source_Model

    private class MetricEntity12
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext12 : DbContext
    {
        public DbSet<MetricEntity12> Metrics => Set<MetricEntity12>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity12>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Source_Model()
    {
        using BasicHypertableContext12 targetContext = new();
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, targetModel);

        CreateHypertableOperation? createOp = operations.OfType<CreateHypertableOperation>().FirstOrDefault();
        Assert.NotNull(createOp);
    }

    #endregion

    #region Should_Handle_Null_Target_Model

    private class MetricEntity13
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext13 : DbContext
    {
        public DbSet<MetricEntity13> Metrics => Set<MetricEntity13>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity13>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Handle_Null_Target_Model()
    {
        using BasicHypertableContext13 sourceContext = new();
        IRelationalModel sourceModel = GetModel(sourceContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, null);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Handle_Both_Null_Models

    [Fact]
    public void Should_Handle_Both_Null_Models()
    {
        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(null, null);

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Detect_Dimension_Type_Change

    private class MetricEntity14
    {
        public DateTime Timestamp { get; set; }
        public int DeviceId { get; set; }
        public double Value { get; set; }
    }

    private class HashDimensionContext14 : DbContext
    {
        public DbSet<MetricEntity14> Metrics => Set<MetricEntity14>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    private class RangeDimensionContext14 : DbContext
    {
        public DbSet<MetricEntity14> Metrics => Set<MetricEntity14>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateRange("DeviceId", "1000"));
            });
        }
    }

    [Fact]
    public void Should_Detect_Dimension_Type_Change()
    {
        using HashDimensionContext14 sourceContext = new();
        using RangeDimensionContext14 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotNull(alterOp.OldAdditionalDimensions);
        Assert.NotNull(alterOp.AdditionalDimensions);
        Assert.Equal(EDimensionType.Hash, alterOp.OldAdditionalDimensions![0].Type);
        Assert.Equal(EDimensionType.Range, alterOp.AdditionalDimensions![0].Type);
    }

    #endregion

    #region Should_Detect_RangeDimension_Added_WithIntegerInterval

    private class MetricEntity14b
    {
        public DateTime Timestamp { get; set; }
        public int SequenceId { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext14b : DbContext
    {
        public DbSet<MetricEntity14b> Metrics => Set<MetricEntity14b>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14b>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class RangeDimensionIntegerContext14b : DbContext
    {
        public DbSet<MetricEntity14b> Metrics => Set<MetricEntity14b>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14b>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateRange("SequenceId", "10000"));
            });
        }
    }

    [Fact]
    public void Should_Detect_RangeDimension_Added_WithIntegerInterval()
    {
        using BasicHypertableContext14b sourceContext = new();
        using RangeDimensionIntegerContext14b targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotNull(alterOp.AdditionalDimensions);
        Dimension dimension = Assert.Single(alterOp.AdditionalDimensions);
        Assert.Equal("SequenceId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Range, dimension.Type);
        Assert.Equal("10000", dimension.Interval);
    }

    #endregion

    #region Should_Detect_RangeDimension_Added_WithTimeInterval

    private class MetricEntity14c
    {
        public DateTime Timestamp { get; set; }
        public DateTime ProcessedTime { get; set; }
        public double Value { get; set; }
    }

    private class BasicHypertableContext14c : DbContext
    {
        public DbSet<MetricEntity14c> Metrics => Set<MetricEntity14c>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14c>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    private class RangeDimensionTimeContext14c : DbContext
    {
        public DbSet<MetricEntity14c> Metrics => Set<MetricEntity14c>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity14c>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateRange("ProcessedTime", "1 day"));
            });
        }
    }

    [Fact]
    public void Should_Detect_RangeDimension_Added_WithTimeInterval()
    {
        using BasicHypertableContext14c sourceContext = new();
        using RangeDimensionTimeContext14c targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotNull(alterOp.AdditionalDimensions);
        Dimension dimension = Assert.Single(alterOp.AdditionalDimensions);
        Assert.Equal("ProcessedTime", dimension.ColumnName);
        Assert.Equal(EDimensionType.Range, dimension.Type);
        Assert.Equal("1 day", dimension.Interval);
    }

    #endregion

    #region Should_Detect_Dimension_Partitions_Change

    private class MetricEntity15
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class HashDimension4PartitionsContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    private class HashDimension8PartitionsContext15 : DbContext
    {
        public DbSet<MetricEntity15> Metrics => Set<MetricEntity15>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity15>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 8));
            });
        }
    }

    [Fact]
    public void Should_Detect_Dimension_Partitions_Change()
    {
        using HashDimension4PartitionsContext15 sourceContext = new();
        using HashDimension8PartitionsContext15 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotNull(alterOp.OldAdditionalDimensions);
        Assert.NotNull(alterOp.AdditionalDimensions);
        Assert.Equal(4, alterOp.OldAdditionalDimensions![0].NumberOfPartitions);
        Assert.Equal(8, alterOp.AdditionalDimensions![0].NumberOfPartitions);
    }

    #endregion

    #region Should_Detect_Dimension_Removed

    private class MetricEntity16
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class WithDimensionContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    private class WithoutDimensionContext16 : DbContext
    {
        public DbSet<MetricEntity16> Metrics => Set<MetricEntity16>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity16>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Detect_Dimension_Removed()
    {
        using WithDimensionContext16 sourceContext = new();
        using WithoutDimensionContext16 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotNull(alterOp.OldAdditionalDimensions);
        Assert.Single(alterOp.OldAdditionalDimensions);
        Assert.Null(alterOp.AdditionalDimensions);
    }

    #endregion

    #region Should_Detect_Compression_Disabled

    private class MetricEntity17
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionEnabledContext17 : DbContext
    {
        public DbSet<MetricEntity17> Metrics => Set<MetricEntity17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity17>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    private class CompressionDisabledContext17 : DbContext
    {
        public DbSet<MetricEntity17> Metrics => Set<MetricEntity17>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity17>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Detect_Compression_Disabled()
    {
        using CompressionEnabledContext17 sourceContext = new();
        using CompressionDisabledContext17 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.True(alterOp.OldEnableCompression);
        Assert.False(alterOp.EnableCompression);
    }

    #endregion

    #region Should_Detect_ChunkSkipColumns_Removed

    private class MetricEntity18
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ChunkSkippingEnabledContext18 : DbContext
    {
        public DbSet<MetricEntity18> Metrics => Set<MetricEntity18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity18>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.Value);
            });
        }
    }

    private class ChunkSkippingDisabledContext18 : DbContext
    {
        public DbSet<MetricEntity18> Metrics => Set<MetricEntity18>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MetricEntity18>(entity =>
            {
                entity.ToTable("Metrics");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Detect_ChunkSkipColumns_Removed()
    {
        using ChunkSkippingEnabledContext18 sourceContext = new();
        using ChunkSkippingDisabledContext18 targetContext = new();

        IRelationalModel sourceModel = GetModel(sourceContext);
        IRelationalModel targetModel = GetModel(targetContext);

        HypertableDiffer differ = new();

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(sourceModel, targetModel);

        AlterHypertableOperation? alterOp = operations.OfType<AlterHypertableOperation>().FirstOrDefault();
        Assert.NotNull(alterOp);
        Assert.NotNull(alterOp.OldChunkSkipColumns);
        Assert.Single(alterOp.OldChunkSkipColumns);
        Assert.Null(alterOp.ChunkSkipColumns);
    }

    #endregion
}

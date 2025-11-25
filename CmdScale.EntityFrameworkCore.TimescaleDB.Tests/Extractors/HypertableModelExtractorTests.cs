using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.Hypertables;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

public class HypertableModelExtractorTests
{
    private static IRelationalModel GetRelationalModel(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.GetRelationalModel();
    }

    #region Should_Extract_Minimal_Hypertable

    private class MinimalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MinimalHypertableContext : DbContext
    {
        public DbSet<MinimalMetric> Metrics => Set<MinimalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MinimalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Extract_Minimal_Hypertable()
    {
        using MinimalHypertableContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        CreateHypertableOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("Timestamp", operation.TimeColumnName);
        Assert.Equal("7 days", operation.ChunkTimeInterval);
        Assert.False(operation.EnableCompression);
        Assert.Null(operation.ChunkSkipColumns);
        Assert.Null(operation.AdditionalDimensions);
    }

    #endregion

    #region Should_Return_Empty_When_No_Hypertables

    private class PlainEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class NoHypertableContext : DbContext
    {
        public DbSet<PlainEntity> Entities => Set<PlainEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainEntity>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.ToTable("Plain");
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_Hypertables()
    {
        using NoHypertableContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Return_Empty_When_RelationalModel_Is_Null

    [Fact]
    public void Should_Return_Empty_When_RelationalModel_Is_Null()
    {
        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(null)];

        Assert.Empty(operations);
    }

    #endregion

    #region Should_Resolve_Column_Names_With_Snake_Case_Convention

    private class SnakeCaseMetric
    {
        public DateTime TimestampUtc { get; set; }
        public double Value { get; set; }
    }

    private class SnakeCaseContext : DbContext
    {
        public DbSet<SnakeCaseMetric> Metrics => Set<SnakeCaseMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SnakeCaseMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.TimestampUtc);
            });
        }
    }

    [Fact]
    public void Should_Resolve_Column_Names_With_Snake_Case_Convention()
    {
        using SnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        CreateHypertableOperation operation = operations[0];
        Assert.Equal("timestamp_utc", operation.TimeColumnName);
    }

    #endregion

    #region Should_Extract_ChunkTimeInterval

    private class ChunkIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ChunkTimeIntervalContext : DbContext
    {
        public DbSet<ChunkIntervalMetric> Metrics => Set<ChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
            });
        }
    }

    [Fact]
    public void Should_Extract_ChunkTimeInterval()
    {
        using ChunkTimeIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.Equal("1 day", operations[0].ChunkTimeInterval);
    }

    #endregion

    #region Should_Use_Default_ChunkTimeInterval_When_Not_Specified

    private class DefaultChunkIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DefaultChunkIntervalContext : DbContext
    {
        public DbSet<DefaultChunkIntervalMetric> Metrics => Set<DefaultChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultChunkIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Use_Default_ChunkTimeInterval_When_Not_Specified()
    {
        using DefaultChunkIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.Equal(DefaultValues.ChunkTimeInterval, operations[0].ChunkTimeInterval);
    }

    #endregion

    #region Should_Extract_EnableCompression_True

    private class CompressionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionEnabledContext : DbContext
    {
        public DbSet<CompressionMetric> Metrics => Set<CompressionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .EnableCompression();
            });
        }
    }

    [Fact]
    public void Should_Extract_EnableCompression_True()
    {
        using CompressionEnabledContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.True(operations[0].EnableCompression);
    }

    #endregion

    #region Should_Extract_EnableCompression_False_By_Default

    private class NoCompressionMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoCompressionContext : DbContext
    {
        public DbSet<NoCompressionMetric> Metrics => Set<NoCompressionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoCompressionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Extract_EnableCompression_False_By_Default()
    {
        using NoCompressionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.False(operations[0].EnableCompression);
    }

    #endregion

    #region Should_Extract_Single_ChunkSkipColumn

    private class SingleChunkSkipMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class SingleChunkSkipColumnContext : DbContext
    {
        public DbSet<SingleChunkSkipMetric> Metrics => Set<SingleChunkSkipMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleChunkSkipMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Extract_Single_ChunkSkipColumn()
    {
        using SingleChunkSkipColumnContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].ChunkSkipColumns);
        string column = Assert.Single(operations[0].ChunkSkipColumns!);
        Assert.Equal("DeviceId", column);
    }

    #endregion

    #region Should_Extract_Multiple_ChunkSkipColumns

    private class MultipleChunkSkipMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class MultipleChunkSkipColumnsContext : DbContext
    {
        public DbSet<MultipleChunkSkipMetric> Metrics => Set<MultipleChunkSkipMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleChunkSkipMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.DeviceId, x => x.Location);
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_ChunkSkipColumns()
    {
        using MultipleChunkSkipColumnsContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].ChunkSkipColumns);
        Assert.Equal(2, operations[0].ChunkSkipColumns!.Count);
        Assert.Contains("DeviceId", operations[0].ChunkSkipColumns!);
        Assert.Contains("Location", operations[0].ChunkSkipColumns!);
    }

    #endregion

    #region Should_Resolve_ChunkSkipColumns_With_Naming_Convention

    private class ChunkSkipSnakeCaseMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class ChunkSkipSnakeCaseContext : DbContext
    {
        public DbSet<ChunkSkipSnakeCaseMetric> Metrics => Set<ChunkSkipSnakeCaseMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChunkSkipSnakeCaseMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkSkipping(x => x.DeviceId);
            });
        }
    }

    [Fact]
    public void Should_Resolve_ChunkSkipColumns_With_Naming_Convention()
    {
        using ChunkSkipSnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].ChunkSkipColumns);
        string column = Assert.Single(operations[0].ChunkSkipColumns!);
        Assert.Equal("device_id", column);
    }

    #endregion

    #region Should_Extract_Hash_Dimension

    private class HashDimensionMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class HashDimensionContext : DbContext
    {
        public DbSet<HashDimensionMetric> Metrics => Set<HashDimensionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HashDimensionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void Should_Extract_Hash_Dimension()
    {
        using HashDimensionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].AdditionalDimensions);
        Dimension dimension = Assert.Single(operations[0].AdditionalDimensions!);
        Assert.Equal("DeviceId", dimension.ColumnName);
        Assert.Equal(EDimensionType.Hash, dimension.Type);
        Assert.Equal(4, dimension.NumberOfPartitions);
    }

    #endregion

    #region Should_Extract_Range_Dimension

    private class RangeDimensionMetric
    {
        public DateTime Timestamp { get; set; }
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class RangeDimensionContext : DbContext
    {
        public DbSet<RangeDimensionMetric> Metrics => Set<RangeDimensionMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RangeDimensionMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateRange("Location", "1000"));
            });
        }
    }

    [Fact]
    public void Should_Extract_Range_Dimension()
    {
        using RangeDimensionContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].AdditionalDimensions);
        Dimension dimension = Assert.Single(operations[0].AdditionalDimensions!);
        Assert.Equal("Location", dimension.ColumnName);
        Assert.Equal(EDimensionType.Range, dimension.Type);
        Assert.Equal("1000", dimension.Interval);
    }

    #endregion

    #region Should_Extract_Multiple_Dimensions

    private class MultipleDimensionsMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class MultipleDimensionsContext : DbContext
    {
        public DbSet<MultipleDimensionsMetric> Metrics => Set<MultipleDimensionsMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleDimensionsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4))
                      .HasDimension(Dimension.CreateRange("Location", "1000"));
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_Dimensions()
    {
        using MultipleDimensionsContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].AdditionalDimensions);
        Assert.Equal(2, operations[0].AdditionalDimensions!.Count);

        Dimension hashDim = operations[0].AdditionalDimensions![0];
        Assert.Equal("DeviceId", hashDim.ColumnName);
        Assert.Equal(EDimensionType.Hash, hashDim.Type);
        Assert.Equal(4, hashDim.NumberOfPartitions);

        Dimension rangeDim = operations[0].AdditionalDimensions![1];
        Assert.Equal("Location", rangeDim.ColumnName);
        Assert.Equal(EDimensionType.Range, rangeDim.Type);
        Assert.Equal("1000", rangeDim.Interval);
    }

    #endregion

    #region Should_Resolve_Dimension_Column_Names_With_Naming_Convention

    private class DimensionSnakeCaseMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class DimensionSnakeCaseContext : DbContext
    {
        public DbSet<DimensionSnakeCaseMetric> Metrics => Set<DimensionSnakeCaseMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention()
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DimensionSnakeCaseMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void Should_Resolve_Dimension_Column_Names_With_Naming_Convention()
    {
        using DimensionSnakeCaseContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        Assert.NotNull(operations[0].AdditionalDimensions);
        Dimension dimension = Assert.Single(operations[0].AdditionalDimensions!);
        Assert.Equal("device_id", dimension.ColumnName);
    }

    #endregion

    #region Should_Extract_Multiple_Hypertables

    private class MultipleHypertablesMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultipleHypertablesEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultipleHypertablesContext : DbContext
    {
        public DbSet<MultipleHypertablesMetric> Metrics => Set<MultipleHypertablesMetric>();
        public DbSet<MultipleHypertablesEvent> Events => Set<MultipleHypertablesEvent>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleHypertablesMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultipleHypertablesEvent>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Events");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_Hypertables()
    {
        using MultipleHypertablesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Equal(2, operations.Count);
        Assert.Contains(operations, op => op.TableName == "Metrics");
        Assert.Contains(operations, op => op.TableName == "Events");
    }

    #endregion

    #region Should_Extract_Fully_Configured_Hypertable

    private class FullyConfiguredMetric
    {
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class FullyConfiguredContext : DbContext
    {
        public DbSet<FullyConfiguredMetric> Metrics => Set<FullyConfiguredMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FullyConfiguredMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 hour")
                      .EnableCompression()
                      .WithChunkSkipping(x => x.DeviceId)
                      .HasDimension(Dimension.CreateHash("DeviceId", 4));
            });
        }
    }

    [Fact]
    public void Should_Extract_Fully_Configured_Hypertable()
    {
        using FullyConfiguredContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        List<CreateHypertableOperation> operations = [.. HypertableModelExtractor.GetHypertables(relationalModel)];

        Assert.Single(operations);
        CreateHypertableOperation operation = operations[0];
        Assert.Equal("Metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("Timestamp", operation.TimeColumnName);
        Assert.Equal("1 hour", operation.ChunkTimeInterval);
        Assert.True(operation.EnableCompression);
        Assert.NotNull(operation.ChunkSkipColumns);
        Assert.Single(operation.ChunkSkipColumns);
        Assert.Equal("DeviceId", operation.ChunkSkipColumns[0]);
        Assert.NotNull(operation.AdditionalDimensions);
        Dimension dimension = Assert.Single(operation.AdditionalDimensions);
        Assert.Equal("DeviceId", dimension.ColumnName);
    }

    #endregion
}

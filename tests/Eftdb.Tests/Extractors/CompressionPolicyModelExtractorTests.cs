using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.CompressionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals.Features.CompressionPolicies;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extractors;

/// <summary>
/// Tests that verify CompressionPolicyModelExtractor correctly extracts compression policy
/// configurations from EF Core models and pairs them with the owning hypertable's chunk time interval.
/// </summary>
public class CompressionPolicyModelExtractorTests
{
    private static IRelationalModel GetRelationalModel(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        return model.GetRelationalModel();
    }

    #region Should_Return_Empty_When_RelationalModel_Is_Null

    [Fact]
    public void Should_Return_Empty_When_RelationalModel_Is_Null()
    {
        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(null)];

        // Assert
        Assert.Empty(entries);
    }

    #endregion

    #region Should_Return_Empty_When_No_CompressionPolicy

    private class NoCompressionPolicyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoCompressionPolicyContext : DbContext
    {
        public DbSet<NoCompressionPolicyMetric> Metrics => Set<NoCompressionPolicyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoCompressionPolicyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("no_compression_policy_metrics");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Return_Empty_When_No_CompressionPolicy()
    {
        // Arrange
        using NoCompressionPolicyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Empty(entries);
    }

    #endregion

    #region Should_Skip_When_HasCompressionPolicy_But_Neither_After_Nor_CreatedBefore

    private class HasPolicyNoIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class HasPolicyNoIntervalContext : DbContext
    {
        public DbSet<HasPolicyNoIntervalMetric> Metrics => Set<HasPolicyNoIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HasPolicyNoIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("has_policy_no_interval_metrics");
                entity.IsHypertable(x => x.Timestamp);
                entity.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);
            });
        }
    }

    [Fact]
    public void Should_Skip_When_HasCompressionPolicy_But_Neither_After_Nor_CreatedBefore()
    {
        // Arrange
        using HasPolicyNoIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Empty(entries);
    }

    #endregion

    #region Should_Extract_Entry_With_After_Only

    private class AfterOnlyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class AfterOnlyContext : DbContext
    {
        public DbSet<AfterOnlyMetric> Metrics => Set<AfterOnlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AfterOnlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("after_only_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Entry_With_After_Only()
    {
        // Arrange
        using AfterOnlyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        AddCompressionPolicyOperation operation = Assert.Single(entries).Operation;
        Assert.Equal("after_only_metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Equal("7 days", operation.After);
        Assert.Null(operation.CreatedBefore);
    }

    #endregion

    #region Should_Extract_Entry_With_CreatedBefore_Only

    private class CreatedBeforeOnlyMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CreatedBeforeOnlyContext : DbContext
    {
        public DbSet<CreatedBeforeOnlyMetric> Metrics => Set<CreatedBeforeOnlyMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CreatedBeforeOnlyMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("created_before_only_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(createdBefore: "30 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Entry_With_CreatedBefore_Only()
    {
        // Arrange
        using CreatedBeforeOnlyContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        AddCompressionPolicyOperation operation = Assert.Single(entries).Operation;
        Assert.Equal("created_before_only_metrics", operation.TableName);
        Assert.Equal("public", operation.Schema);
        Assert.Null(operation.After);
        Assert.Equal("30 days", operation.CreatedBefore);
    }

    #endregion

    #region Should_Extract_ScheduleInterval

    private class ScheduleIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ScheduleIntervalContext : DbContext
    {
        public DbSet<ScheduleIntervalMetric> Metrics => Set<ScheduleIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScheduleIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("schedule_interval_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days", scheduleInterval: "12 hours");
            });
        }
    }

    [Fact]
    public void Should_Extract_ScheduleInterval()
    {
        // Arrange
        using ScheduleIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Equal("12 hours", Assert.Single(entries).Operation.ScheduleInterval);
    }

    #endregion

    #region Should_Have_Null_ScheduleInterval_When_Not_Specified

    private class NullScheduleIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NullScheduleIntervalContext : DbContext
    {
        public DbSet<NullScheduleIntervalMetric> Metrics => Set<NullScheduleIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullScheduleIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("null_schedule_interval_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Have_Null_ScheduleInterval_When_Not_Specified()
    {
        // Arrange
        using NullScheduleIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Null(Assert.Single(entries).Operation.ScheduleInterval);
    }

    #endregion

    #region Should_Extract_Timezone

    private class TimezoneMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class TimezoneContext : DbContext
    {
        public DbSet<TimezoneMetric> Metrics => Set<TimezoneMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimezoneMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("timezone_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days", timezone: "Europe/Berlin");
            });
        }
    }

    [Fact]
    public void Should_Extract_Timezone()
    {
        // Arrange
        using TimezoneContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Equal("Europe/Berlin", Assert.Single(entries).Operation.Timezone);
    }

    #endregion

    #region Should_Extract_IfNotExists

    private class IfNotExistsMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IfNotExistsContext : DbContext
    {
        public DbSet<IfNotExistsMetric> Metrics => Set<IfNotExistsMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IfNotExistsMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("if_not_exists_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days", ifNotExists: true);
            });
        }
    }

    [Fact]
    public void Should_Extract_IfNotExists()
    {
        // Arrange
        using IfNotExistsContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.True(Assert.Single(entries).Operation.IfNotExists);
    }

    #endregion

    #region Should_Extract_InitialStart

    private class InitialStartMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class InitialStartContext : DbContext
    {
        public DbSet<InitialStartMetric> Metrics => Set<InitialStartMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InitialStartMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("initial_start_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(
                          after: "7 days",
                          initialStart: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            });
        }
    }

    [Fact]
    public void Should_Extract_InitialStart()
    {
        // Arrange
        using InitialStartContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        DateTime? initialStart = Assert.Single(entries).Operation.InitialStart;
        Assert.NotNull(initialStart);
        DateTime expectedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDate, initialStart.Value);
    }

    #endregion

    #region Should_Have_Null_InitialStart_When_Not_Specified

    private class NullInitialStartMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NullInitialStartContext : DbContext
    {
        public DbSet<NullInitialStartMetric> Metrics => Set<NullInitialStartMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullInitialStartMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("null_initial_start_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Have_Null_InitialStart_When_Not_Specified()
    {
        // Arrange
        using NullInitialStartContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Null(Assert.Single(entries).Operation.InitialStart);
    }

    #endregion

    #region Should_Pair_Entry_With_Explicit_ChunkTimeInterval

    private class ExplicitChunkIntervalMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ExplicitChunkIntervalContext : DbContext
    {
        public DbSet<ExplicitChunkIntervalMetric> Metrics => Set<ExplicitChunkIntervalMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExplicitChunkIntervalMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("explicit_chunk_interval_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("14 days")
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Pair_Entry_With_Explicit_ChunkTimeInterval()
    {
        // Arrange
        using ExplicitChunkIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Equal("14 days", Assert.Single(entries).ChunkTimeInterval);
    }

    #endregion

    #region Should_Pair_Entry_With_DefaultChunkTimeInterval_When_Not_Specified

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
                entity.ToTable("default_chunk_interval_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Pair_Entry_With_DefaultChunkTimeInterval_When_Not_Specified()
    {
        // Arrange
        using DefaultChunkIntervalContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Equal(DefaultValues.ChunkTimeInterval, Assert.Single(entries).ChunkTimeInterval);
    }

    #endregion

    #region Should_Extract_Custom_Schema

    private class CustomSchemaMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CustomSchemaContext : DbContext
    {
        public DbSet<CustomSchemaMetric> Metrics => Set<CustomSchemaMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomSchemaMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("custom_schema_metrics", "analytics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Custom_Schema()
    {
        // Arrange
        using CustomSchemaContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Equal("analytics", Assert.Single(entries).Operation.Schema);
    }

    #endregion

    #region Should_Skip_Entity_With_No_Table_Or_View_Name

    private class NoTableNameMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoTableNameContext : DbContext
    {
        public DbSet<NoTableNameMetric> Metrics => Set<NoTableNameMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoTableNameMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable((string?)null);
                entity.HasAnnotation(CompressionPolicyAnnotations.HasCompressionPolicy, true);
                entity.HasAnnotation(CompressionPolicyAnnotations.After, "7 days");
            });
        }
    }

    [Fact]
    public void Should_Skip_Entity_With_No_Table_Or_View_Name()
    {
        // Arrange
        using NoTableNameContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Empty(entries);
    }

    #endregion

    #region Should_Extract_Entry_From_Attribute

    [Hypertable("Timestamp")]
    [CompressionPolicy("7 days")]
    private class CompressionPolicyAttributeMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CompressionPolicyAttributeContext : DbContext
    {
        public DbSet<CompressionPolicyAttributeMetric> Metrics => Set<CompressionPolicyAttributeMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompressionPolicyAttributeMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("compression_policy_attribute_metrics");
            });
        }
    }

    [Fact]
    public void Should_Extract_Entry_From_Attribute()
    {
        // Arrange
        using CompressionPolicyAttributeContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        AddCompressionPolicyOperation operation = Assert.Single(entries).Operation;
        Assert.Equal("compression_policy_attribute_metrics", operation.TableName);
        Assert.Equal("7 days", operation.After);
    }

    #endregion

    #region Should_Extract_Multiple_Entries

    private class MultipleFirstMetric
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultipleSecondMetric
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
    }

    private class MultiplePoliciesContext : DbContext
    {
        public DbSet<MultipleFirstMetric> Metrics => Set<MultipleFirstMetric>();
        public DbSet<MultipleSecondMetric> Events => Set<MultipleSecondMetric>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultipleFirstMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("multiple_first_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "7 days");
            });

            modelBuilder.Entity<MultipleSecondMetric>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("multiple_second_metrics");
                entity.IsHypertable(x => x.Timestamp)
                      .WithCompressionPolicy(after: "14 days");
            });
        }
    }

    [Fact]
    public void Should_Extract_Multiple_Entries()
    {
        // Arrange
        using MultiplePoliciesContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Operation.TableName == "multiple_first_metrics");
        Assert.Contains(entries, e => e.Operation.TableName == "multiple_second_metrics");
    }

    #endregion

    #region Should_Extract_Fully_Configured_Entry

    private class FullyConfiguredMetric
    {
        public DateTime Timestamp { get; set; }
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
                entity.ToTable("fully_configured_metrics", "timeseries");
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("14 days")
                      .WithCompressionPolicy(
                          after: "7 days",
                          scheduleInterval: "6 hours",
                          initialStart: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                          timezone: "UTC",
                          ifNotExists: true);
            });
        }
    }

    [Fact]
    public void Should_Extract_Fully_Configured_Entry()
    {
        // Arrange
        using FullyConfiguredContext context = new();
        IRelationalModel relationalModel = GetRelationalModel(context);

        // Act
        List<CompressionPolicyModelExtractor.CompressionPolicyEntry> entries =
            [.. CompressionPolicyModelExtractor.GetCompressionPolicyEntries(relationalModel)];

        // Assert
        CompressionPolicyModelExtractor.CompressionPolicyEntry entry = Assert.Single(entries);
        AddCompressionPolicyOperation operation = entry.Operation;

        Assert.Equal("fully_configured_metrics", operation.TableName);
        Assert.Equal("timeseries", operation.Schema);
        Assert.Equal("7 days", operation.After);
        Assert.Null(operation.CreatedBefore);
        Assert.Equal("6 hours", operation.ScheduleInterval);
        Assert.NotNull(operation.InitialStart);
        Assert.Equal(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), operation.InitialStart.Value);
        Assert.Equal("UTC", operation.Timezone);
        Assert.True(operation.IfNotExists);
        Assert.Equal("14 days", entry.ChunkTimeInterval);
    }

    #endregion
}

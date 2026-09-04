using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify ContinuousAggregateViewColumnValidationConvention rejects colliding view output
/// column names and invalid time-bucket property designations during model finalization.
/// </summary>
public class ContinuousAggregateViewColumnValidationConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Not_Throw_For_Clean_Model

    private class CleanRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class CleanAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CleanContext : DbContext
    {
        public DbSet<CleanRaw> Metrics => Set<CleanRaw>();
        public DbSet<CleanAggregate> HourlyMetrics => Set<CleanAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CleanRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("clean_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<CleanAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("hour_start");
                entity.IsContinuousAggregate<CleanAggregate, CleanRaw>(
                    "clean_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).WithTimeBucketProperty(x => x.Bucket)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Not_Throw_For_Clean_Model()
    {
        using CleanContext context = new();

        IModel model = GetModel(context);

        Assert.NotNull(model.FindEntityType(typeof(CleanAggregate)));
    }

    #endregion

    #region Should_Throw_When_Bucket_Collides_With_AggregateAlias

    private class BucketAliasRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class BucketAliasAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class BucketAliasContext : DbContext
    {
        public DbSet<BucketAliasRaw> Metrics => Set<BucketAliasRaw>();
        public DbSet<BucketAliasAggregate> HourlyMetrics => Set<BucketAliasAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketAliasRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("bucket_alias_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketAliasAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.AvgValue).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<BucketAliasAggregate, BucketAliasRaw>(
                    "bucket_alias_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Bucket_Collides_With_AggregateAlias()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using BucketAliasContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("BucketAliasAggregate", exception.Message);
        Assert.Contains("bucket_alias_hourly", exception.Message);
        Assert.Contains("time_bucket", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Bucket_Collides_With_GroupByColumn

    private class BucketGroupByRaw
    {
        public DateTime Timestamp { get; set; }
        public string TimeBucket { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class BucketGroupByAggregate
    {
        public DateTime Bucket { get; set; }
        public string TimeBucket { get; set; } = string.Empty;
        public double AvgValue { get; set; }
    }

    private class BucketGroupByContext : DbContext
    {
        public DbSet<BucketGroupByRaw> Metrics => Set<BucketGroupByRaw>();
        public DbSet<BucketGroupByAggregate> HourlyMetrics => Set<BucketGroupByAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BucketGroupByRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("bucket_group_by_raw");
                entity.Property(x => x.TimeBucket).HasColumnName("time_bucket");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<BucketGroupByAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<BucketGroupByAggregate, BucketGroupByRaw>(
                    "bucket_group_by_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddGroupByColumn(x => x.TimeBucket)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Bucket_Collides_With_GroupByColumn()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using BucketGroupByContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("bucket_group_by_hourly", exception.Message);
        Assert.Contains("time_bucket", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Designated_BucketProperty_Does_Not_Exist

    private class MissingPropertyRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MissingPropertyAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MissingPropertyContext : DbContext
    {
        public DbSet<MissingPropertyRaw> Metrics => Set<MissingPropertyRaw>();
        public DbSet<MissingPropertyAggregate> HourlyMetrics => Set<MissingPropertyAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingPropertyRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("missing_property_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MissingPropertyAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<MissingPropertyAggregate>(
                    "missing_property_hourly",
                    "Metrics",
                    "1 hour",
                    "Timestamp"
                ).WithTimeBucketProperty("NoSuchProperty")
                 .AddAggregateFunction("avg_value", "Value", EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Designated_BucketProperty_Does_Not_Exist()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using MissingPropertyContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("missing_property_hourly", exception.Message);
        Assert.Contains("NoSuchProperty", exception.Message);
    }

    #endregion

    #region Should_Not_Throw_For_RawViewDefinition_Even_With_Colliding_Columns

    private class RawDefinitionRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class RawDefinitionAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class RawDefinitionContext : DbContext
    {
        public DbSet<RawDefinitionRaw> Metrics => Set<RawDefinitionRaw>();
        public DbSet<RawDefinitionAggregate> HourlyMetrics => Set<RawDefinitionAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RawDefinitionRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("raw_definition_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<RawDefinitionAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.AvgValue).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<RawDefinitionAggregate, RawDefinitionRaw>(
                    "raw_definition_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"timestamp\") AS time_bucket, AVG(\"value\") AS time_bucket FROM \"raw_definition_raw\" GROUP BY 1");
            });
        }
    }

    [Fact]
    public void Should_Not_Throw_For_RawViewDefinition_Even_With_Colliding_Columns()
    {
        using RawDefinitionContext context = new();

        IModel model = GetModel(context);

        Assert.NotNull(model.FindEntityType(typeof(RawDefinitionAggregate)));
    }

    #endregion

    #region Should_Not_Throw_When_Aggregate_Has_No_Store_Object

    private class NoStoreRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NoStoreAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NoStoreContext : DbContext
    {
        public DbSet<NoStoreRaw> Metrics => Set<NoStoreRaw>();
        public DbSet<NoStoreAggregate> HourlyMetrics => Set<NoStoreAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoStoreRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("no_store_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NoStoreAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.AvgValue).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<NoStoreAggregate, NoStoreRaw>(
                    "no_store_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
                entity.ToView(null);
                entity.ToTable((string?)null);
            });
        }
    }

    [Fact]
    public void Should_Not_Throw_When_Aggregate_Has_No_Store_Object()
    {
        using NoStoreContext context = new();

        IModel model = GetModel(context);

        Assert.NotNull(model.FindEntityType(typeof(NoStoreAggregate)));
    }

    #endregion
}

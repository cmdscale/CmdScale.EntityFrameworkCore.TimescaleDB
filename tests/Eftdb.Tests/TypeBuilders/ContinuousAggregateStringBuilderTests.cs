using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.TypeBuilders;

/// <summary>
/// Tests for the scaffold-targeting <c>ContinuousAggregateStringBuilder</c> overloads that reference
/// columns by name string rather than by lambda selector.
/// </summary>
public class ContinuousAggregateStringBuilderTests
{
    private static IModel GetModel(DbContext context)
        => context.GetService<IDesignTimeModel>().Model;

    // ── WithTimeBucketProperty(string) guard ─────────────────────────────────

    #region WithTimeBucketProperty_Throws_When_PropertyName_Null

    private class NullBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class NullBucketAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class NullBucketContext : DbContext
    {
        public DbSet<NullBucketSource> Metrics => Set<NullBucketSource>();
        public DbSet<NullBucketAggregate> HourlyMetrics => Set<NullBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NullBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<NullBucketAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<NullBucketAggregate>(
                    "null_bucket_hourly",
                    "Metrics",
                    "1 hour",
                    "Timestamp"
                ).WithTimeBucketProperty(null!);
            });
        }
    }

    [Fact]
    public void WithTimeBucketProperty_Throws_When_PropertyName_Null()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
        {
            using NullBucketContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Equal("propertyName", exception.ParamName);
    }

    #endregion

    #region WithTimeBucketProperty_Throws_When_PropertyName_Whitespace

    private class WhitespaceBucketSource
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class WhitespaceBucketAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class WhitespaceBucketContext : DbContext
    {
        public DbSet<WhitespaceBucketSource> Metrics => Set<WhitespaceBucketSource>();
        public DbSet<WhitespaceBucketAggregate> HourlyMetrics => Set<WhitespaceBucketAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WhitespaceBucketSource>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("Metrics");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<WhitespaceBucketAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<WhitespaceBucketAggregate>(
                    "whitespace_bucket_hourly",
                    "Metrics",
                    "1 hour",
                    "Timestamp"
                ).WithTimeBucketProperty("   ");
            });
        }
    }

    [Fact]
    public void WithTimeBucketProperty_Throws_When_PropertyName_Whitespace()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
        {
            using WhitespaceBucketContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Equal("propertyName", exception.ParamName);
    }

    #endregion
}

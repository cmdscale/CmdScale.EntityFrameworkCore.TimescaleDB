using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Tests that verify TimescaleModelValidator warns exactly once per structured continuous aggregate
/// whose bucket column stays unmapped, and stays silent when the bucket is designated, mapped, raw,
/// or absent entirely.
/// </summary>
public class TimescaleModelValidatorTests
{
    private const string BucketWarningFragment = "time_bucket";

    private sealed class CapturingLoggerProvider(List<string> warnings) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(warnings);

        public void Dispose() { }

        private sealed class CapturingLogger(List<string> warnings) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    warnings.Add(formatter(state, exception));
                }
            }
        }
    }

    private static List<string> RunValidationAndCapture<TContext>(Func<List<string>, TContext> factory)
        where TContext : DbContext
    {
        List<string> warnings = [];
        using TContext context = factory(warnings);
        _ = context.Model;
        return [.. warnings.Where(w => w.Contains("continuous aggregate", StringComparison.OrdinalIgnoreCase))];
    }

    #region Should_Warn_When_Bucket_Property_Mapped_Elsewhere

    private class UndesignatedRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class UndesignatedAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class UndesignatedContext(List<string> warnings) : DbContext
    {
        public DbSet<UndesignatedRaw> Metrics => Set<UndesignatedRaw>();
        public DbSet<UndesignatedAggregate> HourlyMetrics => Set<UndesignatedAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(warnings))))
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UndesignatedRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("undesignated_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<UndesignatedAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<UndesignatedAggregate, UndesignatedRaw>(
                    "undesignated_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Warn_When_Bucket_Property_Mapped_Elsewhere()
    {
        // Act
        List<string> warnings = RunValidationAndCapture(w => new UndesignatedContext(w));

        // Assert
        string warning = Assert.Single(warnings);
        Assert.Contains("UndesignatedAggregate", warning);
        Assert.Contains(BucketWarningFragment, warning);
    }

    #endregion

    #region Should_Not_Warn_When_Bucket_Property_Designated

    private class DesignatedRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DesignatedAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class DesignatedContext(List<string> warnings) : DbContext
    {
        public DbSet<DesignatedRaw> Metrics => Set<DesignatedRaw>();
        public DbSet<DesignatedAggregate> HourlyMetrics => Set<DesignatedAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(warnings))))
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DesignatedRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("designated_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<DesignatedAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<DesignatedAggregate, DesignatedRaw>(
                    "designated_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).WithTimeBucketProperty(x => x.Bucket)
                 .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Not_Warn_When_Bucket_Property_Designated()
    {
        // Act
        List<string> warnings = RunValidationAndCapture(w => new DesignatedContext(w));

        // Assert
        Assert.Empty(warnings);
    }

    #endregion

    #region Should_Not_Warn_When_Property_Maps_To_BucketColumn

    private class MappedRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MappedAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MappedContext(List<string> warnings) : DbContext
    {
        public DbSet<MappedRaw> Metrics => Set<MappedRaw>();
        public DbSet<MappedAggregate> HourlyMetrics => Set<MappedAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(warnings))))
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MappedRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("mapped_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MappedAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("time_bucket");
                entity.IsContinuousAggregate<MappedAggregate, MappedRaw>(
                    "mapped_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Not_Warn_When_Property_Maps_To_BucketColumn()
    {
        // Act
        List<string> warnings = RunValidationAndCapture(w => new MappedContext(w));

        // Assert
        Assert.Empty(warnings);
    }

    #endregion

    #region Should_Not_Warn_For_RawViewDefinition

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

    private class RawDefinitionContext(List<string> warnings) : DbContext
    {
        public DbSet<RawDefinitionRaw> Metrics => Set<RawDefinitionRaw>();
        public DbSet<RawDefinitionAggregate> HourlyMetrics => Set<RawDefinitionAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(warnings))))
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
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<RawDefinitionAggregate, RawDefinitionRaw>(
                    "raw_definition_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
                entity.HasAnnotation(
                    ContinuousAggregateAnnotations.ViewDefinition,
                    "SELECT time_bucket('1 hour', \"timestamp\") AS time_bucket, AVG(\"value\") AS avg_value FROM \"raw_definition_raw\" GROUP BY 1");
            });
        }
    }

    [Fact]
    public void Should_Not_Warn_For_RawViewDefinition()
    {
        // Act
        List<string> warnings = RunValidationAndCapture(w => new RawDefinitionContext(w));

        // Assert
        Assert.Empty(warnings);
    }

    #endregion

    #region Should_Not_Warn_For_Plain_Hypertable

    private class PlainHypertable
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class PlainHypertableContext(List<string> warnings) : DbContext
    {
        public DbSet<PlainHypertable> Metrics => Set<PlainHypertable>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(warnings))))
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainHypertable>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("plain_hypertable");
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Not_Warn_For_Plain_Hypertable()
    {
        // Act
        List<string> warnings = RunValidationAndCapture(w => new PlainHypertableContext(w));

        // Assert
        Assert.Empty(warnings);
    }

    #endregion

    #region Should_Warn_Per_Offending_Aggregate

    private class MultiRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class MultiAggregateOne
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class MultiAggregateTwo
    {
        public DateTime Bucket { get; set; }
        public double MaxValue { get; set; }
    }

    private class MultiContext(List<string> warnings) : DbContext
    {
        public DbSet<MultiRaw> Metrics => Set<MultiRaw>();
        public DbSet<MultiAggregateOne> HourlyMetrics => Set<MultiAggregateOne>();
        public DbSet<MultiAggregateTwo> DailyMetrics => Set<MultiAggregateTwo>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(warnings))))
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MultiRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("multi_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<MultiAggregateOne>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<MultiAggregateOne, MultiRaw>(
                    "multi_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });

            modelBuilder.Entity<MultiAggregateTwo>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<MultiAggregateTwo, MultiRaw>(
                    "multi_daily",
                    "1 day",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.MaxValue, x => x.Value, EAggregateFunction.Max);
            });
        }
    }

    [Fact]
    public void Should_Warn_Per_Offending_Aggregate()
    {
        // Act
        List<string> warnings = RunValidationAndCapture(w => new MultiContext(w));

        // Assert
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("MultiAggregateOne", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("MultiAggregateTwo", StringComparison.Ordinal));
    }

    #endregion
}

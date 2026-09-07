using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Diagnostics;

/// <summary>
/// Regression tests that the unmapped-bucket model-validation warning reaches a LogTo sink and is
/// silenced by ConfigureWarnings.Ignore. Each scenario disables service-provider caching so EF's global
/// model cache does not skip validation for an already-validated model.
/// </summary>
public class TimeBucketColumnUnmappedWarningTests
{
    private const string ConnectionString = "Host=localhost;Database=dummy;Username=x;Password=x";

    private sealed class CapturingLoggerProvider(List<(LogLevel Level, EventId EventId, string Message)> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);

        public void Dispose() { }

        private sealed class CapturingLogger(List<(LogLevel, EventId, string)> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => sink.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    #region Should_Reach_LogTo_Sink_For_Unmapped_Bucket

    private class LogToRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class LogToAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class LogToContext(List<string> sink) : DbContext
    {
        public DbSet<LogToRaw> Metrics => Set<LogToRaw>();
        public DbSet<LogToAggregate> HourlyMetrics => Set<LogToAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString)
                            .LogTo(sink.Add, LogLevel.Debug)
                            .EnableServiceProviderCaching(false)
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogToRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("logto_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<LogToAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<LogToAggregate, LogToRaw>(
                    "logto_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Reach_LogTo_Sink_For_Unmapped_Bucket()
    {
        // Arrange
        List<string> sink = [];
        using LogToContext context = new(sink);

        // Act
        _ = context.Model;

        // Assert
        Assert.Contains(sink, l => l.Contains("LogToAggregate", StringComparison.Ordinal)
            && l.Contains("cannot be queried through the entity", StringComparison.Ordinal));
    }

    #endregion

    #region Should_Silence_Unmapped_Bucket_When_Ignored

    private class IgnoreRaw
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class IgnoreAggregate
    {
        public DateTime Bucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class IgnoreContext(List<(LogLevel Level, EventId EventId, string Message)> entries) : DbContext
    {
        public DbSet<IgnoreRaw> Metrics => Set<IgnoreRaw>();
        public DbSet<IgnoreAggregate> HourlyMetrics => Set<IgnoreAggregate>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(ConnectionString)
                            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new CapturingLoggerProvider(entries)).SetMinimumLevel(LogLevel.Debug)))
                            .ConfigureWarnings(w => w.Ignore(TimescaleDbEventId.TimeBucketColumnUnmapped))
                            .EnableServiceProviderCaching(false)
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IgnoreRaw>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("ignore_raw");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<IgnoreAggregate>(entity =>
            {
                entity.HasNoKey();
                entity.Property(x => x.Bucket).HasColumnName("bucket");
                entity.IsContinuousAggregate<IgnoreAggregate, IgnoreRaw>(
                    "ignore_hourly",
                    "1 hour",
                    x => x.Timestamp
                ).AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Silence_Unmapped_Bucket_When_Ignored()
    {
        // Arrange
        List<(LogLevel Level, EventId EventId, string Message)> entries = [];
        using IgnoreContext context = new(entries);

        // Act
        _ = context.Model;

        // Assert
        Assert.DoesNotContain(entries, e => e.EventId == TimescaleDbEventId.TimeBucketColumnUnmapped);
    }

    #endregion
}

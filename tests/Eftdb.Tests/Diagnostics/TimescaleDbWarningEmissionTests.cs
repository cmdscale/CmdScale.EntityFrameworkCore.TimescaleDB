using CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Diagnostics;

/// <summary>
/// Regression tests for the centralized warning emitter: TimescaleDB provider warnings must reach
/// every EF Core diagnostics sink (LogTo and UseLoggerFactory), honour ConfigureWarnings (Ignore/Throw),
/// and keep their published EventId values stable for downstream ConfigureWarnings callers.
/// </summary>
public class TimescaleDbWarningEmissionTests
{
    private const string ConnectionString = "Host=localhost;Database=dummy;Username=x;Password=x";
    private const string SkipWarningFragment = "Skipping Community Edition feature";
    private const string BucketWarningFragment = "cannot be queried through the entity";

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose() { }

        private sealed class CapturingLogger(List<(LogLevel, EventId, string)> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => sink.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    private class TestContext(DbContextOptions<TestContext> options) : DbContext(options);

    private static IReadOnlyList<MigrationCommand> GenerateCommunityOperation(DbContextOptions<TestContext> options)
    {
        using TestContext context = new(options);
        IMigrationsSqlGenerator generator = context.GetService<IMigrationsSqlGenerator>();

        AddRetentionPolicyOperation operation = new()
        {
            TableName = "readings",
            Schema = "public",
            DropAfter = "30 days",
            ScheduleInterval = "1 day",
        };

        return generator.Generate([operation]);
    }

    #region Should_Reach_LogTo_Sink_For_Community_Feature_Skipped

    [Fact]
    public void Should_Reach_LogTo_Sink_For_Community_Feature_Skipped()
    {
        // Arrange
        List<string> logToSink = [];
        DbContextOptions<TestContext> options = new DbContextOptionsBuilder<TestContext>()
            .UseNpgsql(ConnectionString)
            .UseTimescaleDb(o => o.UseApacheEdition())
            .LogTo(logToSink.Add, LogLevel.Debug)
            .EnableServiceProviderCaching(false)
            .Options;

        // Act
        GenerateCommunityOperation(options);

        // Assert
        Assert.Contains(logToSink, l => l.Contains(SkipWarningFragment, StringComparison.Ordinal));
    }

    #endregion

    #region Should_Reach_LoggerFactory_Sink_For_Community_Feature_Skipped

    [Fact]
    public void Should_Reach_LoggerFactory_Sink_For_Community_Feature_Skipped()
    {
        // Arrange
        CapturingLoggerProvider capture = new();
        using ILoggerFactory factory = LoggerFactory.Create(b => b.AddProvider(capture).SetMinimumLevel(LogLevel.Debug));
        DbContextOptions<TestContext> options = new DbContextOptionsBuilder<TestContext>()
            .UseNpgsql(ConnectionString)
            .UseTimescaleDb(o => o.UseApacheEdition())
            .UseLoggerFactory(factory)
            .EnableServiceProviderCaching(false)
            .Options;

        // Act
        GenerateCommunityOperation(options);

        // Assert
        Assert.Contains(capture.Entries, e => e.Level == LogLevel.Warning
            && e.EventId == TimescaleDbEventId.CommunityFeatureSkipped
            && e.Message.Contains(SkipWarningFragment, StringComparison.Ordinal));
    }

    #endregion

    #region Should_Silence_Community_Feature_Skipped_When_Ignored

    [Fact]
    public void Should_Silence_Community_Feature_Skipped_When_Ignored()
    {
        // Arrange
        CapturingLoggerProvider capture = new();
        using ILoggerFactory factory = LoggerFactory.Create(b => b.AddProvider(capture).SetMinimumLevel(LogLevel.Debug));
        DbContextOptions<TestContext> options = new DbContextOptionsBuilder<TestContext>()
            .UseNpgsql(ConnectionString)
            .UseTimescaleDb(o => o.UseApacheEdition())
            .UseLoggerFactory(factory)
            .ConfigureWarnings(w => w.Ignore(TimescaleDbEventId.CommunityFeatureSkipped))
            .EnableServiceProviderCaching(false)
            .Options;

        // Act
        GenerateCommunityOperation(options);

        // Assert
        Assert.DoesNotContain(capture.Entries, e => e.EventId == TimescaleDbEventId.CommunityFeatureSkipped);
    }

    #endregion

    #region Should_Throw_Community_Feature_Skipped_When_Configured

    [Fact]
    public void Should_Throw_Community_Feature_Skipped_When_Configured()
    {
        // Arrange
        DbContextOptions<TestContext> options = new DbContextOptionsBuilder<TestContext>()
            .UseNpgsql(ConnectionString)
            .UseTimescaleDb(o => o.UseApacheEdition())
            .ConfigureWarnings(w => w.Throw(TimescaleDbEventId.CommunityFeatureSkipped))
            .EnableServiceProviderCaching(false)
            .Options;

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => GenerateCommunityOperation(options));
        Assert.Contains(SkipWarningFragment, exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Should_Keep_Published_EventId_Values_Stable

    [Fact]
    public void Should_Keep_Published_EventId_Values_Stable()
    {
        // Act & Assert
        Assert.Equal(63000, TimescaleDbEventId.CommunityFeatureSkipped.Id);
        Assert.Equal(63001, TimescaleDbEventId.TimeBucketColumnUnmapped.Id);
    }

    #endregion
}

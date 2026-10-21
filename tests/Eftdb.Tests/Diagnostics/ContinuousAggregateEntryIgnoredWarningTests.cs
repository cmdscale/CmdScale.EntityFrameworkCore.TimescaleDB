using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Diagnostics;

/// <summary>
/// Tests the warning raised for continuous aggregate entries the model extractor leaves out of the view.
/// </summary>
public class ContinuousAggregateEntryIgnoredWarningTests
{
    private class IgnoredEvent
    {
        public DateTime OccurredAt { get; set; }
        public double Value { get; set; }
    }

    private class IgnoredAggregate
    {
        public DateTime Bucket { get; set; }
        public double Total { get; set; }
    }

    private class IgnoredContext(string entry, Action<WarningsConfigurationBuilder>? warnings = null) : DbContext
    {
        public List<string> Log { get; } = [];

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=dummy;Username=x;Password=x")
                             .LogTo(Log.Add)
                             .ConfigureWarnings(w => warnings?.Invoke(w))
                             .EnableServiceProviderCaching(false)
                             .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IgnoredEvent>(e =>
            {
                e.HasNoKey();
                e.ToTable("ignored_events");
                e.IsHypertable(x => x.OccurredAt);
            });
            modelBuilder.Entity<IgnoredAggregate>(e =>
            {
                e.HasNoKey();
                e.Property(x => x.Bucket).HasColumnName("time_bucket");
                e.IsContinuousAggregate("ignored_hourly", nameof(IgnoredEvent), "1 hour", nameof(IgnoredEvent.OccurredAt));
                e.HasAnnotation(ContinuousAggregateAnnotations.AggregateFunctions, new List<string> { entry });
            });
        }
    }

    #region Should_Warn_For_Ignored_Entry

    [Theory]
    [InlineData("Total:Sum:NoSuchColumn", "NoSuchColumn")]
    [InlineData("Total:Sum", "alias:Function:sourceColumn")]
    public void Should_Warn_For_Ignored_Entry(string entry, string expectedFragment)
    {
        // Arrange
        using IgnoredContext context = new(entry);

        // Act
        _ = context.Model;

        // Assert
        string warning = Assert.Single(context.Log, l => l.Contains(TimescaleDbEventId.ContinuousAggregateEntryIgnored.Name!));
        Assert.Contains(expectedFragment, warning);
    }

    #endregion

    #region Should_Not_Warn_For_Valid_Entry

    [Fact]
    public void Should_Not_Warn_For_Valid_Entry()
    {
        // Arrange
        using IgnoredContext context = new("Total:Sum:Value");

        // Act
        _ = context.Model;

        // Assert
        Assert.DoesNotContain(context.Log, l => l.Contains(TimescaleDbEventId.ContinuousAggregateEntryIgnored.Name!));
    }

    #endregion

    #region Should_Throw_When_Ignored_Entry_Warning_Is_An_Error

    [Fact]
    public void Should_Throw_When_Ignored_Entry_Warning_Is_An_Error()
    {
        // Arrange
        using IgnoredContext context = new("Total:Sum:NoSuchColumn", w => w.Throw(TimescaleDbEventId.ContinuousAggregateEntryIgnored));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => context.Model);
    }

    #endregion
}

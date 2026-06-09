using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregatePolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.RetentionPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Verifies that adding a naming convention (e.g. snake_case) on top of an existing migration produces a correct
/// diff: renamed objects are recognized as renames rather than drop-and-create, and policies that cascade away
/// when a continuous aggregate is recreated are re-added. These exercise the full orchestrator (the registered
/// <c>IMigrationsModelDiffer</c>), including EF Core's rename detection.
/// </summary>
public class MigrationDifferRenameTests : MigrationTestBase
{
    #region Should_Not_Recreate_Hypertable_When_Table_Renamed

    private class HtReading1
    {
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
    }

    private class HtInitialContext1 : DbContext
    {
        public DbSet<HtReading1> WeatherReadings => Set<HtReading1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HtReading1>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
            });
    }

    private class HtModifiedContext1 : DbContext
    {
        public DbSet<HtReading1> WeatherReadings => Set<HtReading1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention() // <-- Added on top of the existing migration
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HtReading1>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
            });
    }

    [Fact]
    public void Should_Not_Recreate_Hypertable_When_Table_Renamed()
    {
        // Arrange
        using HtInitialContext1 initial = new();
        using HtModifiedContext1 modified = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initial, modified);

        // Assert
        Assert.Contains(operations.OfType<RenameTableOperation>(), o => o.NewName == "weather_readings");
        Assert.Empty(operations.OfType<CreateHypertableOperation>());
        Assert.Empty(operations.OfType<AlterHypertableOperation>());
    }

    #endregion

    #region Should_Not_Alter_Compressed_Hypertable_When_Only_Renamed

    private class CompressedReading5
    {
        public DateTime Time { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    private class CompressedInitialContext5 : DbContext
    {
        public DbSet<CompressedReading5> DeviceMetrics => Set<CompressedReading5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressedReading5>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time)
                      .EnableCompression()
                      .WithChunkSkipping(x => x.Time)
                      .WithCompressionSegmentBy(x => x.DeviceId)
                      .WithCompressionOrderBy(s => [s.ByDescending(x => x.Time)]);
            });
    }

    private class CompressedModifiedContext5 : DbContext
    {
        public DbSet<CompressedReading5> DeviceMetrics => Set<CompressedReading5>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention() // <-- Added on top of the existing migration
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompressedReading5>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time)
                      .EnableCompression()
                      .WithChunkSkipping(x => x.Time)
                      .WithCompressionSegmentBy(x => x.DeviceId)
                      .WithCompressionOrderBy(s => [s.ByDescending(x => x.Time)]);
            });
    }

    [Fact]
    public void Should_Not_Alter_Compressed_Hypertable_When_Only_Renamed()
    {
        // Arrange
        using CompressedInitialContext5 initial = new();
        using CompressedModifiedContext5 modified = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initial, modified);

        // Assert
        Assert.Contains(operations.OfType<RenameTableOperation>(), o => o.NewName == "device_metrics");
        Assert.Contains(operations.OfType<RenameColumnOperation>(), o => o.NewName == "device_id");
        Assert.Empty(operations.OfType<CreateHypertableOperation>());
        Assert.Empty(operations.OfType<AlterHypertableOperation>());
    }

    #endregion

    #region Should_Not_Readd_ReorderPolicy_When_Table_Renamed

    private class ReorderReading2
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class ReorderInitialContext2 : DbContext
    {
        public DbSet<ReorderReading2> DeviceReadings => Set<ReorderReading2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReorderReading2>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
                entity.HasIndex(x => x.Time).HasDatabaseName("reorder_rename_idx");
                entity.WithReorderPolicy("reorder_rename_idx", null, "1 day", "00:00:00", 3, "00:05:00");
            });
    }

    private class ReorderModifiedContext2 : DbContext
    {
        public DbSet<ReorderReading2> DeviceReadings => Set<ReorderReading2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention() // <-- Added on top of the existing migration
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReorderReading2>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
                entity.HasIndex(x => x.Time).HasDatabaseName("reorder_rename_idx");
                entity.WithReorderPolicy("reorder_rename_idx", null, "1 day", "00:00:00", 3, "00:05:00");
            });
    }

    [Fact]
    public void Should_Not_Readd_ReorderPolicy_When_Table_Renamed()
    {
        // Arrange
        using ReorderInitialContext2 initial = new();
        using ReorderModifiedContext2 modified = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initial, modified);

        // Assert
        Assert.Contains(operations.OfType<RenameTableOperation>(), o => o.NewName == "device_readings");
        Assert.Empty(operations.OfType<AddReorderPolicyOperation>());
        Assert.Empty(operations.OfType<DropReorderPolicyOperation>());
    }

    #endregion

    #region Should_Not_Churn_RetentionPolicy_When_Table_Renamed

    private class RetentionReading3
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class RetentionInitialContext3 : DbContext
    {
        public DbSet<RetentionReading3> ApplicationLogs => Set<RetentionReading3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RetentionReading3>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
                entity.WithRetentionPolicy(dropAfter: "30 days", scheduleInterval: "1 day", maxRetries: 3, retryPeriod: "5 minutes");
            });
    }

    private class RetentionModifiedContext3 : DbContext
    {
        public DbSet<RetentionReading3> ApplicationLogs => Set<RetentionReading3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention() // <-- Added on top of the existing migration
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RetentionReading3>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
                entity.WithRetentionPolicy(dropAfter: "30 days", scheduleInterval: "1 day", maxRetries: 3, retryPeriod: "5 minutes");
            });
    }

    [Fact]
    public void Should_Not_Churn_RetentionPolicy_When_Table_Renamed()
    {
        // Arrange
        using RetentionInitialContext3 initial = new();
        using RetentionModifiedContext3 modified = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initial, modified);

        // Assert
        Assert.Contains(operations.OfType<RenameTableOperation>(), o => o.NewName == "application_logs");
        Assert.Empty(operations.OfType<AddRetentionPolicyOperation>());
        Assert.Empty(operations.OfType<DropRetentionPolicyOperation>());
    }

    #endregion

    #region Should_Readd_Policies_When_ContinuousAggregate_Recreated

    private class CaggMetric4
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class CaggAggregate4
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class CaggInitialContext4 : DbContext
    {
        public DbSet<CaggMetric4> SensorMetrics => Set<CaggMetric4>();
        public DbSet<CaggAggregate4> SensorAggregates => Set<CaggAggregate4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggMetric4>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<CaggAggregate4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CaggAggregate4, CaggMetric4>("sensor_hourly", "1 hour", x => x.Time)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "2 days", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.WithRetentionPolicy(dropAfter: "90 days", scheduleInterval: "1 day", maxRetries: 3, retryPeriod: "15 minutes");
            });
        }
    }

    private class CaggModifiedContext4 : DbContext
    {
        public DbSet<CaggMetric4> SensorMetrics => Set<CaggMetric4>();
        public DbSet<CaggAggregate4> SensorAggregates => Set<CaggAggregate4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseSnakeCaseNamingConvention() // <-- Added on top of the existing migration
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaggMetric4>(entity =>
            {
                entity.HasNoKey();
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<CaggAggregate4>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<CaggAggregate4, CaggMetric4>("sensor_hourly", "1 hour", x => x.Time)
                    .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg)
                    .WithRefreshPolicy(startOffset: "2 days", endOffset: "1 hour", scheduleInterval: "1 hour");
                entity.WithRetentionPolicy(dropAfter: "90 days", scheduleInterval: "1 day", maxRetries: 3, retryPeriod: "15 minutes");
            });
        }
    }

    [Fact]
    public void Should_Readd_Policies_When_ContinuousAggregate_Recreated()
    {
        // Arrange
        using CaggInitialContext4 initial = new();
        using CaggModifiedContext4 modified = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(initial, modified);

        // Assert
        Assert.Contains(operations.OfType<DropContinuousAggregateOperation>(), o => o.MaterializedViewName == "sensor_hourly");
        Assert.Contains(operations.OfType<CreateContinuousAggregateOperation>(), o => o.MaterializedViewName == "sensor_hourly");

        AddContinuousAggregatePolicyOperation refresh = Assert.Single(operations.OfType<AddContinuousAggregatePolicyOperation>());
        Assert.Equal("sensor_hourly", refresh.MaterializedViewName);

        AddRetentionPolicyOperation retention = Assert.Single(operations.OfType<AddRetentionPolicyOperation>());
        Assert.Equal("sensor_hourly", retention.TableName);

        // The cascade already removed these, so no explicit remove/drop should be emitted for the view.
        Assert.Empty(operations.OfType<RemoveContinuousAggregatePolicyOperation>());
        Assert.DoesNotContain(operations.OfType<DropRetentionPolicyOperation>(), o => o.TableName == "sensor_hourly");

        // Re-adds must be ordered after the recreate.
        int createIndex = IndexOf<CreateContinuousAggregateOperation>(operations);
        Assert.True(createIndex < IndexOf<AddContinuousAggregatePolicyOperation>(operations));
        Assert.True(createIndex < IndexOf<AddRetentionPolicyOperation>(operations));
    }

    private static int IndexOf<T>(IReadOnlyList<MigrationOperation> operations) where T : MigrationOperation
    {
        for (int i = 0; i < operations.Count; i++)
        {
            if (operations[i] is T)
            {
                return i;
            }
        }

        return -1;
    }

    #endregion
}

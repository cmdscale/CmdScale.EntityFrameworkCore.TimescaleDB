using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Query;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

public class FirstLastIntegrationTests : MigrationTestBase, IAsyncLifetime
{
    private static readonly DateTime BaseTime = new(2025, 1, 6, 10, 0, 0, DateTimeKind.Utc);

    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder(TimescaleImages.Community)
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    #region Should_Translate_First_And_Last_In_GroupBy

    private class FirstLastGroupByReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastGroupByContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastGroupByReading> Readings => Set<FirstLastGroupByReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastGroupByReading>(entity =>
            {
                entity.ToTable("fl_groupby_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Translate_First_And_Last_In_GroupBy()
    {
        // Arrange
        await using FirstLastGroupByContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_groupby_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(2)}, {30.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_groupby_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(0)}, {10.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_groupby_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(1)}, {20.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_groupby_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('b', {BaseTime.AddMinutes(5)}, {7.0})",
            TestContext.Current.CancellationToken);

        // Act
        var results = await context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => new
            {
                Sensor = g.Key,
                First = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Last = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp)))
            })
            .OrderBy(r => r.Sensor)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Equal("a", results[0].Sensor);
        Assert.Equal(10.0, results[0].First);
        Assert.Equal(30.0, results[0].Last);

        Assert.Equal("b", results[1].Sensor);
        Assert.Equal(7.0, results[1].First);
        Assert.Equal(7.0, results[1].Last);
    }

    #endregion

    #region Should_Translate_First_And_Last_With_DateTimeOffset_Time_Column

    private class FirstLastOffsetReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastOffsetContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastOffsetReading> Readings => Set<FirstLastOffsetReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastOffsetReading>(entity =>
            {
                entity.ToTable("fl_offset_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Translate_First_And_Last_With_DateTimeOffset_Time_Column()
    {
        // Arrange
        DateTimeOffset baseOffset = new(BaseTime);
        await using FirstLastOffsetContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_offset_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {baseOffset.AddMinutes(2)}, {30.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_offset_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {baseOffset.AddMinutes(0)}, {10.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_offset_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {baseOffset.AddMinutes(1)}, {20.0})",
            TestContext.Current.CancellationToken);

        // Act
        var result = await context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => new
            {
                First = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Last = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp)))
            })
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(10.0, result.First);
        Assert.Equal(30.0, result.Last);
    }

    #endregion

    #region Should_Return_Null_When_First_Or_Last_Value_Is_Null

    private class FirstLastNullableReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastNullableContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastNullableReading> Readings => Set<FirstLastNullableReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastNullableReading>(entity =>
            {
                entity.ToTable("fl_nullable_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Return_Null_When_First_Or_Last_Value_Is_Null()
    {
        // Arrange
        await using FirstLastNullableContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_nullable_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('x', {BaseTime.AddMinutes(0)}, NULL)",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_nullable_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('x', {BaseTime.AddMinutes(1)}, {5.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_nullable_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('x', {BaseTime.AddMinutes(2)}, {9.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_nullable_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('y', {BaseTime.AddMinutes(0)}, {4.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_nullable_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('y', {BaseTime.AddMinutes(1)}, {6.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_nullable_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('y', {BaseTime.AddMinutes(2)}, NULL)",
            TestContext.Current.CancellationToken);

        // Act
        var results = await context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => new
            {
                Sensor = g.Key,
                First = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Last = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp)))
            })
            .OrderBy(r => r.Sensor)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Null(results[0].First);
        Assert.Equal(9.0, results[0].Last);

        Assert.Equal(4.0, results[1].First);
        Assert.Null(results[1].Last);
    }

    #endregion

    #region Should_Return_Null_For_All_Null_Group

    private class FirstLastAllNullReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastAllNullContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastAllNullReading> Readings => Set<FirstLastAllNullReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastAllNullReading>(entity =>
            {
                entity.ToTable("fl_allnull_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Return_Null_For_All_Null_Group()
    {
        // Arrange
        await using FirstLastAllNullContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_allnull_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('z', {BaseTime.AddMinutes(0)}, NULL)",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_allnull_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('z', {BaseTime.AddMinutes(1)}, NULL)",
            TestContext.Current.CancellationToken);

        // Act
        var result = await context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => new
            {
                First = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Last = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp)))
            })
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.First);
        Assert.Null(result.Last);
    }

    #endregion

    #region Should_Apply_Where_Filter_Inside_Grouping

    private class FirstLastWhereReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastWhereContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastWhereReading> Readings => Set<FirstLastWhereReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastWhereReading>(entity =>
            {
                entity.ToTable("fl_where_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Apply_Where_Filter_Inside_Grouping()
    {
        // Arrange
        await using FirstLastWhereContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_where_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(0)}, {10.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_where_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(1)}, {20.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_where_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(2)}, {30.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_where_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('c', {BaseTime.AddMinutes(0)}, {1.0})",
            TestContext.Current.CancellationToken);

        // Act
        var results = await context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => new
            {
                Sensor = g.Key,
                First = EF.Functions.First(g.Where(r => r.Value > 10).Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Last = EF.Functions.Last(g.Where(r => r.Value > 10).Select(r => ValueTuple.Create(r.Value, r.Timestamp)))
            })
            .OrderBy(r => r.Sensor)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Equal(20.0, results[0].First);
        Assert.Equal(30.0, results[0].Last);

        Assert.Null(results[1].First);
        Assert.Null(results[1].Last);
    }

    #endregion

    #region Should_Translate_First_And_Last_With_TimeBucket_GroupBy

    private class FirstLastBucketReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastBucketContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastBucketReading> Readings => Set<FirstLastBucketReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastBucketReading>(entity =>
            {
                entity.ToTable("fl_bucket_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Translate_First_And_Last_With_TimeBucket_GroupBy()
    {
        // Arrange
        await using FirstLastBucketContext context = new(_connectionString!);
        await CreateDatabaseViaMigrationAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_bucket_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(3)}, {30.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_bucket_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(0)}, {10.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_bucket_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(1)}, {20.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_bucket_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(7)}, {70.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_bucket_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(5)}, {50.0})",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"fl_bucket_readings\" (\"Sensor\", \"Timestamp\", \"Value\") VALUES ('a', {BaseTime.AddMinutes(6)}, {60.0})",
            TestContext.Current.CancellationToken);

        // Act
        var results = await context.Readings
            .GroupBy(r => EF.Functions.TimeBucket(TimeSpan.FromMinutes(5), r.Timestamp))
            .Select(g => new
            {
                Bucket = g.Key,
                First = EF.Functions.First(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Last = EF.Functions.Last(g.Select(r => ValueTuple.Create(r.Value, r.Timestamp))),
                Count = g.Count()
            })
            .OrderBy(r => r.Bucket)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Equal(10.0, results[0].First);
        Assert.Equal(30.0, results[0].Last);
        Assert.Equal(3, results[0].Count);

        Assert.Equal(50.0, results[1].First);
        Assert.Equal(70.0, results[1].Last);
        Assert.Equal(3, results[1].Count);
    }

    #endregion

    #region Should_Refuse_Distinct_Inside_Grouping

    private class FirstLastDistinctReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastDistinctContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastDistinctReading> Readings => Set<FirstLastDistinctReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastDistinctReading>(entity =>
            {
                entity.ToTable("fl_distinct_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Refuse_Distinct_Inside_Grouping()
    {
        // Arrange
        await using FirstLastDistinctContext context = new(_connectionString!);

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => EF.Functions.First(g.Distinct().Select(r => ValueTuple.Create(r.Value, r.Timestamp))))
            .ToListAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("EF.Functions.First does not support Distinct or OrderBy inside the grouping", exception.Message);
    }

    #endregion

    #region Should_Refuse_OrderBy_Inside_Grouping

    private class FirstLastOrderByReading
    {
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastOrderByContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastOrderByReading> Readings => Set<FirstLastOrderByReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastOrderByReading>(entity =>
            {
                entity.ToTable("fl_orderby_readings");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public async Task Should_Refuse_OrderBy_Inside_Grouping()
    {
        // Arrange
        await using FirstLastOrderByContext context = new(_connectionString!);

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => EF.Functions.Last(g.OrderBy(r => r.Value).Select(r => ValueTuple.Create(r.Value, r.Timestamp))))
            .ToListAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("EF.Functions.Last does not support Distinct or OrderBy inside the grouping", exception.Message);
    }

    #endregion

    #region Should_Refuse_OrderBy_Inside_Grouping_On_Keyed_Entity

    private class FirstLastKeyedOrderByReading
    {
        public int Id { get; set; }
        public string Sensor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double? Value { get; set; }
    }

    private class FirstLastKeyedOrderByContext(string connectionString) : DbContext
    {
        public DbSet<FirstLastKeyedOrderByReading> Readings => Set<FirstLastKeyedOrderByReading>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connectionString).UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstLastKeyedOrderByReading>(entity =>
            {
                entity.ToTable("fl_keyed_orderby_readings");
                entity.HasKey(x => x.Id);
            });
        }
    }

    [Fact]
    public async Task Should_Refuse_OrderBy_Inside_Grouping_On_Keyed_Entity()
    {
        // Arrange
        await using FirstLastKeyedOrderByContext context = new(_connectionString!);

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Readings
            .GroupBy(r => r.Sensor)
            .Select(g => EF.Functions.Last(g.OrderBy(r => r.Value).Select(r => ValueTuple.Create(r.Value, r.Timestamp))))
            .ToListAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("EF.Functions.Last does not support Distinct or OrderBy inside the grouping", exception.Message);
    }

    #endregion
}

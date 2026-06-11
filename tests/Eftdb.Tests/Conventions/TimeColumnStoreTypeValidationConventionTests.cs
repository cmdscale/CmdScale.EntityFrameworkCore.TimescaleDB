using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NodaTime;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Conventions;

/// <summary>
/// Tests that verify TimeColumnStoreTypeValidationConvention validates the resolved PostgreSQL store
/// type of hypertable and continuous aggregate time columns during model finalization.
/// </summary>
public class TimeColumnStoreTypeValidationConventionTests
{
    private static IModel GetModel(DbContext context)
    {
        return context.GetService<IDesignTimeModel>().Model;
    }

    #region Should_Allow_Custom_TimeColumn_Mapped_To_Timestamp

    // Stands in for a custom time type (e.g. NodaTime Instant) mapped to a timestamp store type via a
    // value converter. The .NET type is unknown to the library; validity comes from the store mapping.
    private readonly struct CustomInstant(DateTime utcDateTime)
    {
        public DateTime UtcDateTime { get; } = utcDateTime;
    }

    private class CustomTimeEntity
    {
        public CustomInstant Time { get; set; }
        public double Value { get; set; }
    }

    private class CustomTimeContext : DbContext
    {
        public DbSet<CustomTimeEntity> Metrics => Set<CustomTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_custom_time");
                entity.Property(x => x.Time)
                      .HasConversion(v => v.UtcDateTime, v => new CustomInstant(v));
                entity.IsHypertable(x => x.Time);
            });
        }
    }

    [Fact]
    public void Should_Allow_Custom_TimeColumn_Mapped_To_Timestamp()
    {
        using CustomTimeContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(CustomTimeEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Throw_When_Hypertable_TimeColumn_Maps_To_Boolean

    private class BooleanTimeEntity
    {
        public bool Flag { get; set; }
        public double Value { get; set; }
    }

    private class BooleanTimeContext : DbContext
    {
        public DbSet<BooleanTimeEntity> Metrics => Set<BooleanTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BooleanTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_boolean_time");
                entity.IsHypertable(x => x.Flag);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Hypertable_TimeColumn_Maps_To_Boolean()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using BooleanTimeContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("not a valid TimescaleDB time dimension", exception.Message);
        Assert.Contains("Flag", exception.Message);
    }

    #endregion

    #region Should_Throw_When_Hypertable_TimeColumn_Maps_To_Guid

    private class GuidTimeEntity
    {
        public Guid EventId { get; set; }
        public double Value { get; set; }
    }

    private class GuidTimeContext : DbContext
    {
        public DbSet<GuidTimeEntity> Metrics => Set<GuidTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GuidTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_guid_time");
                entity.IsHypertable(x => x.EventId);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_Hypertable_TimeColumn_Maps_To_Guid()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using GuidTimeContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("not a valid TimescaleDB time dimension", exception.Message);
        Assert.Contains("EventId", exception.Message);
    }

    #endregion

    #region Should_Allow_ContinuousAggregate_With_Valid_Source_TimeColumn

    private class ValidCaggSourceEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ValidCaggAggregateEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ValidCaggContext : DbContext
    {
        public DbSet<ValidCaggSourceEntity> Metrics => Set<ValidCaggSourceEntity>();
        public DbSet<ValidCaggAggregateEntity> HourlyMetrics => Set<ValidCaggAggregateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ValidCaggSourceEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_cagg_source");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ValidCaggAggregateEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ValidCaggAggregateEntity, ValidCaggSourceEntity>(
                    "validation_cagg_view",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Allow_ContinuousAggregate_With_Valid_Source_TimeColumn()
    {
        using ValidCaggContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ValidCaggAggregateEntity))!;

        Assert.Equal("validation_cagg_view", entityType.FindAnnotation(ContinuousAggregateAnnotations.MaterializedViewName)?.Value);
    }

    #endregion

    #region Should_Throw_When_ContinuousAggregate_SourceColumn_Maps_To_Invalid_Type

    private class InvalidCaggSourceEntity
    {
        public DateTime Timestamp { get; set; }
        public bool Flag { get; set; }
        public double Value { get; set; }
    }

    private class InvalidCaggAggregateEntity
    {
        public bool TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class InvalidCaggContext : DbContext
    {
        public DbSet<InvalidCaggSourceEntity> Metrics => Set<InvalidCaggSourceEntity>();
        public DbSet<InvalidCaggAggregateEntity> HourlyMetrics => Set<InvalidCaggAggregateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvalidCaggSourceEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_invalid_cagg_source");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<InvalidCaggAggregateEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<InvalidCaggAggregateEntity, InvalidCaggSourceEntity, bool>(
                    "validation_invalid_cagg_view",
                    "1 hour",
                    x => x.Flag)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_ContinuousAggregate_SourceColumn_Maps_To_Invalid_Type()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using InvalidCaggContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("not a valid TimescaleDB time dimension", exception.Message);
        Assert.Contains("continuous aggregate", exception.Message);
    }

    #endregion

    #region Should_Allow_All_Relevant_NodaTime_Time_Column_Types

    // NodaTime date/time types whose Npgsql store mapping is a valid TimescaleDB time dimension.
    private class NodaInstantEntity
    {
        public Instant Time { get; set; }
        public double Value { get; set; }
    }

    private class NodaLocalDateTimeEntity
    {
        public LocalDateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class NodaLocalDateEntity
    {
        public LocalDate Time { get; set; }
        public double Value { get; set; }
    }

    private class NodaZonedDateTimeEntity
    {
        public ZonedDateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class NodaOffsetDateTimeEntity
    {
        public OffsetDateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class NodaTimeValidContext : DbContext
    {
        public DbSet<NodaInstantEntity> Instants => Set<NodaInstantEntity>();
        public DbSet<NodaLocalDateTimeEntity> LocalDateTimes => Set<NodaLocalDateTimeEntity>();
        public DbSet<NodaLocalDateEntity> LocalDates => Set<NodaLocalDateEntity>();
        public DbSet<NodaZonedDateTimeEntity> ZonedDateTimes => Set<NodaZonedDateTimeEntity>();
        public DbSet<NodaOffsetDateTimeEntity> OffsetDateTimes => Set<NodaOffsetDateTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test", o => o.UseNodaTime())
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodaInstantEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_nodatime_instant");
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<NodaLocalDateTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_nodatime_localdatetime");
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<NodaLocalDateEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_nodatime_localdate");
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<NodaZonedDateTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_nodatime_zoneddatetime");
                entity.IsHypertable(x => x.Time);
            });

            modelBuilder.Entity<NodaOffsetDateTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_nodatime_offsetdatetime");
                entity.IsHypertable(x => x.Time);
            });
        }
    }

    [Theory]
    [InlineData(typeof(NodaInstantEntity), "timestamp with time zone")]
    [InlineData(typeof(NodaLocalDateTimeEntity), "timestamp without time zone")]
    [InlineData(typeof(NodaLocalDateEntity), "date")]
    [InlineData(typeof(NodaZonedDateTimeEntity), "timestamp with time zone")]
    [InlineData(typeof(NodaOffsetDateTimeEntity), "timestamp with time zone")]
    public void Should_Allow_All_Relevant_NodaTime_Time_Column_Types(Type entityClrType, string expectedStoreType)
    {
        using NodaTimeValidContext context = new();

        // Building the model runs the validation convention; a non-time store type would throw here.
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(entityClrType)!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal(expectedStoreType, entityType.FindProperty("Time")!.GetColumnType());
    }

    #endregion

    #region Should_Throw_For_NonDimension_NodaTime_Types

    // NodaTime temporal types whose Npgsql store mapping is NOT a valid TimescaleDB time dimension
    // (per the Npgsql NodaTime mapping table). Every one must be rejected by the validation convention.
    [Hypertable("Time")]
    private class NodaLocalTimeEntity
    {
        public LocalTime Time { get; set; }
        public double Value { get; set; }
    }

    [Hypertable("Time")]
    private class NodaOffsetTimeEntity
    {
        public OffsetTime Time { get; set; }
        public double Value { get; set; }
    }

    [Hypertable("Time")]
    private class NodaPeriodEntity
    {
        public Period Time { get; set; } = null!;
        public double Value { get; set; }
    }

    [Hypertable("Time")]
    private class NodaDurationEntity
    {
        public Duration Time { get; set; }
        public double Value { get; set; }
    }

    [Hypertable("Time")]
    private class NodaIntervalEntity
    {
        public Interval Time { get; set; }
        public double Value { get; set; }
    }

    [Hypertable("Time")]
    private class NodaDateIntervalEntity
    {
        public DateInterval Time { get; set; } = null!;
        public double Value { get; set; }
    }

    // A single generic context validates each type in isolation: the convention throws during model
    // finalization, so each NodaTime type needs its own one-entity model.
    private class NodaHypertableContext<TEntity> : DbContext where TEntity : class
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test", o => o.UseNodaTime())
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_" + typeof(TEntity).Name.ToLowerInvariant());
            });
    }

    [Theory]
    [InlineData(typeof(NodaLocalTimeEntity))]    // time without time zone
    [InlineData(typeof(NodaOffsetTimeEntity))]   // time with time zone
    [InlineData(typeof(NodaPeriodEntity))]       // interval
    [InlineData(typeof(NodaDurationEntity))]     // interval
    [InlineData(typeof(NodaIntervalEntity))]     // tstzrange
    [InlineData(typeof(NodaDateIntervalEntity))] // daterange
    public void Should_Throw_For_NonDimension_NodaTime_Types(Type entityClrType)
    {
        Type contextType = typeof(NodaHypertableContext<>).MakeGenericType(entityClrType);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using DbContext context = (DbContext)Activator.CreateInstance(contextType)!;
            _ = GetModel(context);
        });

        Assert.Contains("not a valid TimescaleDB time dimension", exception.Message);
    }

    #endregion
}

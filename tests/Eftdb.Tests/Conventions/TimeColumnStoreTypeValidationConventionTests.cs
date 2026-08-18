using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NodaTime;
using System.ComponentModel.DataAnnotations.Schema;

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

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(entityClrType)!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
        Assert.Equal(expectedStoreType, entityType.FindProperty("Time")!.GetColumnType());
    }

    #endregion

    #region Should_Throw_For_NonDimension_NodaTime_Types

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
    [InlineData(typeof(NodaLocalTimeEntity))]
    [InlineData(typeof(NodaOffsetTimeEntity))]
    [InlineData(typeof(NodaPeriodEntity))]
    [InlineData(typeof(NodaDurationEntity))]
    [InlineData(typeof(NodaIntervalEntity))]
    [InlineData(typeof(NodaDateIntervalEntity))]
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

    #region Should_Not_Throw_When_Hypertable_TimeColumn_Annotation_Missing

    private class MissingTimeColumnEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class MissingTimeColumnContext : DbContext
    {
        public DbSet<MissingTimeColumnEntity> Metrics => Set<MissingTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MissingTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_missing_time_column");
                entity.HasAnnotation(HypertableAnnotations.IsHypertable, true);
            });
        }
    }

    [Fact]
    public void Should_Not_Throw_When_Hypertable_TimeColumn_Annotation_Missing()
    {
        using MissingTimeColumnContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(MissingTimeColumnEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Not_Throw_When_Hypertable_TimeColumn_Cannot_Be_Resolved

    private class UnresolvableTimeColumnEntity
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    private class UnresolvableTimeColumnContext : DbContext
    {
        public DbSet<UnresolvableTimeColumnEntity> Metrics => Set<UnresolvableTimeColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnresolvableTimeColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_unresolvable_time_column");
                entity.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                entity.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "does_not_exist");
            });
        }
    }

    [Fact]
    public void Should_Not_Throw_When_Hypertable_TimeColumn_Cannot_Be_Resolved()
    {
        using UnresolvableTimeColumnContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(UnresolvableTimeColumnEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Resolve_Hypertable_TimeColumn_By_Column_Name

    private class ColumnNamedTimeEntity
    {
        public DateTime Moment { get; set; }
        public double Value { get; set; }
    }

    private class ColumnNamedTimeContext : DbContext
    {
        public DbSet<ColumnNamedTimeEntity> Metrics => Set<ColumnNamedTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ColumnNamedTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_column_named_time");
                entity.Property(x => x.Moment).HasColumnName("event_ts");
                entity.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                entity.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "event_ts");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Hypertable_TimeColumn_By_Column_Name()
    {
        using ColumnNamedTimeContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ColumnNamedTimeEntity))!;

        Assert.Equal("event_ts", entityType.FindProperty("Moment")!.GetColumnName(StoreObjectIdentifier.Table("validation_column_named_time", null)));
    }

    #endregion

    #region Should_Resolve_Hypertable_TimeColumn_By_Column_Name_On_View

    private class ViewColumnNamedTimeEntity
    {
        public DateTime Moment { get; set; }
        public double Value { get; set; }
    }

    private class ViewColumnNamedTimeContext : DbContext
    {
        public DbSet<ViewColumnNamedTimeEntity> Metrics => Set<ViewColumnNamedTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewColumnNamedTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("validation_view_column_named_time");
                entity.Property(x => x.Moment).HasColumnName("event_ts");
                entity.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                entity.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "event_ts");
            });
        }
    }

    [Fact]
    public void Should_Resolve_Hypertable_TimeColumn_By_Column_Name_On_View()
    {
        using ViewColumnNamedTimeContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ViewColumnNamedTimeEntity))!;

        Assert.Equal("validation_view_column_named_time", entityType.GetViewName());
    }

    #endregion

    #region Should_Resolve_ContinuousAggregate_Parent_By_Table_Name

    private class ParentByTableNameSourceEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class ParentByTableNameAggregateEntity
    {
        public DateTime TimeBucket { get; set; }
        public double AvgValue { get; set; }
    }

    private class ParentByTableNameContext : DbContext
    {
        public DbSet<ParentByTableNameSourceEntity> Metrics => Set<ParentByTableNameSourceEntity>();
        public DbSet<ParentByTableNameAggregateEntity> HourlyMetrics => Set<ParentByTableNameAggregateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParentByTableNameSourceEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_parent_by_table_name");
                entity.IsHypertable(x => x.Timestamp);
            });

            modelBuilder.Entity<ParentByTableNameAggregateEntity>(entity =>
            {
                entity.HasNoKey();
                entity.IsContinuousAggregate<ParentByTableNameAggregateEntity, ParentByTableNameSourceEntity>(
                    "validation_parent_by_table_name_view",
                    "1 hour",
                    x => x.Timestamp)
                .AddAggregateFunction(x => x.AvgValue, x => x.Value, EAggregateFunction.Avg);

                entity.HasAnnotation(ContinuousAggregateAnnotations.ParentName, "validation_parent_by_table_name");
            });
        }
    }

    [Fact]
    public void Should_Resolve_ContinuousAggregate_Parent_By_Table_Name()
    {
        using ParentByTableNameContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ParentByTableNameAggregateEntity))!;

        Assert.Equal("validation_parent_by_table_name", entityType.FindAnnotation(ContinuousAggregateAnnotations.ParentName)?.Value);
    }

    #endregion

    #region Should_Not_Throw_When_Hypertable_Has_No_Table_Or_View

    private class NoStoreObjectEntity
    {
        public DateTime Moment { get; set; }
        public double Value { get; set; }
    }

    private class NoStoreObjectContext : DbContext
    {
        public DbSet<NoStoreObjectEntity> Metrics => Set<NoStoreObjectEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoStoreObjectEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable((string?)null);
                entity.HasAnnotation(HypertableAnnotations.IsHypertable, true);
                entity.HasAnnotation(HypertableAnnotations.HypertableTimeColumn, "does_not_exist");
            });
        }
    }

    [Fact]
    public void Should_Not_Throw_When_Hypertable_Has_No_Table_Or_View()
    {
        using NoStoreObjectContext context = new();

        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(NoStoreObjectEntity))!;

        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region EnsureValidTimeColumn store-type resolution (direct)

    [Fact]
    public void EnsureValidTimeColumn_Falls_Back_To_Mapping_Store_Type_When_Column_Type_Blank()
    {
        TimeColumnStoreTypeValidationConvention.EnsureValidTimeColumn("", "timestamp with time zone", "hypertable 'Probe'", "Time");
    }

    [Fact]
    public void EnsureValidTimeColumn_Does_Not_Throw_When_Store_Type_Undeterminable()
    {
        TimeColumnStoreTypeValidationConvention.EnsureValidTimeColumn(null, null, "hypertable 'Probe'", "Time");
    }

    [Fact]
    public void EnsureValidTimeColumn_Throws_When_Resolved_Store_Type_Invalid()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            TimeColumnStoreTypeValidationConvention.EnsureValidTimeColumn(null, "boolean", "hypertable 'Probe'", "Time"));

        Assert.Contains("not a valid TimescaleDB time dimension", exception.Message);
        Assert.Contains("boolean", exception.Message);
    }

    #endregion

    // ── Complex-type support ──

    #region Should_Allow_ComplexType_TimeColumn_With_Valid_DateTime_Store_Type

    [ComplexType]
    private class ValidComplexMeta
    {
        public DateTime Timestamp { get; set; }
    }

    private class ValidComplexTimeEntity
    {
        public double Value { get; set; }
        public ValidComplexMeta Meta { get; set; } = new();
    }

    private class ValidComplexTimeContext : DbContext
    {
        public DbSet<ValidComplexTimeEntity> Metrics => Set<ValidComplexTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ValidComplexTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_complex_valid_time");
                entity.IsHypertable<ValidComplexTimeEntity, DateTime>(x => x.Meta.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Allow_ComplexType_TimeColumn_With_Valid_DateTime_Store_Type()
    {
        // Arrange & Act
        using ValidComplexTimeContext context = new();
        IModel model = GetModel(context);
        IEntityType entityType = model.FindEntityType(typeof(ValidComplexTimeEntity))!;

        // Assert
        Assert.Equal(true, entityType.FindAnnotation(HypertableAnnotations.IsHypertable)?.Value);
    }

    #endregion

    #region Should_Throw_When_ComplexType_TimeColumn_Has_Invalid_Store_Type

    [ComplexType]
    private class InvalidComplexMeta
    {
        public string Tag { get; set; } = string.Empty;
    }

    private class InvalidComplexTimeEntity
    {
        public double Value { get; set; }
        public InvalidComplexMeta Meta { get; set; } = new();
    }

    private class InvalidComplexTimeContext : DbContext
    {
        public DbSet<InvalidComplexTimeEntity> Metrics => Set<InvalidComplexTimeEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InvalidComplexTimeEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("validation_complex_invalid_time");
                entity.IsHypertable<InvalidComplexTimeEntity, string>(x => x.Meta.Tag);
            });
        }
    }

    [Fact]
    public void Should_Throw_When_ComplexType_TimeColumn_Has_Invalid_Store_Type()
    {
        // Arrange & Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using InvalidComplexTimeContext context = new();
            IModel model = GetModel(context);
        });

        Assert.Contains("not a valid TimescaleDB time dimension", exception.Message);
    }

    #endregion
}

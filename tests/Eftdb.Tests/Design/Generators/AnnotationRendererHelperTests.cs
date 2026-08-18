using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

public class AnnotationRendererHelperTests
{
    private sealed record StubAnnotation(string Name, object? Value) : IAnnotation;

    private static Dictionary<string, IAnnotation> Annotations(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => (IAnnotation)new StubAnnotation(p.Key, p.Value));

    private static IEntityType GetEntityType<T>(DbContext context)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!;

    #region Find_Returns_Annotation_When_Key_Exists

    [Fact]
    public void Find_Returns_Annotation_When_Key_Exists()
    {
        Dictionary<string, IAnnotation> annotations = Annotations(("some:Key", "hello"));

        IAnnotation? result = AnnotationRendererHelper.Find(annotations, "some:Key");

        Assert.NotNull(result);
        Assert.Equal("hello", result.Value);
    }

    #endregion

    #region Find_Returns_Null_When_Key_Missing

    [Fact]
    public void Find_Returns_Null_When_Key_Missing()
    {
        Dictionary<string, IAnnotation> annotations = Annotations();

        IAnnotation? result = AnnotationRendererHelper.Find(annotations, "missing:Key");

        Assert.Null(result);
    }

    #endregion

    #region GetString_Returns_String_Value

    [Fact]
    public void GetString_Returns_String_Value()
    {
        Dictionary<string, IAnnotation> annotations = Annotations(("some:Key", "myValue"));

        string? result = AnnotationRendererHelper.GetString(annotations, "some:Key");

        Assert.Equal("myValue", result);
    }

    #endregion

    #region GetString_Returns_Null_When_Value_Is_Not_String

    [Fact]
    public void GetString_Returns_Null_When_Value_Is_Not_String()
    {
        Dictionary<string, IAnnotation> annotations = Annotations(("some:Key", 42));

        string? result = AnnotationRendererHelper.GetString(annotations, "some:Key");

        Assert.Null(result);
    }

    #endregion

    #region GetString_Returns_Null_When_Key_Missing

    [Fact]
    public void GetString_Returns_Null_When_Key_Missing()
    {
        Dictionary<string, IAnnotation> annotations = Annotations();

        string? result = AnnotationRendererHelper.GetString(annotations, "missing:Key");

        Assert.Null(result);
    }

    #endregion

    #region SplitColumns_Returns_Empty_For_Null

    [Fact]
    public void SplitColumns_Returns_Empty_For_Null()
    {
        string[] result = AnnotationRendererHelper.SplitColumns(null);

        Assert.Empty(result);
    }

    #endregion

    #region SplitColumns_Returns_Empty_For_Whitespace

    [Fact]
    public void SplitColumns_Returns_Empty_For_Whitespace()
    {
        string[] result = AnnotationRendererHelper.SplitColumns("   ");

        Assert.Empty(result);
    }

    #endregion

    #region SplitColumns_Returns_Single_Entry

    [Fact]
    public void SplitColumns_Returns_Single_Entry()
    {
        string[] result = AnnotationRendererHelper.SplitColumns("Timestamp");

        Assert.Equal("Timestamp", Assert.Single(result));
    }

    #endregion

    #region SplitColumns_Returns_Multiple_Trimmed_Entries

    [Fact]
    public void SplitColumns_Returns_Multiple_Trimmed_Entries()
    {
        string[] result = AnnotationRendererHelper.SplitColumns(" DeviceId , TenantId ");

        Assert.Equal(2, result.Length);
        Assert.Equal("DeviceId", result[0]);
        Assert.Equal("TenantId", result[1]);
    }

    #endregion

    #region Consume_Removes_Specified_Keys

    [Fact]
    public void Consume_Removes_Specified_Keys()
    {
        Dictionary<string, IAnnotation> annotations = Annotations(
            ("key:A", "a"),
            ("key:B", "b"),
            ("key:C", "c"));

        AnnotationRendererHelper.Consume(annotations, "key:A", "key:C");

        Assert.False(annotations.ContainsKey("key:A"));
        Assert.True(annotations.ContainsKey("key:B"));
        Assert.False(annotations.ContainsKey("key:C"));
    }

    #endregion

    #region Consume_Does_Not_Throw_For_Missing_Keys

    [Fact]
    public void Consume_Does_Not_Throw_For_Missing_Keys()
    {
        Dictionary<string, IAnnotation> annotations = Annotations(("key:A", "a"));

        AnnotationRendererHelper.Consume(annotations, "key:A", "key:NotPresent");

        Assert.Empty(annotations);
    }

    #endregion

    #region ResolvePropertyName_Maps_Column_Name_To_Property_Name

    private class ColumnMappingEntity
    {
        public DateTime EventTime { get; set; }
        public double Value { get; set; }
    }

    private class ColumnMappingContext : DbContext
    {
        public DbSet<ColumnMappingEntity> Items => Set<ColumnMappingEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ColumnMappingEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("column_mapping_items");
                entity.Property(x => x.EventTime).HasColumnName("event_time");
            });
        }
    }

    [Fact]
    public void ResolvePropertyName_Maps_Column_Name_To_Property_Name()
    {
        using ColumnMappingContext context = new();
        IEntityType entityType = GetEntityType<ColumnMappingEntity>(context);

        string resolved = AnnotationRendererHelper.ResolvePropertyName(entityType, "event_time");

        Assert.Equal("EventTime", resolved);
    }

    #endregion

    #region ResolvePropertyName_Returns_Raw_When_Not_Found

    private class UnmappedColumnEntity
    {
        public DateTime Timestamp { get; set; }
    }

    private class UnmappedColumnContext : DbContext
    {
        public DbSet<UnmappedColumnEntity> Items => Set<UnmappedColumnEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnmappedColumnEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("unmapped_items");
            });
        }
    }

    [Fact]
    public void ResolvePropertyName_Returns_Raw_When_Not_Found()
    {
        using UnmappedColumnContext context = new();
        IEntityType entityType = GetEntityType<UnmappedColumnEntity>(context);

        string resolved = AnnotationRendererHelper.ResolvePropertyName(entityType, "nonexistent_column");

        Assert.Equal("nonexistent_column", resolved);
    }

    #endregion

    #region TryResolvePropertyName_Returns_True_On_Match

    private class TryResolveMatchEntity
    {
        public DateTime CreatedAt { get; set; }
    }

    private class TryResolveMatchContext : DbContext
    {
        public DbSet<TryResolveMatchEntity> Items => Set<TryResolveMatchEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TryResolveMatchEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("try_resolve_match_items");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            });
        }
    }

    [Fact]
    public void TryResolvePropertyName_Returns_True_On_Match()
    {
        using TryResolveMatchContext context = new();
        IEntityType entityType = GetEntityType<TryResolveMatchEntity>(context);

        bool found = AnnotationRendererHelper.TryResolvePropertyName(entityType, "created_at", out string propertyName);

        Assert.True(found);
        Assert.Equal("CreatedAt", propertyName);
    }

    #endregion

    #region TryResolvePropertyName_Returns_False_When_Not_Found

    private class TryResolveNoMatchEntity
    {
        public DateTime Timestamp { get; set; }
    }

    private class TryResolveNoMatchContext : DbContext
    {
        public DbSet<TryResolveNoMatchEntity> Items => Set<TryResolveNoMatchEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TryResolveNoMatchEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("try_resolve_no_match_items");
            });
        }
    }

    [Fact]
    public void TryResolvePropertyName_Returns_False_When_Not_Found()
    {
        using TryResolveNoMatchContext context = new();
        IEntityType entityType = GetEntityType<TryResolveNoMatchEntity>(context);

        bool found = AnnotationRendererHelper.TryResolvePropertyName(entityType, "ghost_column", out string propertyName);

        Assert.False(found);
        Assert.Equal("ghost_column", propertyName);
    }

    #endregion

    #region TryResolvePropertyName_Fallback_FindProperty_When_No_StoreObject

    private class NoMappingFallbackEntity
    {
        public int Timestamp { get; set; }
        public double SomeValue { get; set; }
    }

    private class NoMappingFallbackContext : DbContext
    {
        public DbSet<NoMappingFallbackEntity> Items => Set<NoMappingFallbackEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoMappingFallbackEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToSqlQuery("SELECT 0 AS \"Timestamp\", 0.0 AS \"SomeValue\"");
            });
        }
    }

    [Fact]
    public void TryResolvePropertyName_Fallback_FindProperty_When_No_StoreObject()
    {
        // Arrange
        using NoMappingFallbackContext context = new();
        IEntityType entityType = GetEntityType<NoMappingFallbackEntity>(context);

        // Act
        bool found = AnnotationRendererHelper.TryResolvePropertyName(entityType, "SomeValue", out string propertyName);

        // Assert
        Assert.True(found);
        Assert.Equal("SomeValue", propertyName);
    }

    #endregion

    #region TryResolvePropertyName_NoStore_And_PropertyMissing_Returns_Raw

    private class NoStoreMissingPropertyEntity
    {
        public int Counter { get; set; }
    }

    private class NoStoreMissingPropertyContext : DbContext
    {
        public DbSet<NoStoreMissingPropertyEntity> Items => Set<NoStoreMissingPropertyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NoStoreMissingPropertyEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToSqlQuery("SELECT 0 AS \"Counter\"");
            });
        }
    }

    [Fact]
    public void TryResolvePropertyName_NoStore_And_PropertyMissing_Returns_Raw()
    {
        // Arrange
        using NoStoreMissingPropertyContext context = new();
        IEntityType entityType = GetEntityType<NoStoreMissingPropertyEntity>(context);

        // Act
        bool found = AnnotationRendererHelper.TryResolvePropertyName(entityType, "nonexistent_column", out string propertyName);

        // Assert
        Assert.False(found);
        Assert.Equal("nonexistent_column", propertyName);
    }

    #endregion

    #region TryResolvePropertyName_View_Mapped_Entity_Returns_True_On_Match

    private class ViewMappedEntity
    {
        public DateTime CreatedAt { get; set; }
        public string? Name { get; set; }
    }

    private class ViewMappedContext : DbContext
    {
        public DbSet<ViewMappedEntity> Items => Set<ViewMappedEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewMappedEntity>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("view_mapped_items");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            });
        }
    }

    [Fact]
    public void TryResolvePropertyName_View_Mapped_Entity_Returns_True_On_Match()
    {
        using ViewMappedContext context = new();
        IEntityType entityType = GetEntityType<ViewMappedEntity>(context);

        bool found = AnnotationRendererHelper.TryResolvePropertyName(entityType, "created_at", out string propertyName);

        Assert.True(found);
        Assert.Equal("CreatedAt", propertyName);
    }

    #endregion

    #region TryResolvePropertyName_FindProperty_Fallback_When_Column_Name_Differs_From_Property_Name

    private class SnakeCaseEntity
    {
        public int Id { get; set; }
        public double SomeValue { get; set; }
    }

    private class SnakeCaseContext : DbContext
    {
        public DbSet<SnakeCaseEntity> Items => Set<SnakeCaseEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                .UseTimescaleDb()
                .UseSnakeCaseNamingConvention();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SnakeCaseEntity>(e => e.ToTable("snake_case_entity"));
    }

    [Fact]
    public void TryResolvePropertyName_FindProperty_Fallback_When_Column_Name_Differs_From_Property_Name()
    {
        // Arrange
        using SnakeCaseContext context = new();
        IEntityType entityType = GetEntityType<SnakeCaseEntity>(context);

        // Act
        bool found = AnnotationRendererHelper.TryResolvePropertyName(entityType, "SomeValue", out string propertyName);

        // Assert
        Assert.True(found);
        Assert.Equal("SomeValue", propertyName);
    }

    #endregion
}

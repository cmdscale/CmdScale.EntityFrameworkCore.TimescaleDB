using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Internals;

/// <summary>
/// Unit tests for <see cref="ParentEntityTypeResolver"/>: null/whitespace guard and the three
/// match strategies (CLR type name, EF short name, database table name).
/// </summary>
public class ParentEntityTypeResolverTests
{
    private static IModel GetModel<TContext>(TContext context) where TContext : DbContext
        => context.GetService<IDesignTimeModel>().Model;

    #region Resolve_NullParentName_Returns_Null

    private class NullLookupEntity { public int Id { get; set; } }

    private class NullLookupContext : DbContext
    {
        public DbSet<NullLookupEntity> Items => Set<NullLookupEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullLookupEntity>().ToTable("null_lookup_items");
    }

    [Fact]
    public void Resolve_NullParentName_Returns_Null()
    {
        // Arrange
        using NullLookupContext context = new();
        IModel model = GetModel(context);

        // Act
        IEntityType? result = ParentEntityTypeResolver.Resolve(model, null);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Resolve_WhitespaceParentName_Returns_Null

    private class WhitespaceLookupEntity { public int Id { get; set; } }

    private class WhitespaceLookupContext : DbContext
    {
        public DbSet<WhitespaceLookupEntity> Items => Set<WhitespaceLookupEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<WhitespaceLookupEntity>().ToTable("whitespace_lookup_items");
    }

    [Fact]
    public void Resolve_WhitespaceParentName_Returns_Null()
    {
        // Arrange
        using WhitespaceLookupContext context = new();
        IModel model = GetModel(context);

        // Act
        IEntityType? result = ParentEntityTypeResolver.Resolve(model, "   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyParentName_Returns_Null()
    {
        // Arrange
        using WhitespaceLookupContext context = new();
        IModel model = GetModel(context);

        // Act
        IEntityType? result = ParentEntityTypeResolver.Resolve(model, string.Empty);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Resolve_ByClrTypeName_Returns_EntityType

    private class ClrNameMatchEntity { public int Id { get; set; } }

    private class ClrNameMatchContext : DbContext
    {
        public DbSet<ClrNameMatchEntity> Items => Set<ClrNameMatchEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ClrNameMatchEntity>().ToTable("clr_name_match_items");
    }

    [Fact]
    public void Resolve_ByClrTypeName_Returns_EntityType()
    {
        // Arrange
        using ClrNameMatchContext context = new();
        IModel model = GetModel(context);

        // Act
        IEntityType? result = ParentEntityTypeResolver.Resolve(model, nameof(ClrNameMatchEntity));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(typeof(ClrNameMatchEntity), result.ClrType);
    }

    #endregion

    #region Resolve_ByTableName_Returns_EntityType

    private class TableNameMatchEntity { public int Id { get; set; } }

    private class TableNameMatchContext : DbContext
    {
        public DbSet<TableNameMatchEntity> Items => Set<TableNameMatchEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TableNameMatchEntity>().ToTable("sensor_data_table_name");
    }

    [Fact]
    public void Resolve_ByTableName_Returns_EntityType()
    {
        // Arrange
        using TableNameMatchContext context = new();
        IModel model = GetModel(context);

        // Act
        IEntityType? result = ParentEntityTypeResolver.Resolve(model, "sensor_data_table_name");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("sensor_data_table_name", result.GetTableName());
    }

    #endregion

    #region Resolve_NoMatch_Returns_Null

    private class NoMatchEntity { public int Id { get; set; } }

    private class NoMatchContext : DbContext
    {
        public DbSet<NoMatchEntity> Items => Set<NoMatchEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoMatchEntity>().ToTable("no_match_items");
    }

    [Fact]
    public void Resolve_NoMatch_Returns_Null()
    {
        // Arrange
        using NoMatchContext context = new();
        IModel model = GetModel(context);

        // Act
        IEntityType? result = ParentEntityTypeResolver.Resolve(model, "does_not_exist_anywhere");

        // Assert
        Assert.Null(result);
    }

    #endregion
}

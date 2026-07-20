using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Extensions;

/// <summary>
/// Tests for <c>TimescaleDbContextOptionsBuilderExtensions</c>.
/// </summary>
public class TimescaleDbContextOptionsBuilderExtensionsTests
{
    // ── Generic overload ──────────────────────────────────────────────────────

    #region Should_Return_Typed_Builder_From_Generic_UseTimescaleDb

    private class GenericOverloadEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class GenericOverloadContext : DbContext
    {
        public DbSet<GenericOverloadEntity> Metrics => Set<GenericOverloadEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }
    }

    [Fact]
    public void Should_Return_Typed_Builder_From_Generic_UseTimescaleDb()
    {
        // Arrange
        DbContextOptionsBuilder<GenericOverloadContext> typedBuilder = new();
        typedBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test");

        // Act
        DbContextOptionsBuilder<GenericOverloadContext> returned = typedBuilder.UseTimescaleDb<GenericOverloadContext>();

        // Assert
        Assert.Same(typedBuilder, returned);
    }

    #endregion

    #region Should_Register_TimescaleDb_Extension_Via_Generic_UseTimescaleDb

    private class ExtensionRegistrationEntity
    {
        public DateTime Timestamp { get; set; }
    }

    private class ExtensionRegistrationContext : DbContext
    {
        public DbSet<ExtensionRegistrationEntity> Items => Set<ExtensionRegistrationEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                .UseTimescaleDb();
    }

    [Fact]
    public void Should_Register_TimescaleDb_Extension_Via_Generic_UseTimescaleDb()
    {
        // Arrange
        DbContextOptionsBuilder<ExtensionRegistrationContext> builder = new();
        builder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test");

        // Act
        builder.UseTimescaleDb<ExtensionRegistrationContext>();

        // Assert
        Dictionary<string, string> debugInfo = [];
        foreach (IDbContextOptionsExtension ext in builder.Options.Extensions)
        {
            ext.Info.PopulateDebugInfo(debugInfo);
        }

        Assert.True(debugInfo.ContainsKey("TimescaleDB:Enabled"));
        Assert.Equal("True", debugInfo["TimescaleDB:Enabled"]);
    }

    #endregion

    // ── ExtensionInfo.PopulateDebugInfo ───────────────────────────────────────

    #region Should_PopulateDebugInfo_With_Enabled_True

    private class DebugInfoEntity
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class DebugInfoContext : DbContext
    {
        public DbSet<DebugInfoEntity> Metrics => Set<DebugInfoEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                .UseTimescaleDb();
    }

    [Fact]
    public void Should_PopulateDebugInfo_With_Enabled_True()
    {
        // Arrange
        DbContextOptionsBuilder builder = new();
        builder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test");
        builder.UseTimescaleDb();
        Dictionary<string, string> debugInfo = [];

        // Act
        foreach (IDbContextOptionsExtension ext in builder.Options.Extensions)
        {
            ext.Info.PopulateDebugInfo(debugInfo);
        }

        // Assert
        Assert.True(debugInfo.TryGetValue("TimescaleDB:Enabled", out string? value));
        Assert.Equal("True", value);
    }

    #endregion

    // ── Service registration integration ─────────────────────────────────────

    #region Should_Register_TimescaleMigrationsModelDiffer

    private class ServiceRegMigrateEntity
    {
        public DateTime Timestamp { get; set; }
    }

    private class ServiceRegMigrateContext : DbContext
    {
        public DbSet<ServiceRegMigrateEntity> Items => Set<ServiceRegMigrateEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ServiceRegMigrateEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("svc_reg_migrate");
                e.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Register_TimescaleMigrationsModelDiffer()
    {
        // Arrange & Act
        using ServiceRegMigrateContext context = new();
        IMigrationsModelDiffer differ = context.GetService<IMigrationsModelDiffer>();

        // Assert
        Assert.IsType<TimescaleMigrationsModelDiffer>(differ);
    }

    #endregion

    #region Should_Register_TimescaleDbMigrationsSqlGenerator

    private class ServiceRegSqlGenEntity
    {
        public DateTime Timestamp { get; set; }
    }

    private class ServiceRegSqlGenContext : DbContext
    {
        public DbSet<ServiceRegSqlGenEntity> Items => Set<ServiceRegSqlGenEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ServiceRegSqlGenEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("svc_reg_sqlgen");
                e.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Register_TimescaleDbMigrationsSqlGenerator()
    {
        // Arrange & Act
        using ServiceRegSqlGenContext context = new();

        IMigrationsSqlGenerator generator = context.GetService<IMigrationsSqlGenerator>();

        // Assert
        Assert.IsType<TimescaleDbMigrationsSqlGenerator>(generator);
    }

    #endregion

    #region Should_Support_Multiple_UseTimescaleDb_Calls_Idempotently

    private class IdempotentEntity
    {
        public DateTime Timestamp { get; set; }
    }

    private class IdempotentContext : DbContext
    {
        public DbSet<IdempotentEntity> Items => Set<IdempotentEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                .UseTimescaleDb()
                .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdempotentEntity>(e =>
            {
                e.HasNoKey();
                e.ToTable("idempotent_entity");
                e.IsHypertable(x => x.Timestamp);
            });
        }
    }

    [Fact]
    public void Should_Support_Multiple_UseTimescaleDb_Calls_Idempotently()
    {
        // Arrange
        DbContextOptionsBuilder builder = new();
        builder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test");

        // Act
        builder.UseTimescaleDb();
        builder.UseTimescaleDb();

        // Assert
        Dictionary<string, string> debugInfo = [];
        foreach (IDbContextOptionsExtension ext in builder.Options.Extensions)
        {
            ext.Info.PopulateDebugInfo(debugInfo);
        }

        Assert.Equal("True", debugInfo["TimescaleDB:Enabled"]);
    }

    #endregion
}

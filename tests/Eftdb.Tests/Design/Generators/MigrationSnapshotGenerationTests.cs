#pragma warning disable EF1001
using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ContinuousAggregate;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators;

/// <summary>
/// Migration-snapshot generation must preserve TimescaleDB feature annotations verbatim. The design-time
/// <c>IAnnotationCodeGenerator</c> is also active during <c>dotnet ef migrations add</c> for package
/// consumers; the feature renderers may only rewrite annotations during scaffolding. Outside scaffolding
/// the <c>.HasAnnotation(...)</c> fallback keeps the snapshot complete and compilable (builder extension
/// methods require the generic <c>EntityTypeBuilder&lt;T&gt;</c>, which snapshots do not use).
/// </summary>
public class MigrationSnapshotGenerationTests
{
    private static string GenerateSnapshot(DbContext context)
    {
        IModel model = context.GetService<IDesignTimeModel>().Model;
        ServiceCollection services = new();
        services.AddEntityFrameworkDesignTimeServices();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);
        using ServiceProvider sp = services.BuildServiceProvider();
        IMigrationsCodeGenerator generator = sp.GetRequiredService<IMigrationsCodeGeneratorSelector>().Select("C#");
        return generator.GenerateSnapshot("TestModels", context.GetType(), "TestSnapshot", model);
    }

    #region Should_Preserve_Feature_Annotations_In_CodeFirst_Snapshot

    private class SnapshotSource
    {
        public DateTime Time { get; set; }
        public double Price { get; set; }
        public string Exchange { get; set; } = null!;
    }

    private class SnapshotCa
    {
        public DateTime Bucket { get; set; }
        public double MaxPrice { get; set; }
        public string Exchange { get; set; } = null!;
    }

    private class SnapshotCodeFirstContext : DbContext
    {
        public DbSet<SnapshotSource> Sources => Set<SnapshotSource>();
        public DbSet<SnapshotCa> Cas => Set<SnapshotCa>();
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").UseTimescaleDb();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SnapshotSource>(e =>
            {
                e.HasNoKey();
                e.ToTable("snapshot_cf_source");
                e.IsHypertable(x => x.Time);
            });
            modelBuilder.Entity<SnapshotCa>(e =>
            {
                e.HasNoKey();
                e.IsContinuousAggregate<SnapshotCa, SnapshotSource, DateTime>("snapshot_cf_ca", "1 hour", x => x.Time)
                    .AddAggregateFunction(x => x.MaxPrice, x => x.Price, EAggregateFunction.Max)
                    .AddGroupByColumn(x => x.Exchange);
            });
        }
    }

    [Fact]
    public void Should_Preserve_Feature_Annotations_In_CodeFirst_Snapshot()
    {
        using SnapshotCodeFirstContext context = new();

        string snapshot = GenerateSnapshot(context);

        Assert.Contains($"HasAnnotation(\"{HypertableAnnotations.IsHypertable}\"", snapshot);
        Assert.Contains($"HasAnnotation(\"{HypertableAnnotations.HypertableTimeColumn}\"", snapshot);
        Assert.Contains($"HasAnnotation(\"{ContinuousAggregateAnnotations.MaterializedViewName}\"", snapshot);
        Assert.Contains($"HasAnnotation(\"{ContinuousAggregateAnnotations.AggregateFunctions}\"", snapshot);
        Assert.Contains($"HasAnnotation(\"{ContinuousAggregateAnnotations.GroupByColumns}\"", snapshot);

        Assert.DoesNotContain(".IsHypertable(", snapshot);
        Assert.DoesNotContain(".IsContinuousAggregate(", snapshot);
        Assert.DoesNotContain(".AddAggregateFunction(", snapshot);
    }

    #endregion
}
#pragma warning restore EF1001

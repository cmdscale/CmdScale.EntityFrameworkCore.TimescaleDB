using CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Snapshots;
using CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit.Abstractions;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests
{
    public class TimescaleDbMigrationsInfrastructureTests : MigrationsInfrastructureTestBase<TimescaleMigrationsFixture>
    {
        private readonly ITestOutputHelper _testOutputHelper;

        private class AspNetIdentityDbContext(DbContextOptions options)
            : IdentityDbContext<IdentityUser>(options)
        {
            protected override void OnModelCreating(ModelBuilder builder)
            {
                base.OnModelCreating(builder);

                builder.Entity<IdentityUser>(b =>
                {
                    b.HasIndex(u => u.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique();
                    b.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex");
                    b.ToTable("AspNetUsers");
                });

                builder.Entity<IdentityUserClaim<string>>(b =>
                {
                    b.ToTable("AspNetUserClaims");
                });

                builder.Entity<IdentityUserLogin<string>>(b =>
                {
                    b.ToTable("AspNetUserLogins");

                    b.Property(l => l.LoginProvider).HasMaxLength(128);
                    b.Property(l => l.ProviderKey).HasMaxLength(128);
                });

                builder.Entity<IdentityUserToken<string>>(b =>
                {
                    b.ToTable("AspNetUserTokens");

                    b.Property(t => t.LoginProvider).HasMaxLength(128);
                    b.Property(t => t.Name).HasMaxLength(128);
                });

                builder.Entity<IdentityRole>(b =>
                {
                    b.HasIndex(r => r.NormalizedName).HasDatabaseName("RoleNameIndex").IsUnique();
                    b.ToTable("AspNetRoles");
                });

                builder.Entity<IdentityRoleClaim<string>>(b =>
                {
                    b.ToTable("AspNetRoleClaims");
                });

                builder.Entity<IdentityUserRole<string>>(b =>
                {
                    b.ToTable("AspNetUserRoles");
                });
            }
        }

        public TimescaleDbMigrationsInfrastructureTests(TimescaleMigrationsFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture)
        {
            _testOutputHelper = testOutputHelper;
            Fixture.ListLoggerFactory.Clear();
        }

        protected override Task ExecuteSqlAsync(string value)
        {
            ((TimescaleTestStore)Fixture.TestStore).ExecuteScript(value);
            return Task.CompletedTask;
        }

        [ConditionalFact]
        public override void Can_diff_against_2_1_ASP_NET_Identity_model()
        {
            using AspNetIdentityDbContext context = new(
                Fixture.TestStore.AddProviderOptions(new DbContextOptionsBuilder()).Options);

            DiffSnapshot(new AspNetIdentity21ModelSnapshot(), context);
        }

        [ConditionalFact]
        public override void Can_diff_against_2_2_ASP_NET_Identity_model()
        {
            using AspNetIdentityDbContext context = new(
                Fixture.TestStore.AddProviderOptions(new DbContextOptionsBuilder()).Options);

            DiffSnapshot(new AspNetIdentity22ModelSnapshot(), context);
        }

        [ConditionalFact]
        public override void Can_diff_against_2_2_model()
        {
            using MigrationsInfrastructureFixtureBase.MigrationsContext context = Fixture.CreateContext();
            DiffSnapshot(new EfCore22ModelSnapshot(), context);
        }

        [ConditionalFact]
        public override void Can_diff_against_3_0_ASP_NET_Identity_model()
        {
            using AspNetIdentityDbContext context = new(
                Fixture.TestStore.AddProviderOptions(new DbContextOptionsBuilder()).Options);

            DiffSnapshot(new AspNetIdentity30ModelSnapshot(), context);
        }

        protected virtual void DiffSnapshotWithDebug(ModelSnapshot snapshot, DbContext context)
        {
            IModel sourceModel = context.GetService<IModelRuntimeInitializer>().Initialize(
                snapshot.Model, designTime: true, validationLogger: null);

            IMigrationsModelDiffer modelDiffer = context.GetService<IMigrationsModelDiffer>();
            IReadOnlyList<MigrationOperation> operations = modelDiffer.GetDifferences(
                sourceModel.GetRelationalModel(),
                context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

            if (operations.Count > 0)
            {
                _testOutputHelper.WriteLine($"Found {operations.Count} differences:");
                for (int i = 0; i < operations.Count; i++)
                {
                    MigrationOperation op = operations[i];
                    _testOutputHelper.WriteLine($"  {i + 1}. {op.GetType().Name}:");

                    if (op is AlterColumnOperation alterColumn)
                    {
                        _testOutputHelper.WriteLine($"    Table: {alterColumn.Table}");
                        _testOutputHelper.WriteLine($"    Column: {alterColumn.Name}");
                        _testOutputHelper.WriteLine($"    OldColumn Type: {alterColumn.OldColumn?.ClrType?.Name} -> {alterColumn.OldColumn?.ColumnType}");
                        _testOutputHelper.WriteLine($"    NewColumn Type: {alterColumn.ClrType?.Name} -> {alterColumn.ColumnType}");
                        _testOutputHelper.WriteLine($"    OldColumn Nullable: {alterColumn.OldColumn?.IsNullable}");
                        _testOutputHelper.WriteLine($"    NewColumn Nullable: {alterColumn.IsNullable}");
                        _testOutputHelper.WriteLine($"    OldColumn DefaultValue: {alterColumn.OldColumn?.DefaultValue}");
                        _testOutputHelper.WriteLine($"    NewColumn DefaultValue: {alterColumn.DefaultValue}");
                        _testOutputHelper.WriteLine($"    OldColumn DefaultValueGenerated: {alterColumn.OldColumn?.DefaultValueSql}");
                        _testOutputHelper.WriteLine($"    NewColumn DefaultValueGenerated: {alterColumn.DefaultValueSql}");
                    }
                    else if (op is CreateTableOperation createTable)
                    {
                        _testOutputHelper.WriteLine($"    Table: {createTable.Name}");
                        _testOutputHelper.WriteLine($"    Columns: {string.Join(", ", createTable.Columns.Select(c => $"{c.Name}:{c.ClrType?.Name}"))}");
                    }
                    else if (op is CreateIndexOperation createIndex)
                    {
                        _testOutputHelper.WriteLine($"    Table: {createIndex.Table}");
                        _testOutputHelper.WriteLine($"    Name: {createIndex.Name}");
                        _testOutputHelper.WriteLine($"    Columns: {string.Join(", ", createIndex.Columns)}");
                        _testOutputHelper.WriteLine($"    IsUnique: {createIndex.IsUnique}");
                    }
                    else
                    {
                        _testOutputHelper.WriteLine($"    {op}");
                    }
                    _testOutputHelper.WriteLine("");
                }
            }

            Assert.Empty(operations);
        }
    }
}
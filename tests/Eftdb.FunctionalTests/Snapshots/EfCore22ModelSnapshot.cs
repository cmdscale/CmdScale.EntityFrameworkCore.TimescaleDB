// EF Core 2.2 Model Snapshot (for the basic Foo entity)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Snapshots
{
    public class EfCore22ModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "2.2.4-servicing-10062")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("Microsoft.EntityFrameworkCore.Migrations.MigrationsInfrastructureFixtureBase+Foo", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("integer")
                    .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                b.Property<int>("Bar")
                    .HasColumnType("integer");

                b.Property<string>("Description")
                    .HasColumnType("text");

                b.HasKey("Id");

                b.ToTable("Table1");
            });
#pragma warning restore 612, 618
        }
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Npgsql.EntityFrameworkCore.PostgreSQL.Scaffolding.Internal;
using System.Reflection;

#pragma warning disable EF1001 // NpgsqlCodeGenerator lives in *.Internal but is public and the documented extension point.
namespace CmdScale.EntityFrameworkCore.TimescaleDB.Design
{
    /// <summary>
    /// Extends Npgsql's scaffold code generator so the generated <c>OnConfiguring</c> chains
    /// <c>.UseTimescaleDb()</c> after <c>.UseNpgsql(...)</c>.
    /// </summary>
    public class TimescaleDbCodeGenerator(ProviderCodeGeneratorDependencies dependencies)
        : NpgsqlCodeGenerator(dependencies)
    {
        private static readonly MethodInfo UseTimescaleDbMethod =
            typeof(TimescaleDbContextOptionsBuilderExtensions)
                .GetMethod(
                    nameof(TimescaleDbContextOptionsBuilderExtensions.UseTimescaleDb),
                    [typeof(DbContextOptionsBuilder)])
            ?? throw new InvalidOperationException(
                "Could not locate UseTimescaleDb(DbContextOptionsBuilder) via reflection.");

        public override MethodCallCodeFragment GenerateUseProvider(
            string connectionString,
            MethodCallCodeFragment? providerOptions)
            => base.GenerateUseProvider(connectionString, providerOptions)
                   .Chain(new MethodCallCodeFragment(UseTimescaleDbMethod));
    }
}
#pragma warning restore EF1001

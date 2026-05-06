using CmdScale.EntityFrameworkCore.TimescaleDB.Design;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design;

/// <summary>
/// Regression tests for gap #1: the scaffolder must emit
/// <c>.UseNpgsql(...).UseTimescaleDb()</c> in the generated <c>OnConfiguring</c>
/// so the scaffolded context registers <c>TimescaleMigrationsModelDiffer</c>
/// and emits TimescaleDB operations on subsequent migrations.
/// </summary>
public class TimescaleDbCodeGeneratorTests
{
    private const string ConnectionString = "Host=localhost;Database=test;Username=test;Password=test";

    #region Should_Chain_UseTimescaleDb_After_UseNpgsql

    [Fact]
    public void Should_Chain_UseTimescaleDb_After_UseNpgsql()
    {
        // Arrange — mimic the EF tooling DI container so the test exercises the same
        // resolution path as `dotnet ef dbcontext scaffold`. This also confirms that
        // TimescaleDBDesignTimeServices wins over the Npgsql default registration.
        ServiceCollection services = new();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        IProviderConfigurationCodeGenerator generator = provider.GetRequiredService<IProviderConfigurationCodeGenerator>();

        // Act
        MethodCallCodeFragment fragment = generator.GenerateUseProvider(ConnectionString, providerOptions: null);

        // Assert — head call must remain UseNpgsql; chained call must be UseTimescaleDb.
        Assert.Equal("UseNpgsql", fragment.Method);
        Assert.NotNull(fragment.ChainedCall);
        Assert.Equal("UseTimescaleDb", fragment.ChainedCall!.Method);
    }

    #endregion

    #region Should_Resolve_TimescaleDbCodeGenerator_From_Service_Provider

    [Fact]
    public void Should_Resolve_TimescaleDbCodeGenerator_From_Service_Provider()
    {
        // Arrange
        ServiceCollection services = new();
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);

        // Act
        using ServiceProvider provider = services.BuildServiceProvider();
        IProviderConfigurationCodeGenerator generator = provider.GetRequiredService<IProviderConfigurationCodeGenerator>();

        // Assert — the Eftdb.Design registration must replace Npgsql's default.
        Assert.IsType<TimescaleDbCodeGenerator>(generator);
    }

    #endregion

    #region Should_Chain_UseTimescaleDb_When_Npgsql_Defaults_Are_Pre_Registered

    [Fact]
    public void Should_Chain_UseTimescaleDb_When_Npgsql_Defaults_Are_Pre_Registered()
    {
        // Arrange — register Npgsql defaults first so the test confirms registration
        // order does not matter: TimescaleDBDesignTimeServices must still take over.
        ServiceCollection services = new();
#pragma warning disable EF1001
        new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(services);
#pragma warning restore EF1001
        new TimescaleDBDesignTimeServices().ConfigureDesignTimeServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        IProviderConfigurationCodeGenerator generator = provider.GetRequiredService<IProviderConfigurationCodeGenerator>();

        // Act
        MethodCallCodeFragment fragment = generator.GenerateUseProvider(ConnectionString, providerOptions: null);

        // Assert
        Assert.IsType<TimescaleDbCodeGenerator>(generator);
        Assert.Equal("UseNpgsql", fragment.Method);
        Assert.NotNull(fragment.ChainedCall);
        Assert.Equal("UseTimescaleDb", fragment.ChainedCall!.Method);
    }

    #endregion
}

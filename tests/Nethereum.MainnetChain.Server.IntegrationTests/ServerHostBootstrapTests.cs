using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Gate;
using Nethereum.MainnetChain.Server.Hosting;
using Nethereum.MainnetChain.Server.Rpc;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class ServerHostBootstrapTests
{
    [Fact]
    public void AddMainnetChainServer_WithoutBeaconEndpoint_RegistersAlwaysAcceptGate()
    {
        var services = new ServiceCollection();
        var config = new MainnetChainServerConfig
        {
            DataDir = null,
            LightClient = null,
        };

        services.AddLogging();
        services.AddMainnetChainServer(config);

        using var provider = services.BuildServiceProvider();

        var gate = provider.GetRequiredService<IConsensusBlockGate>();
        Assert.IsType<AlwaysAcceptConsensusBlockGate>(gate);
    }

    [Fact]
    public void AddMainnetChainServer_WithoutBeaconEndpoint_RegistersLatestOnlyFinalityProvider()
    {
        var services = new ServiceCollection();
        var config = new MainnetChainServerConfig();

        services.AddLogging();
        services.AddMainnetChainServer(config);

        using var provider = services.BuildServiceProvider();

        var cursor = provider.GetRequiredService<IFinalityCursorProvider>();
        Assert.IsType<LatestOnlyFinalityCursorProvider>(cursor);
        Assert.Null(cursor.GetFinalizedBlockNumber());
        Assert.Null(cursor.GetSafeBlockNumber());
    }

    [Fact]
    public void AddMainnetChainServer_WithoutBeaconEndpoint_DoesNotRegisterLightClientHostedService()
    {
        var services = new ServiceCollection();
        var config = new MainnetChainServerConfig();

        services.AddLogging();
        services.AddMainnetChainServer(config);

        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(IHostedService) && DescribesImplementationType(sd, typeof(LightClientHostedService)));
    }

    [Fact]
    public void AddMainnetChainServer_RegistersMainnetChainHostedService()
    {
        var services = new ServiceCollection();
        var config = new MainnetChainServerConfig();

        services.AddLogging();
        services.AddMainnetChainServer(config);

        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IHostedService) && DescribesImplementationType(sd, typeof(MainnetChainHostedService)));
    }

    private static bool DescribesImplementationType(ServiceDescriptor sd, Type expected)
        => sd.ImplementationType == expected
           || sd.ImplementationInstance?.GetType() == expected;
}

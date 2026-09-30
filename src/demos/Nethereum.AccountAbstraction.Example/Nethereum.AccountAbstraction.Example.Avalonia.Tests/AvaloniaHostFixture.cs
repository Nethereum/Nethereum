using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.WebAuthn.Windows;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Avalonia.Tests;

[CollectionDefinition(COLLECTION_NAME)]
public class AvaloniaHostCollection : ICollectionFixture<AvaloniaHostFixture>
{
    public const string COLLECTION_NAME = "AaAvaloniaHost";
}

public class AvaloniaHostFixture : IAsyncLifetime
{
    public const string COLLECTION_NAME = AvaloniaHostCollection.COLLECTION_NAME;

    public IServiceProvider Services { get; private set; } = null!;
    private HostBootstrap _infra = null!;

    public async Task InitializeAsync()
    {
        _infra = await HostBootstrap.StartAsync();
        var services = new ServiceCollection();
        services.AddNethereumWebAuthnWindows();
        services.AddExampleHost(_infra);
        Services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync() => await _infra.Bootstrap.DisposeAsync();
}

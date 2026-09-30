using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.WebAuthn;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [CollectionDefinition(COLLECTION_NAME)]
    public class PasskeyHostCollection : ICollectionFixture<PasskeyHostFixture>
    {
        public const string COLLECTION_NAME = "AaExamplePasskeyHost";
    }

    public class PasskeyHostFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = PasskeyHostCollection.COLLECTION_NAME;

        public IServiceProvider Services { get; private set; } = null!;
        public HostBootstrap Infra { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Infra = await HostBootstrap.StartAsync();

            var authenticator = new SoftwareWebAuthnAuthenticator(requireUV: true);
            var services = new ServiceCollection();
            services.AddSingleton<IWebAuthnAuthenticator>(authenticator);
            services.AddSingleton<IWebAuthnCredentialFactory>(authenticator);
            services.AddExampleHost(Infra);

            Services = services.BuildServiceProvider();
        }

        public async Task DisposeAsync() => await Infra.Bootstrap.DisposeAsync();
    }
}

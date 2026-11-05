using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection("Sequential")]
    public class MainnetConfiguredEnodeRefusalTests
    {
        private static readonly string WellFormed = $"enode://{new string('a', 128)}@127.0.0.1:30307";
        private const string Malformed = "enode://not-a-real-pubkey@112.154.155.200:20001";

        private static MainnetChainServerConfig ConfigAt(string dataDir, string trustedPeer) =>
            new MainnetChainServerConfig
            {
                DataDir = dataDir,
                ListenPort = -1,
                DisableDiscv4 = true,
                DisableDiscv5 = true,
                TargetPeers = 1,
                TrustedPeer = trustedPeer,
            };

        [Fact]
        public async Task Given_ATrustedPeerListWithAMalformedEnode_When_TheMainnetNodeIsComposed_Then_StartupIsRefusedNamingTheKeyBeforeTheDataDirectoryOrNodeKeyIsCreated()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "mainnet-enode-refusal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => new ServiceCollection().AddMainnetNodeAsync(
                        ConfigAt(dataDir, $"{WellFormed},{Malformed}"), NullLoggerFactory.Instance));

                Assert.Contains($"MainnetChain:TrustedPeer = '{Malformed}'", refusal.Message);
                Assert.False(Directory.Exists(dataDir), "a refused start must not create the data directory");
            }
            finally
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
            }
        }

        [Fact]
        public async Task Given_OnlyWellFormedTrustedPeers_When_TheMainnetNodeIsComposed_Then_ItStartsAndCreatesTheDataDirectory()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "mainnet-enode-refusal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var services = new ServiceCollection();
                services.AddLogging();
                await services.AddMainnetNodeAsync(ConfigAt(dataDir, WellFormed), NullLoggerFactory.Instance);
                var provider = services.BuildServiceProvider();
                try
                {
                    Assert.Contains(provider.GetServices<IHostedService>(), node => node is IAsyncDisposable);
                    Assert.True(Directory.Exists(dataDir));
                    Assert.NotEmpty(Directory.GetFileSystemEntries(dataDir));
                }
                finally
                {
                    await provider.DisposeAsync();
                }
            }
            finally
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
            }
        }
    }
}

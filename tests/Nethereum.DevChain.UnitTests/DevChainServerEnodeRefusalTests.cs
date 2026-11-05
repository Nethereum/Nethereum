using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ChainNode.Hosting;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainServerEnodeRefusalTests
    {
        private const string Malformed = "enode://not-a-real-pubkey@127.0.0.1:30702";
        private static readonly string WellFormed = $"enode://{new string('a', 128)}@127.0.0.1:30303";

        private static DevChainServerConfig RocksDbAt(string dataDir, params string[] trustedPeers)
        {
            var config = new DevChainServerConfig { Storage = "rocksdb", DataDir = dataDir };
            config.Node.Network.TrustedPeers = trustedPeers;
            return config;
        }

        [Fact]
        public void Given_AMalformedTrustedPeer_When_AHostCallsAddDevChainServerWithoutTheServerValidator_Then_ItIsRefusedNamingTheKeyBeforeTheDataDirectoryIsCreated()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "devchain-enode-refusal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var refusal = Assert.Throws<InvalidOperationException>(
                    () => new ServiceCollection().AddDevChainServer(RocksDbAt(dataDir, WellFormed, Malformed)));

                Assert.Contains($"DevChain:Node:Network:TrustedPeers:1 = '{Malformed}'", refusal.Message);
                Assert.False(Directory.Exists(dataDir), "a refused start must not create the data directory");
            }
            finally
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
            }
        }

        [Fact]
        public async Task Given_OnlyWellFormedTrustedPeers_When_AHostCallsAddDevChainServer_Then_ItOpensStorageInTheDataDirectory()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "devchain-enode-refusal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var services = new ServiceCollection().AddDevChainServer(RocksDbAt(dataDir, WellFormed));
                await using (var provider = services.BuildServiceProvider())
                {
                    var storage = provider.GetRequiredService<ChainNodeStorage>();
                    try
                    {
                        Assert.True(Directory.Exists(dataDir));
                        Assert.NotEmpty(Directory.GetFileSystemEntries(dataDir));
                    }
                    finally
                    {
                        await storage.DisposeAsync();
                    }
                }
            }
            finally
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
            }
        }
    }
}

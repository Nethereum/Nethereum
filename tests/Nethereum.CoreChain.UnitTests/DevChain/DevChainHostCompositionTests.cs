using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainHostCompositionTests
    {
        private static ServiceProvider BuildProvider(DevChainServerConfig config) =>
            new ServiceCollection().AddDevChainServer(config).BuildServiceProvider();

        [Fact]
        public async Task Given_TheDefaultServerConfig_When_TheNodeIsComposed_Then_NoListenerOrSyncStackIsCreated()
        {
            var config = new DevChainServerConfig { Storage = "memory" };
            using var provider = BuildProvider(config);

            var node = provider.GetRequiredService<Nethereum.DevChain.DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var accounts = provider.GetRequiredService<DevAccountManager>();

            await using var composed = await DevChainComposition.ComposeAsync(
                config, node, bundle,
                n => n.StartAsync(accounts.Accounts.Select(a => a.Address)),
                NullLoggerFactory.Instance);

            Assert.Null(composed.Listener);
            Assert.Null(composed.Sync);
            Assert.NotNull(composed.Mempool);
            Assert.NotNull(composed.Profile);
            Assert.Equal((ulong)config.ChainId, composed.Profile.NetworkId);
        }

        [Fact]
        public async Task Given_NetworkServeForcedOn_When_TheNodeIsComposed_Then_AListenerActuallyBinds()
        {
            var config = new DevChainServerConfig { Storage = "memory" };
            config.Node.Network.Serve = true;
            config.Node.Network.ListenPort = 0;
            config.Node.Network.NodeKeyHex = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";

            using var provider = BuildProvider(config);

            var node = provider.GetRequiredService<Nethereum.DevChain.DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var accounts = provider.GetRequiredService<DevAccountManager>();

            var composed = await DevChainComposition.ComposeAsync(
                config, node, bundle,
                n => n.StartAsync(accounts.Accounts.Select(a => a.Address)),
                NullLoggerFactory.Instance);

            try
            {
                Assert.NotNull(composed.Listener);
                Assert.True(composed.Listener.Port > 0);
            }
            finally
            {
                await composed.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_TheComposedNode_When_ChainStateIsRead_Then_ItMatchesTheDirectlyStartedNode()
        {
            var config = new DevChainServerConfig { Storage = "memory", ChainId = 31337 };
            using var provider = BuildProvider(config);

            var node = provider.GetRequiredService<Nethereum.DevChain.DevChainNode>();
            var bundle = provider.GetRequiredService<IChainStoreBundle>();
            var accounts = provider.GetRequiredService<DevAccountManager>();

            await using var composed = await DevChainComposition.ComposeAsync(
                config, node, bundle,
                n => n.StartAsync(accounts.Accounts.Select(a => a.Address)),
                NullLoggerFactory.Instance);

            var firstAccount = accounts.Accounts.First();
            var balance = await node.GetBalanceAsync(firstAccount.Address);

            Assert.Equal(firstAccount.Balance, balance);
            Assert.Equal(31337, (int)node.Config.ChainId);
        }
    }
}

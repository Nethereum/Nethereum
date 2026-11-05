using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting;
using MainnetChainNode = Nethereum.ChainNode.Hosting.ChainNode;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Node.HarnessServer;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class CustomGenesisInitialisationTests : IDisposable
    {
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"custom_genesis_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true); } catch { }
        }

        private Task<MainnetChainNode> ComposeAsync(StandardGenesisDocument genesis)
        {
            var config = new MainnetChainServerConfig { DataDir = _dataDir };
            return MainnetChainNode.ComposeAsync(
                new MainnetChainDefinition(genesis: genesis),
                config.ToChainNodeConfig(),
                NullLoggerFactory.Instance,
                storageFactory: (cfg, logger) => ChainNodeStorage.Open(cfg, logger, new TransactionVerificationAndRecoveryImp()));
        }

        private static async Task<(byte[] Hash, byte[] Root)> DevChainGenesisAsync(StandardGenesisDocument genesis)
        {
            await using var devchain = await HarnessNodeHost.ComposeInProcessAsync(genesis);
            var header = await devchain.Stores.Blocks.GetByNumberAsync(0);
            var hash = await devchain.Stores.Blocks.GetHashByNumberAsync(BigInteger.Zero);
            return (hash, header.StateRoot);
        }

        [Fact]
        public async Task Given_a_custom_genesis_When_the_chain_is_composed_on_an_empty_data_dir_Then_the_genesis_is_the_head_and_its_root_resolves_in_the_trie_store()
        {
            var genesis = CustomGenesisFixture.Document();
            var expected = await DevChainGenesisAsync(genesis);

            await using var node = await ComposeAsync(genesis);

            var header = await node.Bundle.Blocks.GetByNumberAsync(0);
            Assert.Equal(expected.Root, header.StateRoot);
            Assert.Equal(expected.Hash, await node.Bundle.Blocks.GetHashByNumberAsync(BigInteger.Zero));
            Assert.Equal(expected.Hash, node.Bundle.Metadata.GetLastBlockHash());
            Assert.Equal(0UL, node.Bundle.Metadata.GetLastBlock());
            Assert.True(node.Bundle.StateTrieNodes.ContainsKey(header.StateRoot));
            var funded = await node.Bundle.State.GetAccountAsync(CustomGenesisFixture.FundedAddress);
            Assert.Equal(new BigInteger(100) * BigInteger.Pow(10, 18), (BigInteger)funded.Balance);
        }

        [Fact]
        public async Task Given_a_composed_custom_genesis_When_the_store_is_reopened_Then_the_genesis_is_kept_and_its_root_still_resolves()
        {
            var genesis = CustomGenesisFixture.Document();
            byte[] hash;
            await using (var first = await ComposeAsync(genesis))
                hash = await first.Bundle.Blocks.GetHashByNumberAsync(BigInteger.Zero);

            await using var reopened = await ComposeAsync(genesis);

            var header = await reopened.Bundle.Blocks.GetByNumberAsync(0);
            Assert.Equal(hash, await reopened.Bundle.Blocks.GetHashByNumberAsync(BigInteger.Zero));
            Assert.True(reopened.Bundle.StateTrieNodes.ContainsKey(header.StateRoot));
            Assert.True(reopened.Bundle.Metadata.IsGenesisLoaded());
        }

        [Fact]
        public async Task Given_a_data_dir_holding_another_genesis_When_the_chain_is_composed_Then_it_is_refused()
        {
            await using (await ComposeAsync(CustomGenesisFixture.Document())) { }

            var other = CustomGenesisFixture.Document();
            other.GasLimit += 1;

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await ComposeAsync(other));
        }

        [Fact]
        public async Task Given_a_genesis_declaring_no_system_contracts_When_the_chain_is_composed_Then_they_are_predeployed_as_the_devchain_does()
        {
            var genesis = CustomGenesisFixture.Document();
            Assert.False(StandardGenesisLoader.DeclaresAnySystemContract(genesis));
            var expected = await DevChainGenesisAsync(genesis);

            await using var node = await ComposeAsync(genesis);

            var historyStorage = await node.Bundle.State.GetAccountAsync(
                Nethereum.CoreChain.Forks.SystemContractPredeploys.All[0].Address);
            Assert.NotNull(historyStorage);
            Assert.Equal(expected.Root, (await node.Bundle.Blocks.GetByNumberAsync(0)).StateRoot);
        }
    }
}

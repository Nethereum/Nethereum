using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;
using AppChainConfig = Nethereum.AppChain.AppChainConfig;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    public class AppChainSequencerBlockAccessListTests
    {
        [Fact]
        public async Task Given_AnAmsterdamAppChainSequencer_When_ItProducesABlock_Then_TheListTheHeaderCommitsToIsRetained()
        {
            var host = await HostAsync("amsterdam");

            await host.Sequencer.ProduceBlockAsync();

            var header = await host.AppChain.Blocks.GetByNumberAsync(1);
            Assert.NotNull(header.BlockAccessListHash);
            var retained = await host.AccessLists.GetByBlockNumberAsync(1);
            Assert.NotNull(retained);
            Assert.Equal(header.BlockAccessListHash, new Sha3Keccack().CalculateHash(retained));

            var response = await AskForBlockOneAsync(host.Node);
            Assert.False(response.HasError, response.Error?.Message);
            Assert.IsType<List<AccountAccessDto>>(response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_APragueAppChainSequencer_When_ItProducesABlock_Then_NoListIsRetainedAndTheReadIsResourceNotFound()
        {
            var host = await HostAsync("prague");

            await host.Sequencer.ProduceBlockAsync();

            var header = await host.AppChain.Blocks.GetByNumberAsync(1);
            Assert.Null(header.BlockAccessListHash);
            Assert.Null(await host.AccessLists.GetByBlockNumberAsync(1));

            var response = await AskForBlockOneAsync(host.Node);
            Assert.True(response.HasError);
            Assert.Equal(-32001, response.Error.Code);
        }

        private static async Task<TestHost> HostAsync(string hardfork)
        {
            var blocks = new InMemoryBlockStore();
            var accessLists = new InMemoryBlockAccessListStore(blocks);

            var config = AppChainConfig.Default;
            config.Hardfork = hardfork;

            var appChain = new AppChainCore(
                config,
                blocks,
                new InMemoryTransactionStore(blocks),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                new InMemoryStateStore());
            await appChain.InitializeAsync();

            var sequencer = new Sequencer(
                appChain,
                new SequencerConfig { BlockTimeMs = 0 },
                blockAccessListStore: accessLists);

            var node = new AppChainNode(appChain, sequencer, filterStore: null, accessLists);

            return new TestHost(appChain, sequencer, accessLists, node);
        }

        private static Task<RpcResponseMessage> AskForBlockOneAsync(AppChainNode node) =>
            new EthGetBlockAccessListHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockAccessList", "0x1"),
                new RpcContext(node, chainId: 1, services: null));

        private sealed class TestHost
        {
            public TestHost(
                AppChainCore appChain,
                Sequencer sequencer,
                IBlockAccessListStore accessLists,
                AppChainNode node)
            {
                AppChain = appChain;
                Sequencer = sequencer;
                AccessLists = accessLists;
                Node = node;
            }

            public AppChainCore AppChain { get; }
            public Sequencer Sequencer { get; }
            public IBlockAccessListStore AccessLists { get; }
            public AppChainNode Node { get; }
        }
    }
}

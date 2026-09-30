using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;
using AppChainConfig = Nethereum.AppChain.AppChainConfig;

namespace Nethereum.AppChain.Sequencer.UnitTests
{
    /// <summary>
    /// AMS-7928-34. An AppChain host retains EIP-7928 block access lists in its store bundle, and the node
    /// it serves RPC from has to be built with that store or <c>eth_getBlockAccessList</c> answers about a
    /// node that was never wired instead of about the block that was asked for.
    /// </summary>
    public class AppChainNodeBlockAccessListTests
    {
        private const string Address = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";

        [Fact]
        public async Task Given_AnAppChainNodeBuiltWithTheHostsStore_When_AnAmsterdamBlocksListIsAsked_Then_ItComesBack()
        {
            var chain = await AmsterdamChainAsync();
            var node = new AppChainNode(chain.AppChain, sequencer: null, filterStore: null, chain.AccessLists);

            var response = await AskForBlockOneAsync(node);

            Assert.False(response.HasError, response.Error?.Message);
            var accounts = Assert.IsType<List<AccountAccessDto>>(response.ResultNewtonsoft);
            Assert.Equal(Address, Assert.Single(accounts).Address);
        }

        [Fact]
        public async Task Given_AnAppChainNodeBuiltWithoutTheHostsStore_When_AnAmsterdamBlocksListIsAsked_Then_ItIsNotReportedAsPruned()
        {
            var chain = await AmsterdamChainAsync();
            var node = new AppChainNode(chain.AppChain, sequencer: null, filterStore: null, blockAccessListStore: null);

            var response = await AskForBlockOneAsync(node);

            Assert.True(response.HasError);
            Assert.NotEqual(4444, response.Error.Code);
            Assert.Equal(-32004, response.Error.Code);
        }

        private static async Task<TestChain> AmsterdamChainAsync()
        {
            var blocks = new InMemoryBlockStore();
            var accessLists = new InMemoryBlockAccessListStore(blocks);
            var blockHash = new byte[32];
            blockHash[31] = 0xaa;

            await blocks.SaveAsync(
                new BlockHeader { BlockNumber = 1, BlockAccessListHash = blockHash },
                blockHash);
            await accessLists.SaveAsync(blockHash, BlockAccessListRLPEncoder.Current.Encode(OneAccount()));

            var appChain = new AppChainCore(
                AppChainConfig.Default,
                blocks,
                new InMemoryTransactionStore(blocks),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                new InMemoryStateStore());

            return new TestChain(appChain, accessLists);
        }

        private static List<AccountChanges> OneAccount()
        {
            var account = new AccountChanges(Address);
            account.NonceChanges.Add(new NonceChange(0, 1));
            return new List<AccountChanges> { account };
        }

        private static Task<RpcResponseMessage> AskForBlockOneAsync(IChainNode node) =>
            new EthGetBlockAccessListHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockAccessList", "0x1"),
                new RpcContext(node, chainId: 1, services: null));

        private sealed class TestChain
        {
            public TestChain(AppChainCore appChain, IBlockAccessListStore accessLists)
            {
                AppChain = appChain;
                AccessLists = accessLists;
            }

            public AppChainCore AppChain { get; }
            public IBlockAccessListStore AccessLists { get; }
        }
    }
}

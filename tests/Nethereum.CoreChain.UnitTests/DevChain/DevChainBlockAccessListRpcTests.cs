using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainBlockAccessListRpcTests
    {
        [Fact]
        public async Task Given_AnAmsterdamDevChain_When_ARpcClientAsksForAMinedBlocksAccessList_Then_ItReceivesTheBytesTheHeaderCommitsTo()
        {
            using var node = await MineOneBlockAsync("amsterdam");

            var retained = await node.BlockAccessLists.GetByBlockNumberAsync(1);
            var header = await node.GetBlockByNumberAsync(1);
            Assert.Equal(header.BlockAccessListHash, new Sha3Keccack().CalculateHash(retained));

            var response = await AskForBlockOneAsync(node);

            Assert.False(response.HasError, response.Error?.Message);
            var accounts = Assert.IsType<List<AccountAccessDto>>(response.ResultNewtonsoft);
            Assert.Equal(6, accounts.Count);
            var historyStorage = accounts.Single(account =>
                string.Equals(account.Address, Eip2935Constants.HistoryStorageAddress, StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(historyStorage.StorageChanges);
        }

        [Fact]
        public async Task Given_APragueDevChain_When_ARpcClientAsksForAMinedBlocksAccessList_Then_ItIsResourceNotFound()
        {
            using var node = await MineOneBlockAsync("prague");

            Assert.Null((await node.GetBlockByNumberAsync(1)).BlockAccessListHash);

            var response = await AskForBlockOneAsync(node);

            Assert.True(response.HasError);
            Assert.Equal(-32001, response.Error.Code);
        }

        private static async Task<DevChainNode> MineOneBlockAsync(string hardfork)
        {
            var config = DevChainConfig.Default;
            config.Hardfork = hardfork;
            var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync();
            await node.MineBlockAsync();
            return node;
        }

        private static Task<RpcResponseMessage> AskForBlockOneAsync(DevChainNode node) =>
            new EthGetBlockAccessListHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockAccessList", "0x1"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));
    }
}

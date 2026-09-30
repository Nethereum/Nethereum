using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EthCapabilitiesHandlerTests
    {
        [Fact]
        public async Task Given_AStartedDevChain_When_AskedForEthCapabilities_Then_TheHeadAndEveryResourceAreReported()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var response = await new EthCapabilitiesHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_capabilities"),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var result = Assert.IsType<EthCapabilitiesResult>(response.ResultNewtonsoft);

            Assert.NotNull(result.Head);
            Assert.Matches("^0x[0-9a-f]{64}$", result.Head.Hash);
            Assert.NotNull(result.Head.Number);

            Assert.NotNull(result.Blocks);
            Assert.False(result.Blocks.Disabled);
            Assert.NotNull(result.Logs);
            Assert.False(result.Logs.Disabled);
            Assert.NotNull(result.Receipts);
            Assert.False(result.Receipts.Disabled);
            Assert.NotNull(result.State);
            Assert.False(result.State.Disabled);
            Assert.NotNull(result.Stateproofs);
            Assert.False(result.Stateproofs.Disabled);
            Assert.NotNull(result.Tx);
            Assert.False(result.Tx.Disabled);
        }
    }
}

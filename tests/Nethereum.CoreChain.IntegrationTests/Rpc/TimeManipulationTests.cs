using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class TimeManipulationTests
    {
        private readonly string _address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly BigInteger _chainId = 31337;
        private readonly BigInteger _initialBalance = BigInteger.Parse("10000000000000000000000");

        private async Task<(DevChainNode node, RpcDispatcher dispatcher)> CreateNodeAndDispatcher()
        {
            var config = new DevChainConfig
            {
                ChainId = _chainId,
                BlockGasLimit = 30_000_000,
                AutoMine = false
            };

            var node = new DevChainNode(config);
            await node.StartAsync(new[] { _address }, _initialBalance);

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(node, _chainId, services);
            var dispatcher = new RpcDispatcher(registry, context);

            return (node, dispatcher);
        }

        [Fact]
        public async Task EvmIncreaseTime_AddsSecondsToTimeOffset()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var request = new RpcRequestMessage(1, "evm_increaseTime", 3600);
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            Assert.Equal(3600, node.DevConfig.TimeOffset);
        }

        [Fact]
        public async Task EvmIncreaseTime_AffectsNextBlockTimestamp()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var block1 = await node.GetLatestBlockAsync();
            var block1Time = block1.Timestamp;

            var request = new RpcRequestMessage(1, "evm_increaseTime", 3600);
            await dispatcher.DispatchAsync(request);

            await node.MineBlockAsync();

            var block2 = await node.GetLatestBlockAsync();
            var block2Time = block2.Timestamp;

            Assert.True(block2Time >= block1Time + 3600,
                $"Expected block2 time ({block2Time}) to be at least 3600 seconds after block1 time ({block1Time})");
        }

        [Fact]
        public async Task EvmIncreaseTime_AccumulatesMultipleCalls()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            await dispatcher.DispatchAsync(new RpcRequestMessage(1, "evm_increaseTime", 1000));
            await dispatcher.DispatchAsync(new RpcRequestMessage(2, "evm_increaseTime", 2000));
            await dispatcher.DispatchAsync(new RpcRequestMessage(3, "evm_increaseTime", 500));

            Assert.Equal(3500, node.DevConfig.TimeOffset);
        }

        [Fact]
        public async Task EvmSetNextBlockTimestamp_SetsExactTimestamp()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            long targetTimestamp = 1700000000;
            var request = new RpcRequestMessage(1, "evm_setNextBlockTimestamp", targetTimestamp);
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.Equal(targetTimestamp, node.DevConfig.NextBlockTimestamp);
        }

        [Fact]
        public async Task EvmSetNextBlockTimestamp_AffectsNextBlock()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            long targetTimestamp = 1700000000;
            var request = new RpcRequestMessage(1, "evm_setNextBlockTimestamp", targetTimestamp);
            await dispatcher.DispatchAsync(request);

            await node.MineBlockAsync();

            var block = await node.GetLatestBlockAsync();
            Assert.Equal(targetTimestamp, block.Timestamp);
        }

        [Fact]
        public async Task EvmSetNextBlockTimestamp_ClearedAfterMining()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            long targetTimestamp = 1700000000;
            var request = new RpcRequestMessage(1, "evm_setNextBlockTimestamp", targetTimestamp);
            await dispatcher.DispatchAsync(request);

            await node.MineBlockAsync();

            Assert.Null(node.DevConfig.NextBlockTimestamp);
        }

        [Fact]
        public async Task EvmSetNextBlockTimestamp_OnlyAffectsNextBlock()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            long targetTimestamp = 1700000000;
            await dispatcher.DispatchAsync(new RpcRequestMessage(1, "evm_setNextBlockTimestamp", targetTimestamp));

            await node.MineBlockAsync();
            var block1 = await node.GetLatestBlockAsync();

            await node.MineBlockAsync();
            var block2 = await node.GetLatestBlockAsync();

            Assert.Equal(targetTimestamp, block1.Timestamp);
            Assert.NotEqual(targetTimestamp, block2.Timestamp);
            Assert.True(block2.Timestamp > block1.Timestamp);
        }
    }
}

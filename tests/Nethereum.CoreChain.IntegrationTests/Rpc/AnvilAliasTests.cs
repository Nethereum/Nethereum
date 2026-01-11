using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class AnvilAliasTests
    {
        private readonly string _address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly string _testAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
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
            await node.StartAsync(new[] { _address, _testAddress }, _initialBalance);

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();
            registry.AddAnvilAliases();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(node, _chainId, services);
            var dispatcher = new RpcDispatcher(registry, context);

            return (node, dispatcher);
        }

        [Fact]
        public async Task AnvilSetBalance_MapsToHardhatSetBalance()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var newBalanceValue = new HexBigInteger(BigInteger.Parse("1000000000000000000"));
            var request = new RpcRequestMessage(1, "anvil_setBalance", _testAddress, newBalanceValue.HexValue);
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);

            var balance = await node.GetBalanceAsync(_testAddress);
            Assert.Equal(BigInteger.Parse("1000000000000000000"), balance);
        }

        [Fact]
        public async Task AnvilSetCode_MapsToHardhatSetCode()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var code = "0x600160005260206000f3";
            var request = new RpcRequestMessage(1, "anvil_setCode", _testAddress, code);
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);

            var storedCode = await node.GetCodeAsync(_testAddress);
            Assert.Equal(code.HexToByteArray(), storedCode);
        }

        [Fact]
        public async Task AnvilSetNonce_MapsToHardhatSetNonce()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var newNonce = "0x10"; // 16
            var request = new RpcRequestMessage(1, "anvil_setNonce", _testAddress, newNonce);
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);

            var nonce = await node.GetNonceAsync(_testAddress);
            Assert.Equal(16, nonce);
        }

        [Fact]
        public async Task AnvilSetStorageAt_MapsToHardhatSetStorageAt()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var slot = "0x0";
            var value = "0x0000000000000000000000000000000000000000000000000000000000000042";
            var request = new RpcRequestMessage(1, "anvil_setStorageAt", _testAddress, slot, value);
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);

            var storedValue = await node.GetStorageAtAsync(_testAddress, 0);
            Assert.Equal(value.HexToByteArray(), storedValue);
        }

        [Fact]
        public async Task AnvilMine_MapsToEvmMine()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var blockNumberBefore = await node.GetBlockNumberAsync();

            var request = new RpcRequestMessage(1, "anvil_mine");
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);

            var blockNumberAfter = await node.GetBlockNumberAsync();
            Assert.Equal(blockNumberBefore + 1, blockNumberAfter);
        }

        [Fact]
        public async Task AnvilSnapshot_MapsToEvmSnapshot()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var request = new RpcRequestMessage(1, "anvil_snapshot");
            var response = await dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
        }

        [Fact]
        public async Task AnvilRevert_MapsToEvmRevert()
        {
            var (node, dispatcher) = await CreateNodeAndDispatcher();

            var snapshotRequest = new RpcRequestMessage(1, "anvil_snapshot");
            var snapshotResponse = await dispatcher.DispatchAsync(snapshotRequest);
            var snapshotResult = snapshotResponse.Result;
            var snapshotId = snapshotResult is JsonElement je ? je.GetString() : snapshotResult.ToString();

            await node.SetBalanceAsync(_testAddress, BigInteger.One);

            var revertRequest = new RpcRequestMessage(2, "anvil_revert", snapshotId);
            var revertResponse = await dispatcher.DispatchAsync(revertRequest);

            Assert.Null(revertResponse.Error);
            var result = revertResponse.Result;
            Assert.True(result is bool b ? b : ((JsonElement)result).GetBoolean());

            var balance = await node.GetBalanceAsync(_testAddress);
            Assert.Equal(_initialBalance, balance);
        }
    }
}

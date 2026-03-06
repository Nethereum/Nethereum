using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class HistoricalStatePruningTests : IAsyncLifetime
    {
        private DevChainNode _node;
        private RpcDispatcher _dispatcher;

        private readonly string _privateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly string _recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private readonly BigInteger _chainId = 31337;
        private readonly LegacyTransactionSigner _signer = new();

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig
            {
                ChainId = _chainId,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            var blockStore = new InMemoryBlockStore();
            var stateStore = new HistoricalStateStore(
                new InMemoryStateStore(),
                new InMemoryStateDiffStore(),
                new HistoricalStateOptions
                {
                    MaxHistoryBlocks = 5,
                    EnablePruning = true,
                    PruningIntervalBlocks = 1
                });

            _node = new DevChainNode(
                config,
                blockStore,
                new InMemoryTransactionStore(blockStore),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                stateStore,
                new InMemoryFilterStore(),
                new InMemoryTrieNodeStore());

            await _node.StartAsync(new[] { _address }, BigInteger.Parse("10000000000000000000000"));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(_node, _chainId, services);
            _dispatcher = new RpcDispatcher(registry, context);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private ISignedTransaction CreateTransfer(BigInteger value, BigInteger? nonce = null)
        {
            var txNonce = nonce ?? _node.GetNonceAsync(_address).Result;
            var signedTxHex = _signer.SignTransaction(
                _privateKey.HexToByteArray(),
                _chainId,
                _recipient,
                value,
                txNonce,
                1_000_000_000,
                21_000,
                "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        [Fact]
        public async Task QueryOutsideWindow_ReturnsError()
        {
            var blockAtStart = await _node.GetBlockNumberAsync();

            for (int i = 0; i < 10; i++)
            {
                var tx = CreateTransfer(BigInteger.Parse("100000000000000000")); // 0.1 ETH
                var result = await _node.SendTransactionAsync(tx);
                Assert.True(result.Success);
            }

            var currentBlock = await _node.GetBlockNumberAsync();
            Assert.True(currentBlock >= 10);

            var request = new RpcRequestMessage(1, "eth_getBalance",
                _recipient, new HexBigInteger(blockAtStart).HexValue);
            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("historical state not available", response.Error.Message);
        }

        [Fact]
        public async Task QueryWithinWindow_ReturnsCorrectBalance()
        {
            for (int i = 0; i < 10; i++)
            {
                var tx = CreateTransfer(BigInteger.Parse("100000000000000000"));
                var result = await _node.SendTransactionAsync(tx);
                Assert.True(result.Success);
            }

            var currentBlock = await _node.GetBlockNumberAsync();
            var recentBlock = currentBlock - 2;

            var balanceAtRecent = await _node.GetBalanceAsync(_recipient, recentBlock);
            var balanceAtLatest = await _node.GetBalanceAsync(_recipient, currentBlock);

            Assert.True(balanceAtLatest > balanceAtRecent);
        }

        [Fact]
        public async Task QueryAtBoundaryEdge_WorksCorrectly()
        {
            for (int i = 0; i < 8; i++)
            {
                var tx = CreateTransfer(BigInteger.Parse("100000000000000000"));
                var result = await _node.SendTransactionAsync(tx);
                Assert.True(result.Success);
            }

            var currentBlock = await _node.GetBlockNumberAsync();

            var boundaryBlock = currentBlock - 5;
            if (boundaryBlock > 0)
            {
                var balance = await _node.GetBalanceAsync(_recipient, boundaryBlock);
                Assert.True(balance >= 0);
            }

            var outsideBlock = currentBlock - 6;
            if (outsideBlock > 0)
            {
                await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                    () => _node.GetBalanceAsync(_recipient, outsideBlock));
            }
        }

        [Fact]
        public async Task LatestBlockQuery_AlwaysWorks()
        {
            for (int i = 0; i < 10; i++)
            {
                var tx = CreateTransfer(BigInteger.Parse("100000000000000000"));
                var result = await _node.SendTransactionAsync(tx);
                Assert.True(result.Success);
            }

            var request = new RpcRequestMessage(1, "eth_getBalance", _recipient, "latest");
            var response = await _dispatcher.DispatchAsync(request);

            Assert.Null(response.Error);
            var balance = ParseHexBigInteger(response.Result);
            Assert.True(balance > 0);
        }

        [Fact]
        public async Task ProgressiveTransfers_WindowSlidesCorrectly()
        {
            var balances = new BigInteger[12];

            for (int i = 0; i < 12; i++)
            {
                var tx = CreateTransfer(BigInteger.Parse("100000000000000000"));
                var result = await _node.SendTransactionAsync(tx);
                Assert.True(result.Success);

                var block = await _node.GetBlockNumberAsync();
                balances[i] = await _node.GetBalanceAsync(_recipient, block);
            }

            for (int i = 1; i < 12; i++)
            {
                Assert.True(balances[i] > balances[i - 1],
                    $"Balance at block {i + 1} ({balances[i]}) should be > block {i} ({balances[i - 1]})");
            }

            var currentBlock = await _node.GetBlockNumberAsync();
            var oldBlock = currentBlock - 10;
            if (oldBlock > 0)
            {
                await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                    () => _node.GetBalanceAsync(_recipient, oldBlock));
            }
        }

        private static BigInteger ParseHexBigInteger(object result)
        {
            var hex = result?.ToString();
            if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
            return hex.HexToBigInteger(false);
        }
    }
}

using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain;
using Nethereum.DevChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class HistoricalStateNoProviderTests : IAsyncLifetime
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
            var config = new DevChainConfig { ChainId = _chainId, BlockGasLimit = 30_000_000, AutoMine = true };

            var blockStore = new InMemoryBlockStore();
            var stateStore = new InMemoryStateStore();

            _node = new DevChainNode(
                config,
                blockStore,
                new InMemoryTransactionStore(blockStore),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                stateStore,
                new InMemoryFilterStore(),
                new InMemoryContentNodeStore());

            await _node.StartAsync(new[] { _address }, BigInteger.Parse("10000000000000000000000"));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            registry.AddDevHandlers();
            var services = new ServiceCollection().BuildServiceProvider();
            _dispatcher = new RpcDispatcher(registry, new RpcContext(_node, _chainId, services));
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        private ISignedTransaction CreateTransfer(BigInteger value)
        {
            var txNonce = _node.GetNonceAsync(_address).Result;
            var signedTxHex = _signer.SignTransaction(
                _privateKey.HexToByteArray(), _chainId, _recipient, value, txNonce, 1_000_000_000, 21_000, "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        private async Task<BigInteger> AdvanceAndGetHeadAsync(int blocks)
        {
            for (var i = 0; i < blocks; i++)
                Assert.True((await _node.SendTransactionAsync(CreateTransfer(BigInteger.Parse("100000000000000000")))).Success);
            return await _node.GetBlockNumberAsync();
        }

        [Fact]
        public async Task PastBlockBalance_WithoutHistoricalProvider_FailsLoudly()
        {
            var head = await AdvanceAndGetHeadAsync(3);
            var pastBlock = head - 1;

            await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                () => _node.GetBalanceAsync(_recipient, pastBlock));

            var request = new RpcRequestMessage(1, "eth_getBalance", _recipient, new HexBigInteger(pastBlock).HexValue);
            var response = await _dispatcher.DispatchAsync(request);
            Assert.NotNull(response.Error);
            Assert.Contains("historical state not available", response.Error.Message);
        }

        [Fact]
        public async Task HeadBlockBalance_WithoutHistoricalProvider_Succeeds()
        {
            var head = await AdvanceAndGetHeadAsync(3);

            var balance = await _node.GetBalanceAsync(_recipient, head);

            Assert.True(balance > BigInteger.Zero);
        }

        [Fact]
        public async Task TraceTransaction_WithoutHistoricalProvider_FailsLoudly()
        {
            var tx = CreateTransfer(BigInteger.Parse("100000000000000000"));
            Assert.True((await _node.SendTransactionAsync(tx)).Success);
            await AdvanceAndGetHeadAsync(2);

            var request = new RpcRequestMessage(1, "debug_traceTransaction", tx.Hash.ToHex(true));
            var response = await _dispatcher.DispatchAsync(request);

            Assert.NotNull(response.Error);
            Assert.Contains("historical state not available", response.Error.Message);
        }
    }
}

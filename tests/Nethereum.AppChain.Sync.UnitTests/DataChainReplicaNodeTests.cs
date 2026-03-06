using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Sequencer;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class AppChainReplicaNodeTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public async Task Constructor_InitializesProperties()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy();
            var config = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(appChain, txProxy, config);

            Assert.Equal(_chainId, replica.Config.ChainId);
            Assert.NotNull(replica.AppChain);
            Assert.Equal(SyncMode.Idle, replica.SyncMode);
            Assert.False(replica.IsSyncing);
        }

        [Fact]
        public async Task SendTransactionAsync_ForwardsToSequencer()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy();
            var config = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(appChain, txProxy, config);

            var tx = CreateSignedTransaction();
            var result = await replica.SendTransactionAsync(tx);

            Assert.True(result.Success);
            Assert.NotNull(result.TransactionHash);
            Assert.True(txProxy.SendRawTransactionCalled);
            Assert.True(txProxy.WaitForReceiptCalled);
        }

        [Fact]
        public async Task SendTransactionAsync_ReturnsErrorOnProxyFailure()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy { ShouldFail = true, FailureMessage = "connection refused" };
            var config = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(appChain, txProxy, config);

            var tx = CreateSignedTransaction();
            var result = await replica.SendTransactionAsync(tx);

            Assert.False(result.Success);
            Assert.Contains("connection refused", result.RevertReason);
        }

        [Fact]
        public async Task SendTransactionAsync_RaisesTransactionForwardedEvent()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy();
            var config = new AppChainReplicaConfig
            {
                SequencerRpcUrl = "http://localhost:8545"
            };

            using var replica = new AppChainReplicaNode(appChain, txProxy, config);

            TransactionForwardedEventArgs? eventArgs = null;
            replica.TransactionForwarded += (sender, args) => eventArgs = args;

            var tx = CreateSignedTransaction();
            await replica.SendTransactionAsync(tx);

            Assert.NotNull(eventArgs);
            Assert.Equal("http://localhost:8545", eventArgs.SequencerRpcUrl);
            Assert.NotEmpty(eventArgs.TransactionHash);
        }

        [Fact]
        public async Task GetPendingTransactionsAsync_ReturnsEmptyList()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy();
            var config = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(appChain, txProxy, config);

            var pending = await replica.GetPendingTransactionsAsync();

            Assert.NotNull(pending);
            Assert.Empty(pending);
        }

        [Fact]
        public async Task SyncMode_ReturnsIdleWhenNoSyncService()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy();
            var config = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(appChain, txProxy, config, syncService: null);

            Assert.Equal(SyncMode.Idle, replica.SyncMode);
            Assert.False(replica.IsSyncing);
        }

        [Fact]
        public void AppChainReplicaConfig_ForSequencer_SetsCorrectValues()
        {
            var url = "http://localhost:8545";
            var config = AppChainReplicaConfig.ForSequencer(url);

            Assert.Equal(url, config.SequencerRpcUrl);
            Assert.True(config.AutoStartSync);
            Assert.Equal(url, config.LiveSyncConfig.SequencerRpcUrl);
            Assert.True(config.LiveSyncConfig.AutoFollow);
        }

        [Fact]
        public void AppChainReplicaConfig_Default_HasReasonableDefaults()
        {
            var config = AppChainReplicaConfig.Default;

            Assert.Equal(30000, config.TxConfirmationTimeoutMs);
            Assert.Equal(500, config.TxPollIntervalMs);
            Assert.True(config.AutoStartSync);
            Assert.NotNull(config.SyncConfig);
            Assert.NotNull(config.LiveSyncConfig);
        }

        private async Task<IAppChain> CreateAppChainAsync()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            var config = new AppChainConfig { ChainId = _chainId };
            var appChain = new Nethereum.AppChain.AppChain(config, blockStore, txStore, receiptStore, logStore, stateStore);
            await appChain.InitializeAsync();

            return appChain;
        }

        private ISignedTransaction CreateSignedTransaction()
        {
            var key = EthECKey.GenerateKey();

            var tx = new Transaction1559(
                _chainId,
                nonce: 0,
                maxPriorityFeePerGas: 1000000000,
                maxFeePerGas: 2000000000,
                gasLimit: 21000,
                receiverAddress: "0x0000000000000000000000000000000000000001",
                amount: 1000000000000000000,
                data: "",
                accessList: null);

            var signature = key.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return tx;
        }
    }

    public class MockSequencerTxProxy : ISequencerTxProxy
    {
        public bool SendRawTransactionCalled { get; private set; }
        public bool WaitForReceiptCalled { get; private set; }
        public bool ShouldFail { get; set; }
        public string FailureMessage { get; set; } = "Error";

        private readonly byte[] _mockTxHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef".HexToByteArray();

        public Task<byte[]> SendRawTransactionAsync(byte[] rawTransaction, CancellationToken cancellationToken = default)
        {
            SendRawTransactionCalled = true;

            if (ShouldFail)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            return Task.FromResult(_mockTxHash);
        }

        public Task<ReceiptInfo?> WaitForReceiptAsync(byte[] txHash, int timeoutMs = 30000, int pollIntervalMs = 500, CancellationToken cancellationToken = default)
        {
            WaitForReceiptCalled = true;

            if (ShouldFail)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            var receipt = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>());
            var receiptInfo = new ReceiptInfo
            {
                Receipt = receipt,
                TxHash = txHash,
                BlockHash = new byte[32],
                BlockNumber = 10,
                TransactionIndex = 0,
                GasUsed = 21000
            };

            return Task.FromResult<ReceiptInfo?>(receiptInfo);
        }

        public Task<ReceiptInfo?> GetTransactionReceiptAsync(byte[] txHash, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult<ReceiptInfo?>(null);
            }

            var receipt = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>());
            var receiptInfo = new ReceiptInfo
            {
                Receipt = receipt,
                TxHash = txHash,
                BlockHash = new byte[32],
                BlockNumber = 10,
                TransactionIndex = 0,
                GasUsed = 21000
            };

            return Task.FromResult<ReceiptInfo?>(receiptInfo);
        }
    }
}

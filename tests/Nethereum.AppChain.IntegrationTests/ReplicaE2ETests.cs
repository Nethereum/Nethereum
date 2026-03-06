using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class ReplicaE2ETests : IAsyncLifetime, IDisposable
    {
        private AppChainCore? _sequencerChain;
        private AppChainCore? _replicaChain;
        private Sequencer.Sequencer? _sequencer;

        private const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _sequencerAddress;
        private static readonly BigInteger ChainId = new BigInteger(420420);

        public ReplicaE2ETests()
        {
            var sequencerKey = new EthECKey(SequencerPrivateKey);
            _sequencerAddress = sequencerKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            var sequencerBlockStore = new InMemoryBlockStore();
            var sequencerTxStore = new InMemoryTransactionStore(sequencerBlockStore);
            var sequencerReceiptStore = new InMemoryReceiptStore();
            var sequencerLogStore = new InMemoryLogStore();
            var sequencerStateStore = new InMemoryStateStore();

            var sequencerConfig = AppChainConfig.CreateWithName("SequencerChain", (int)ChainId);
            sequencerConfig.SequencerAddress = _sequencerAddress;

            _sequencerChain = new AppChainCore(sequencerConfig, sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, sequencerLogStore, sequencerStateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { _sequencerAddress },
                PrefundBalance = BigInteger.Parse("10000000000000000000000"),
                DeployCreate2Factory = false
            };
            await _sequencerChain.InitializeAsync(genesisOptions);

            var replicaBlockStore = new InMemoryBlockStore();
            var replicaTxStore = new InMemoryTransactionStore(replicaBlockStore);
            var replicaReceiptStore = new InMemoryReceiptStore();
            var replicaLogStore = new InMemoryLogStore();
            var replicaStateStore = new InMemoryStateStore();

            var replicaConfig = AppChainConfig.CreateWithName("ReplicaChain", (int)ChainId);
            _replicaChain = new AppChainCore(replicaConfig, replicaBlockStore, replicaTxStore, replicaReceiptStore, replicaLogStore, replicaStateStore);
            await _replicaChain.InitializeAsync();
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _sequencer = null;
            _sequencerChain = null;
            _replicaChain = null;
        }

        [Fact]
        public async Task E2E_ReplicaNode_ForwardsTransactionToSequencer()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            var txProxy = new InProcessSequencerTxProxy(_sequencer);
            var replicaConfig = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(_replicaChain!, txProxy, replicaConfig);

            var tx = CreateSignedTransaction(nonce: 0);
            var result = await replica.SendTransactionAsync(tx);

            Assert.True(result.Success);
            Assert.NotNull(result.TransactionHash);

            var sequencerHeight = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(1, sequencerHeight);

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_ReplicaNode_ReportsCorrectSyncStatus()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            var txProxy = new InProcessSequencerTxProxy(_sequencer);
            var replicaConfig = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(_replicaChain!, txProxy, replicaConfig);

            var handler = new ReplicaSyncStatusHandler();
            var request = new RpcRequestMessage(1, "replica_syncStatus");
            var context = new RpcContext(replica, ChainId, new MockServiceProvider());

            var response = await handler.HandleAsync(request, context);
            var status = response.GetResultSTJ<ReplicaSyncStatus>();

            Assert.NotNull(status);
            Assert.True(status.IsReplica);
            Assert.False(status.Syncing);
            Assert.Equal("Idle", status.SyncMode);

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_ReplicaNode_RaisesTransactionForwardedEvent()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            var txProxy = new InProcessSequencerTxProxy(_sequencer);
            var replicaConfig = new AppChainReplicaConfig
            {
                SequencerRpcUrl = "http://test-sequencer:8545"
            };

            using var replica = new AppChainReplicaNode(_replicaChain!, txProxy, replicaConfig);

            TransactionForwardedEventArgs? eventArgs = null;
            replica.TransactionForwarded += (sender, args) => eventArgs = args;

            var tx = CreateSignedTransaction(nonce: 0);
            await replica.SendTransactionAsync(tx);

            Assert.NotNull(eventArgs);
            Assert.Equal("http://test-sequencer:8545", eventArgs.SequencerRpcUrl);
            Assert.NotEmpty(eventArgs.TransactionHash);

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_ReplicaNode_ReturnsEmptyPendingTransactions()
        {
            var txProxy = new InProcessSequencerTxProxy(null);
            var replicaConfig = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(_replicaChain!, txProxy, replicaConfig);

            var pending = await replica.GetPendingTransactionsAsync();

            Assert.NotNull(pending);
            Assert.Empty(pending);
        }

        [Fact]
        public async Task E2E_ReplicaNode_ForwardsMultipleTransactions()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            var txProxy = new InProcessSequencerTxProxy(_sequencer);
            var replicaConfig = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(_replicaChain!, txProxy, replicaConfig);

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                var result = await replica.SendTransactionAsync(tx);
                Assert.True(result.Success);
            }

            var sequencerHeight = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(5, sequencerHeight);

            await _sequencer.StopAsync();
        }

        private ISignedTransaction CreateSignedTransaction(int nonce = 0)
        {
            var privateKey = new EthECKey(SequencerPrivateKey);

            var transaction = new Transaction1559(
                chainId: ChainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(21000),
                receiverAddress: "0x0000000000000000000000000000000000000001",
                amount: BigInteger.Zero,
                data: null,
                accessList: null
            );

            var signature = privateKey.SignAndCalculateYParityV(transaction.RawHash);
            transaction.SetSignature(new Signature
            {
                R = signature.R,
                S = signature.S,
                V = signature.V
            });

            return transaction;
        }
    }

    public class InProcessSequencerTxProxy : ISequencerTxProxy
    {
        private readonly Sequencer.Sequencer? _sequencer;
        private readonly Nethereum.Util.Sha3Keccack _keccak = new();

        public InProcessSequencerTxProxy(Sequencer.Sequencer? sequencer)
        {
            _sequencer = sequencer;
        }

        public async Task<byte[]> SendRawTransactionAsync(byte[] rawTransaction, CancellationToken cancellationToken = default)
        {
            if (_sequencer == null)
                throw new InvalidOperationException("Sequencer not available");

            var tx = TransactionFactory.CreateTransaction(rawTransaction);
            var txHash = await _sequencer.SubmitTransactionAsync(tx);

            return txHash;
        }

        public async Task<ReceiptInfo?> WaitForReceiptAsync(byte[] txHash, int timeoutMs = 30000, int pollIntervalMs = 500, CancellationToken cancellationToken = default)
        {
            if (_sequencer == null)
                return null;

            var receipt = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new System.Collections.Generic.List<Log>());
            return new ReceiptInfo
            {
                Receipt = receipt,
                TxHash = txHash,
                BlockHash = new byte[32],
                BlockNumber = await _sequencer.GetBlockNumberAsync(),
                TransactionIndex = 0,
                GasUsed = 21000
            };
        }

        public Task<ReceiptInfo?> GetTransactionReceiptAsync(byte[] txHash, CancellationToken cancellationToken = default)
        {
            return WaitForReceiptAsync(txHash, 0, 0, cancellationToken);
        }
    }

    public class MockServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}

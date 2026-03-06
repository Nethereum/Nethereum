using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class LiveSyncE2ETests : IAsyncLifetime, IDisposable
    {
        private AppChainCore? _sequencerChain;
        private AppChainCore? _replicaChain;
        private Sequencer.Sequencer? _sequencer;

        private const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _sequencerAddress;
        private static readonly BigInteger ChainId = new BigInteger(420420);

        public LiveSyncE2ETests()
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

        private (MultiPeerSyncService sync, PeerManager peerManager) CreateSyncService(
            InProcessSequencerRpcClient mockRpcClient,
            InMemoryFinalityTracker finalityTracker)
        {
            var peerManager = new PeerManager(clientFactory: url => mockRpcClient);
            peerManager.AddPeer("http://localhost:8545");
            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;

            var config = new MultiPeerSyncConfig { AutoFollow = false };
            var sync = new MultiPeerSyncService(
                config,
                _replicaChain!.Blocks,
                _replicaChain.Transactions,
                _replicaChain.Receipts,
                _replicaChain.Logs,
                finalityTracker,
                peerManager);

            return (sync, peerManager);
        }

        [Fact]
        public async Task E2E_LiveSync_SyncsBlocksFromSequencer()
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

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            var sequencerHeight = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(5, sequencerHeight);

            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain!);
            var finalityTracker = new InMemoryFinalityTracker();

            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker);
            peerManager.Peers.First().BlockNumber = 5;

            await liveSync.StartAsync();
            var result = await liveSync.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(6, result.BlocksSynced);

            var replicaHeight = await _replicaChain!.Blocks.GetHeightAsync();
            Assert.Equal(5, replicaHeight);

            Assert.True(await finalityTracker.IsSoftAsync(1));
            Assert.True(await finalityTracker.IsSoftAsync(5));

            await liveSync.StopAsync();
            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_LiveSync_MarksBlocksAsSoft()
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

            for (int i = 0; i < 3; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain!);
            var finalityTracker = new InMemoryFinalityTracker();

            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker);
            peerManager.Peers.First().BlockNumber = 3;

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();

            Assert.False(await finalityTracker.IsFinalizedAsync(1));
            Assert.True(await finalityTracker.IsSoftAsync(1));
            Assert.True(await finalityTracker.IsSoftAsync(3));

            await liveSync.StopAsync();
            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_LiveSync_IncrementalSync()
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

            for (int i = 0; i < 3; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain!);
            var finalityTracker = new InMemoryFinalityTracker();

            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker);
            peerManager.Peers.First().BlockNumber = 3;

            await liveSync.StartAsync();
            var result1 = await liveSync.SyncToLatestAsync();
            Assert.Equal(4, result1.BlocksSynced);

            for (int i = 3; i < 6; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            // Update peer with new block height
            peerManager.Peers.First().BlockNumber = 6;

            var result2 = await liveSync.SyncToLatestAsync();
            Assert.Equal(3, result2.BlocksSynced);

            var replicaHeight = await _replicaChain!.Blocks.GetHeightAsync();
            Assert.Equal(6, replicaHeight);

            await liveSync.StopAsync();
            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_LiveSync_FiresBlockImportedEvents()
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

            for (int i = 0; i < 3; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain!);
            var finalityTracker = new InMemoryFinalityTracker();

            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker);
            peerManager.Peers.First().BlockNumber = 3;

            var importedBlocks = new System.Collections.Generic.List<BigInteger>();
            liveSync.BlockImported += (sender, args) => importedBlocks.Add(args.BlockNumber);

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();

            Assert.Equal(4, importedBlocks.Count);
            Assert.Contains((BigInteger)0, importedBlocks);
            Assert.Contains((BigInteger)1, importedBlocks);
            Assert.Contains((BigInteger)2, importedBlocks);
            Assert.Contains((BigInteger)3, importedBlocks);

            await liveSync.StopAsync();
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
            transaction.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return transaction;
        }
    }

    public class InProcessSequencerRpcClient : ISequencerRpcClient
    {
        private readonly AppChainCore _appChain;
        private readonly Nethereum.Util.Sha3Keccack _keccak = new();

        public InProcessSequencerRpcClient(AppChainCore appChain)
        {
            _appChain = appChain;
        }

        public async Task<BigInteger> GetBlockNumberAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return await _appChain.GetBlockNumberAsync();
        }

        public async Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, System.Threading.CancellationToken cancellationToken = default)
        {
            var header = await _appChain.Blocks.GetByNumberAsync((long)blockNumber);
            if (header == null)
                return null;

            var blockHash = await _appChain.Blocks.GetHashByNumberAsync((long)blockNumber);
            var transactions = await _appChain.Transactions.GetByBlockHashAsync(blockHash);
            var receipts = await _appChain.Receipts.GetByBlockNumberAsync((long)blockNumber);

            return new LiveBlockData
            {
                Header = header,
                BlockHash = blockHash,
                Transactions = transactions ?? new System.Collections.Generic.List<ISignedTransaction>(),
                Receipts = receipts ?? new System.Collections.Generic.List<Receipt>(),
                IsSoft = true
            };
        }

        public async Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, System.Threading.CancellationToken cancellationToken = default)
        {
            return await _appChain.Blocks.GetByNumberAsync((long)blockNumber);
        }

        public async Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, System.Threading.CancellationToken cancellationToken = default)
        {
            return await _appChain.Blocks.GetHashByNumberAsync((long)blockNumber);
        }
    }
}

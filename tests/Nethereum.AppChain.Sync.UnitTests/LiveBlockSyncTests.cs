using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class SinglePeerSyncTests
    {
        private readonly BigInteger _chainId = 420420;

        private (MultiPeerSyncService sync, PeerManager peerManager) CreateSyncService(
            MockSequencerRpcClient mockClient,
            InMemoryBlockStore blockStore,
            InMemoryTransactionStore txStore,
            InMemoryReceiptStore receiptStore,
            InMemoryLogStore logStore,
            InMemoryFinalityTracker finalityTracker)
        {
            var peerManager = new PeerManager(clientFactory: url => mockClient);
            peerManager.AddPeer("http://localhost:8545");
            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;

            var config = new MultiPeerSyncConfig { AutoFollow = false };
            var sync = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            return (sync, peerManager);
        }

        [Fact]
        public async Task SyncToLatestAsync_SyncsAllNewBlocks()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 10);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            // Update peer with block height
            peerManager.Peers.First().BlockNumber = 9;

            await liveSync.StartAsync();
            var result = await liveSync.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(10, result.BlocksSynced);
            Assert.Equal(9, result.EndBlock);

            var localHeight = await blockStore.GetHeightAsync();
            Assert.Equal(9, localHeight);

            Assert.Equal(9, finalityTracker.LastSoftBlock);
        }

        [Fact]
        public async Task SyncToBlockAsync_SyncsToSpecificBlock()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 20);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            peerManager.Peers.First().BlockNumber = 19;

            await liveSync.StartAsync();
            var result = await liveSync.SyncToBlockAsync(5);

            Assert.True(result.Success);
            Assert.Equal(6, result.BlocksSynced);
            Assert.Equal(5, result.EndBlock);
        }

        [Fact]
        public async Task SyncToLatestAsync_SkipsWhenAlreadySynced()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 5);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            peerManager.Peers.First().BlockNumber = 4;

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();
            var result = await liveSync.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(0, result.BlocksSynced);
        }

        [Fact]
        public async Task BlockImported_EventFires_ForEachBlock()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 3);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            peerManager.Peers.First().BlockNumber = 2;

            var importedBlocks = new List<BigInteger>();
            liveSync.BlockImported += (sender, args) => importedBlocks.Add(args.BlockNumber);

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();

            Assert.Equal(3, importedBlocks.Count);
            Assert.Contains(0, importedBlocks);
            Assert.Contains(1, importedBlocks);
            Assert.Contains(2, importedBlocks);
        }

        [Fact]
        public async Task SyncMarksBlocksAsSoft()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 5);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            peerManager.Peers.First().BlockNumber = 4;

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();

            Assert.True(await finalityTracker.IsSoftAsync(0));
            Assert.True(await finalityTracker.IsSoftAsync(4));
            Assert.False(await finalityTracker.IsFinalizedAsync(0));
        }

        [Fact]
        public async Task State_TransitionsDuringSyncing()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 3);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            peerManager.Peers.First().BlockNumber = 2;

            Assert.Equal(LiveSyncState.Idle, liveSync.State);

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();

            Assert.Equal(LiveSyncState.Idle, liveSync.State);
        }

        [Fact]
        public async Task SyncStoresTransactionsAndReceipts()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockRpcClient = new MockSequencerRpcClient();
            await PopulateMockClient(mockRpcClient, 3);

            var (liveSync, peerManager) = CreateSyncService(
                mockRpcClient, blockStore, txStore, receiptStore, logStore, finalityTracker);

            peerManager.Peers.First().BlockNumber = 2;

            await liveSync.StartAsync();
            await liveSync.SyncToLatestAsync();

            var blockHash = await blockStore.GetHashByNumberAsync(0);
            var transactions = await txStore.GetByBlockHashAsync(blockHash);

            Assert.NotNull(transactions);
            Assert.Equal(2, transactions.Count);

            var receipts = await receiptStore.GetByBlockNumberAsync(0);
            Assert.NotNull(receipts);
            Assert.Equal(2, receipts.Count);
        }

        private async Task PopulateMockClient(MockSequencerRpcClient client, int blockCount)
        {
            var key = Nethereum.Signer.EthECKey.GenerateKey();
            var keccak = new Nethereum.Util.Sha3Keccack();
            var prevHash = new byte[32];

            for (int i = 0; i < blockCount; i++)
            {
                var transactions = new List<ISignedTransaction>();
                var receipts = new List<Receipt>();

                for (int t = 0; t < 2; t++)
                {
                    var tx = new Transaction1559(
                        _chainId,
                        nonce: i * 2 + t,
                        maxPriorityFeePerGas: 1000000000,
                        maxFeePerGas: 2000000000,
                        gasLimit: 21000,
                        receiverAddress: "0x0000000000000000000000000000000000000002",
                        amount: 1000000000000000000,
                        data: "",
                        accessList: null);

                    var signature = key.SignAndCalculateYParityV(tx.RawHash);
                    tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
                    transactions.Add(tx);

                    var receipt = Receipt.CreateStatusReceipt(true, (t + 1) * 21000, new byte[256], new List<Log>());
                    receipts.Add(receipt);
                }

                var header = new BlockHeader
                {
                    ParentHash = prevHash,
                    UnclesHash = new byte[32],
                    Coinbase = "0x0000000000000000000000000000000000000001",
                    StateRoot = new byte[32],
                    TransactionsHash = new byte[32],
                    ReceiptHash = new byte[32],
                    LogsBloom = new byte[256],
                    Difficulty = 1,
                    BlockNumber = i,
                    GasLimit = 30000000,
                    GasUsed = 42000,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + i,
                    ExtraData = new byte[0],
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    BaseFee = 1000000000
                };

                var blockHash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));

                client.AddBlock(new LiveBlockData
                {
                    Header = header,
                    BlockHash = blockHash,
                    Transactions = transactions,
                    Receipts = receipts,
                    IsSoft = true
                });

                prevHash = blockHash;
            }
        }
    }

    public class MockSequencerRpcClient : ISequencerRpcClient
    {
        private readonly Dictionary<BigInteger, LiveBlockData> _blocks = new();

        public void AddBlock(LiveBlockData block)
        {
            _blocks[block.Header.BlockNumber] = block;
        }

        public Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default)
        {
            if (_blocks.Count == 0)
                return Task.FromResult(BigInteger.MinusOne);

            return Task.FromResult(_blocks.Keys.Max());
        }

        public Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            _blocks.TryGetValue(blockNumber, out var block);
            return Task.FromResult(block);
        }

        public Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            if (_blocks.TryGetValue(blockNumber, out var block))
                return Task.FromResult<BlockHeader?>(block.Header);

            return Task.FromResult<BlockHeader?>(null);
        }

        public Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            if (_blocks.TryGetValue(blockNumber, out var block))
                return Task.FromResult<byte[]?>(block.BlockHash);

            return Task.FromResult<byte[]?>(null);
        }
    }
}

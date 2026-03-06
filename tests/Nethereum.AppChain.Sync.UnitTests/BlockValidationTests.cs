using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class BlockValidationTests
    {
        private readonly Sha3Keccack _keccak = new();

        private MultiPeerSyncService CreateSyncService(
            MockSequencerRpcClient mockClient,
            out InMemoryBlockStore blockStore)
        {
            blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var peerManager = new PeerManager(clientFactory: _ => mockClient);
            peerManager.AddPeer("http://localhost:8545");
            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;
            peers[0].BlockNumber = 10;

            var config = new MultiPeerSyncConfig { AutoFollow = false };
            return new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);
        }

        private LiveBlockData CreateBlock(int blockNumber, byte[] parentHash)
        {
            var header = new BlockHeader
            {
                ParentHash = parentHash,
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000001",
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                Difficulty = 1,
                BlockNumber = blockNumber,
                GasLimit = 30000000,
                GasUsed = 0,
                Timestamp = 1000000 + blockNumber,
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = 1000000000
            };

            var blockHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));

            return new LiveBlockData
            {
                Header = header,
                BlockHash = blockHash,
                Transactions = new List<ISignedTransaction>(),
                Receipts = new List<Receipt>(),
                IsSoft = true
            };
        }

        [Fact]
        public async Task Sync_BlockHashMismatch_ThrowsInvalidBlockException()
        {
            var mockClient = new MockSequencerRpcClient();

            var block0 = CreateBlock(0, new byte[32]);
            mockClient.AddBlock(block0);

            var block1 = CreateBlock(1, block0.BlockHash);
            block1.BlockHash = new byte[32]; // Wrong hash
            block1.BlockHash[0] = 0xFF;
            mockClient.AddBlock(block1);

            var syncService = CreateSyncService(mockClient, out _);
            await syncService.StartAsync();

            var result = await syncService.SyncToLatestAsync();

            Assert.False(result.Success);
            Assert.Contains("hash mismatch", result.ErrorMessage);
        }

        [Fact]
        public async Task Sync_ParentHashMismatch_ThrowsInvalidBlockException()
        {
            var mockClient = new MockSequencerRpcClient();

            var block0 = CreateBlock(0, new byte[32]);
            mockClient.AddBlock(block0);

            var block1 = CreateBlock(1, block0.BlockHash);
            mockClient.AddBlock(block1);

            // Block 2 has wrong parent hash (points to genesis instead of block1)
            var block2 = CreateBlock(2, block0.BlockHash); // Should be block1.BlockHash
            mockClient.AddBlock(block2);

            var syncService = CreateSyncService(mockClient, out _);
            await syncService.StartAsync();

            var result = await syncService.SyncToLatestAsync();

            Assert.False(result.Success);
            Assert.Contains("parent hash mismatch", result.ErrorMessage);
        }

        [Fact]
        public async Task Sync_NullBlockHash_UsesComputedHash()
        {
            var mockClient = new MockSequencerRpcClient();

            var block0 = CreateBlock(0, new byte[32]);
            block0.BlockHash = null; // Null hash should be computed
            mockClient.AddBlock(block0);

            var block1ParentHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block0.Header));
            var block1 = CreateBlock(1, block1ParentHash);
            block1.BlockHash = null;
            mockClient.AddBlock(block1);

            var syncService = CreateSyncService(mockClient, out var blockStore);
            await syncService.StartAsync();

            var result = await syncService.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(1, result.EndBlock);

            var storedHash = await blockStore.GetHashByNumberAsync(0);
            Assert.NotNull(storedHash);
            Assert.Equal(32, storedHash.Length);
        }

        [Fact]
        public async Task Sync_ValidChain_ImportsAllBlocks()
        {
            var mockClient = new MockSequencerRpcClient();

            var prevHash = new byte[32];
            for (int i = 0; i < 5; i++)
            {
                var block = CreateBlock(i, prevHash);
                mockClient.AddBlock(block);
                prevHash = block.BlockHash;
            }

            var syncService = CreateSyncService(mockClient, out var blockStore);
            await syncService.StartAsync();

            var result = await syncService.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(4, result.EndBlock);

            for (int i = 0; i < 5; i++)
            {
                var hash = await blockStore.GetHashByNumberAsync(i);
                Assert.NotNull(hash);
            }
        }
    }
}

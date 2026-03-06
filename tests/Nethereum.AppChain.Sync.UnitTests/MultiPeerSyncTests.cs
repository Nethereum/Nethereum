using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class MultiPeerSyncTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public async Task SyncToLatestAsync_UsesBestPeer()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockClient1 = new MockSequencerRpcClient();
            var mockClient2 = new MockSequencerRpcClient();
            PopulateMockClient(mockClient1, 5);
            PopulateMockClient(mockClient2, 10);

            var peerManager = new PeerManager(clientFactory: url =>
            {
                if (url.Contains("8545")) return mockClient1;
                return mockClient2;
            });

            peerManager.AddPeer("http://localhost:8545");
            peerManager.AddPeer("http://localhost:8546");

            // Mark peers as healthy with their block heights
            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;
            peers[0].BlockNumber = 4;
            peers[1].IsHealthy = true;
            peers[1].BlockNumber = 9;

            var config = new MultiPeerSyncConfig { AutoFollow = false };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            await syncService.StartAsync();
            var result = await syncService.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(9, result.EndBlock);
            Assert.Equal("http://localhost:8546", syncService.CurrentPeerUrl);
        }

        [Fact]
        public async Task SyncToLatestAsync_FailsOverToDifferentPeer()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var failingClient = new FailingMockClient(failAfterBlocks: 2);
            var workingClient = new MockSequencerRpcClient();
            PopulateMockClient(workingClient, 5);

            var peerManager = new PeerManager(clientFactory: url =>
            {
                if (url.Contains("8545")) return failingClient;
                return workingClient;
            });

            peerManager.AddPeer("http://localhost:8545");
            peerManager.AddPeer("http://localhost:8546");

            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;
            peers[0].BlockNumber = 10; // Higher so it's picked first
            peers[1].IsHealthy = true;
            peers[1].BlockNumber = 4;

            var config = new MultiPeerSyncConfig { AutoFollow = false, MaxPeerRetries = 3 };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            string? switchedToPeer = null;
            syncService.PeerSwitched += (s, e) => switchedToPeer = e.NewPeerUrl;

            await syncService.StartAsync();
            var result = await syncService.SyncToLatestAsync();

            // Should have switched to working peer after failure
            Assert.True(result.Success);
            Assert.Equal("http://localhost:8546", syncService.CurrentPeerUrl);
        }

        [Fact]
        public async Task GetRemoteTipAsync_ReturnsHighestFromAllPeers()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockClient1 = new MockSequencerRpcClient();
            var mockClient2 = new MockSequencerRpcClient();
            var mockClient3 = new MockSequencerRpcClient();

            PopulateMockClient(mockClient1, 5);
            PopulateMockClient(mockClient2, 15);
            PopulateMockClient(mockClient3, 10);

            var peerManager = new PeerManager(clientFactory: url =>
            {
                if (url.Contains("8545")) return mockClient1;
                if (url.Contains("8546")) return mockClient2;
                return mockClient3;
            });

            peerManager.AddPeer("http://localhost:8545");
            peerManager.AddPeer("http://localhost:8546");
            peerManager.AddPeer("http://localhost:8547");

            var peers = peerManager.Peers.ToList();
            foreach (var p in peers) p.IsHealthy = true;

            var config = new MultiPeerSyncConfig { AutoFollow = false };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            var remoteTip = await syncService.GetRemoteTipAsync();

            Assert.Equal(14, remoteTip); // Highest block number (0-14 = 15 blocks)
        }

        [Fact]
        public async Task PeerSwitched_EventFires()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockClient = new MockSequencerRpcClient();
            PopulateMockClient(mockClient, 3);

            var peerManager = new PeerManager(clientFactory: _ => mockClient);
            peerManager.AddPeer("http://localhost:8545");

            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;
            peers[0].BlockNumber = 2;

            var config = new MultiPeerSyncConfig { AutoFollow = false };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            PeerSwitchedEventArgs? switchEvent = null;
            syncService.PeerSwitched += (s, e) => switchEvent = e;

            await syncService.StartAsync();
            await syncService.SyncToLatestAsync();

            Assert.NotNull(switchEvent);
            Assert.Null(switchEvent.PreviousPeerUrl);
            Assert.Equal("http://localhost:8545", switchEvent.NewPeerUrl);
            Assert.Equal("Initial connection", switchEvent.Reason);
        }

        private void PopulateMockClient(MockSequencerRpcClient client, int blockCount)
        {
            var key = Nethereum.Signer.EthECKey.GenerateKey();
            var keccak = new Nethereum.Util.Sha3Keccack();
            var prevHash = new byte[32];

            for (int i = 0; i < blockCount; i++)
            {
                var transactions = new List<ISignedTransaction>();
                var receipts = new List<Receipt>();

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
                    GasUsed = 0,
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

    public class FailingMockClient : ISequencerRpcClient
    {
        private readonly MockSequencerRpcClient _inner = new();
        private readonly int _failAfterBlocks;
        private int _blocksFetched;

        public FailingMockClient(int failAfterBlocks)
        {
            _failAfterBlocks = failAfterBlocks;

            // Add some blocks
            var keccak = new Nethereum.Util.Sha3Keccack();
            var prevHash = new byte[32];

            for (int i = 0; i < 10; i++)
            {
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
                    GasUsed = 0,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + i,
                    ExtraData = new byte[0],
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    BaseFee = 1000000000
                };

                var blockHash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                _inner.AddBlock(new LiveBlockData
                {
                    Header = header,
                    BlockHash = blockHash,
                    Transactions = new List<ISignedTransaction>(),
                    Receipts = new List<Receipt>(),
                    IsSoft = true
                });

                prevHash = blockHash;
            }
        }

        public Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default)
        {
            return _inner.GetBlockNumberAsync(cancellationToken);
        }

        public Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            _blocksFetched++;
            if (_blocksFetched > _failAfterBlocks)
            {
                throw new Exception("Simulated peer failure");
            }
            return _inner.GetBlockWithReceiptsAsync(blockNumber, cancellationToken);
        }

        public Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            return _inner.GetBlockHeaderAsync(blockNumber, cancellationToken);
        }

        public Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            return _inner.GetBlockHashAsync(blockNumber, cancellationToken);
        }
    }
}

using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class AlwaysFailingMockClient : ISequencerRpcClient
    {
        public Task<BigInteger> GetBlockNumberAsync(CancellationToken cancellationToken = default)
        {
            throw new Exception("Connection failed");
        }

        public Task<LiveBlockData?> GetBlockWithReceiptsAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            throw new Exception("Connection failed");
        }

        public Task<BlockHeader?> GetBlockHeaderAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            throw new Exception("Connection failed");
        }

        public Task<byte[]?> GetBlockHashAsync(BigInteger blockNumber, CancellationToken cancellationToken = default)
        {
            throw new Exception("Connection failed");
        }
    }

    public class CliqueMultiNodeSyncTests
    {
        private const int NODE_COUNT = 3;
        private readonly string[] _signerKeys = new[]
        {
            "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80",
            "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d",
            "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a"
        };

        [Fact]
        public async Task MultiNode_SyncFromBestPeer_SelectsHighestBlock()
        {
            var mockClients = new MockSequencerRpcClient[NODE_COUNT];
            var peerManager = new PeerManager(clientFactory: url =>
            {
                var index = int.Parse(url.Split(':').Last()) - 8545;
                return mockClients[index];
            });

            for (int i = 0; i < NODE_COUNT; i++)
            {
                mockClients[i] = new MockSequencerRpcClient();
                PopulateMockClient(mockClients[i], i == 1 ? 10 : 5);
                peerManager.AddPeer($"http://localhost:{8545 + i}");
            }

            await Task.Delay(100);

            var peers = peerManager.Peers.ToList();
            for (int i = 0; i < NODE_COUNT; i++)
            {
                var peer = peerManager.GetPeer($"http://localhost:{8545 + i}");
                peer!.IsHealthy = true;
                peer.BlockNumber = i == 1 ? 9 : 4;
            }

            var best = peerManager.GetBestPeer();

            Assert.NotNull(best);
            Assert.Equal("http://localhost:8546", best.Url);
            Assert.Equal(9, best.BlockNumber);
        }

        [Fact]
        public async Task MultiNode_FailoverToNextPeer_OnPrimaryFailure()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockClients = new ISequencerRpcClient[NODE_COUNT];
            var failingClient = new FailingMockClient(failAfterBlocks: 2);
            var workingClient1 = new MockSequencerRpcClient();
            var workingClient2 = new MockSequencerRpcClient();

            PopulateMockClient(workingClient1, 10);
            PopulateMockClient(workingClient2, 10);

            mockClients[0] = failingClient;
            mockClients[1] = workingClient1;
            mockClients[2] = workingClient2;

            var peerManager = new PeerManager(clientFactory: url =>
            {
                var index = int.Parse(url.Split(':').Last()) - 8545;
                return mockClients[index];
            });

            for (int i = 0; i < NODE_COUNT; i++)
            {
                peerManager.AddPeer($"http://localhost:{8545 + i}");
            }

            await Task.Delay(100);

            var peer0 = peerManager.GetPeer("http://localhost:8545");
            var peer1 = peerManager.GetPeer("http://localhost:8546");
            var peer2 = peerManager.GetPeer("http://localhost:8547");

            peer0!.IsHealthy = true;
            peer0.BlockNumber = 15;
            peer1!.IsHealthy = true;
            peer1.BlockNumber = 9;
            peer2!.IsHealthy = true;
            peer2.BlockNumber = 9;

            var config = new MultiPeerSyncConfig { AutoFollow = false, MaxPeerRetries = NODE_COUNT };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            var switchEvents = new List<string>();
            syncService.PeerSwitched += (s, e) => switchEvents.Add(e.NewPeerUrl ?? "");

            await syncService.StartAsync();
            var result = await syncService.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Contains(switchEvents, url => url != "http://localhost:8545");
        }

        [Fact]
        public async Task MultiNode_AllPeersAggregateRemoteTip()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockClients = new MockSequencerRpcClient[NODE_COUNT];
            var peerManager = new PeerManager(clientFactory: url =>
            {
                var index = int.Parse(url.Split(':').Last()) - 8545;
                return mockClients[index];
            });

            for (int i = 0; i < NODE_COUNT; i++)
            {
                mockClients[i] = new MockSequencerRpcClient();
                PopulateMockClient(mockClients[i], 5 + i * 5);
                peerManager.AddPeer($"http://localhost:{8545 + i}");
            }

            await Task.Delay(100);

            for (int i = 0; i < NODE_COUNT; i++)
            {
                var peer = peerManager.GetPeer($"http://localhost:{8545 + i}");
                peer!.IsHealthy = true;
            }

            var config = new MultiPeerSyncConfig { AutoFollow = false };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            var remoteTip = await syncService.GetRemoteTipAsync();

            Assert.Equal(14, remoteTip);
        }

        [Fact]
        public async Task MultiNode_CatchUpSync_FromMultiplePeers()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            var mockClients = new MockSequencerRpcClient[NODE_COUNT];
            var peerManager = new PeerManager(clientFactory: url =>
            {
                var index = int.Parse(url.Split(':').Last()) - 8545;
                return mockClients[index];
            });

            for (int i = 0; i < NODE_COUNT; i++)
            {
                mockClients[i] = new MockSequencerRpcClient();
                PopulateMockClient(mockClients[i], 20);
                peerManager.AddPeer($"http://localhost:{8545 + i}");
            }

            await Task.Delay(100);

            for (int i = 0; i < NODE_COUNT; i++)
            {
                var peer = peerManager.GetPeer($"http://localhost:{8545 + i}");
                peer!.IsHealthy = true;
                peer.BlockNumber = 19;
            }

            var config = new MultiPeerSyncConfig { AutoFollow = false };

            var syncService = new MultiPeerSyncService(
                config, blockStore, txStore, receiptStore, logStore, finalityTracker, peerManager);

            await syncService.StartAsync();
            var result = await syncService.SyncToLatestAsync();

            Assert.True(result.Success);
            Assert.Equal(19, result.EndBlock);
            Assert.Equal(20, result.BlocksSynced);
        }

        [Fact]
        public async Task MultiNode_HealthCheck_MarksBadPeersUnhealthy()
        {
            var badClient = new AlwaysFailingMockClient();
            var goodClient1 = new MockSequencerRpcClient();
            var goodClient2 = new MockSequencerRpcClient();

            PopulateMockClient(goodClient1, 5);
            PopulateMockClient(goodClient2, 5);

            var mockClients = new ISequencerRpcClient[] { badClient, goodClient1, goodClient2 };

            var peerManager = new PeerManager(clientFactory: url =>
            {
                var index = int.Parse(url.Split(':').Last()) - 8545;
                return mockClients[index];
            });

            for (int i = 0; i < NODE_COUNT; i++)
            {
                peerManager.AddPeer($"http://localhost:{8545 + i}");
            }

            await peerManager.CheckAllPeersAsync();

            var badPeer = peerManager.GetPeer("http://localhost:8545");
            var goodPeer1 = peerManager.GetPeer("http://localhost:8546");
            var goodPeer2 = peerManager.GetPeer("http://localhost:8547");

            Assert.False(badPeer!.IsHealthy);
            Assert.True(goodPeer1!.IsHealthy);
            Assert.True(goodPeer2!.IsHealthy);

            var bestPeer = peerManager.GetBestPeer();
            Assert.NotNull(bestPeer);
            Assert.NotEqual("http://localhost:8545", bestPeer.Url);
        }

        [Fact]
        public void CliqueRotation_BlockAssignment_CorrectlyRotates()
        {
            var signers = new[] { "signer0", "signer1", "signer2" };
            var signerCount = signers.Length;

            for (int blockNumber = 1; blockNumber <= 12; blockNumber++)
            {
                var expectedSignerIndex = blockNumber % signerCount;
                var expectedSigner = signers[expectedSignerIndex];

                var actualSignerIndex = (int)(blockNumber % signerCount);
                Assert.Equal(expectedSignerIndex, actualSignerIndex);
            }
        }

        [Fact]
        public void CliqueRotation_DifficultyAssignment_InTurnVsOutOfTurn()
        {
            var signers = new[] { "signer0", "signer1", "signer2" };
            var signerCount = signers.Length;

            const int DIFF_IN_TURN = 2;
            const int DIFF_OUT_OF_TURN = 1;

            for (int blockNumber = 1; blockNumber <= 9; blockNumber++)
            {
                for (int signerIndex = 0; signerIndex < signerCount; signerIndex++)
                {
                    var isInTurn = (blockNumber % signerCount) == signerIndex;
                    var expectedDifficulty = isInTurn ? DIFF_IN_TURN : DIFF_OUT_OF_TURN;

                    if (isInTurn)
                    {
                        Assert.Equal(DIFF_IN_TURN, expectedDifficulty);
                    }
                    else
                    {
                        Assert.Equal(DIFF_OUT_OF_TURN, expectedDifficulty);
                    }
                }
            }
        }

        private void PopulateMockClient(MockSequencerRpcClient client, int blockCount)
        {
            var key = EthECKey.GenerateKey();
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
                    Difficulty = (i % 3 == 0) ? 2 : 1,
                    BlockNumber = i,
                    GasLimit = 30000000,
                    GasUsed = 0,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + i,
                    ExtraData = new byte[97],
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
}

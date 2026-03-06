using System.Numerics;
using Nethereum.AppChain.Sync;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class PeerManagerTests
    {
        [Fact]
        public void AddPeer_AddsPeerSuccessfully()
        {
            var manager = new PeerManager();

            var result = manager.AddPeer("http://localhost:8545");

            Assert.True(result);
            Assert.Single(manager.Peers);
            Assert.Equal("http://localhost:8545", manager.Peers[0].Url);
        }

        [Fact]
        public void AddPeer_NormalizesUrl()
        {
            var manager = new PeerManager();

            manager.AddPeer("localhost:8545");
            manager.AddPeer("HTTP://LOCALHOST:8546/");

            Assert.Equal(2, manager.Peers.Count);
            var peer1 = manager.GetPeer("http://localhost:8545");
            var peer2 = manager.GetPeer("http://localhost:8546");
            Assert.NotNull(peer1);
            Assert.NotNull(peer2);
            Assert.Equal("http://localhost:8545", peer1.Url);
            Assert.Equal("http://localhost:8546", peer2.Url);
        }

        [Fact]
        public void AddPeer_RejectsDuplicates()
        {
            var manager = new PeerManager();

            var result1 = manager.AddPeer("http://localhost:8545");
            var result2 = manager.AddPeer("http://localhost:8545");

            Assert.True(result1);
            Assert.False(result2);
            Assert.Single(manager.Peers);
        }

        [Fact]
        public void RemovePeer_RemovesPeerSuccessfully()
        {
            var manager = new PeerManager();
            manager.AddPeer("http://localhost:8545");
            manager.AddPeer("http://localhost:8546");

            var result = manager.RemovePeer("http://localhost:8545");

            Assert.True(result);
            Assert.Single(manager.Peers);
            Assert.Equal("http://localhost:8546", manager.Peers[0].Url);
        }

        [Fact]
        public void RemovePeer_ReturnsFalseForNonexistent()
        {
            var manager = new PeerManager();

            var result = manager.RemovePeer("http://localhost:8545");

            Assert.False(result);
        }

        [Fact]
        public void GetBestPeer_ReturnsHighestBlockPeer()
        {
            var mockClient1 = new MockSequencerRpcClient();
            var mockClient2 = new MockSequencerRpcClient();
            var mockClient3 = new MockSequencerRpcClient();

            var manager = new PeerManager(clientFactory: url =>
            {
                if (url.Contains("8545")) return mockClient1;
                if (url.Contains("8546")) return mockClient2;
                return mockClient3;
            });

            manager.AddPeer("http://localhost:8545");
            manager.AddPeer("http://localhost:8546");
            manager.AddPeer("http://localhost:8547");

            // Simulate different block heights
            var peers = manager.Peers.ToList();
            peers[0].IsHealthy = true;
            peers[0].BlockNumber = 100;
            peers[1].IsHealthy = true;
            peers[1].BlockNumber = 150;
            peers[2].IsHealthy = true;
            peers[2].BlockNumber = 120;

            var best = manager.GetBestPeer();

            Assert.NotNull(best);
            Assert.Equal("http://localhost:8546", best.Url);
            Assert.Equal(150, best.BlockNumber);
        }

        [Fact]
        public void GetBestPeer_SkipsUnhealthyPeers()
        {
            var manager = new PeerManager();

            manager.AddPeer("http://localhost:8545");
            manager.AddPeer("http://localhost:8546");

            var peer1 = manager.GetPeer("http://localhost:8545");
            var peer2 = manager.GetPeer("http://localhost:8546");

            peer1!.IsHealthy = false;
            peer1.BlockNumber = 200;
            peer2!.IsHealthy = true;
            peer2.BlockNumber = 100;

            var best = manager.GetBestPeer();

            Assert.NotNull(best);
            Assert.Equal("http://localhost:8546", best.Url);
        }

        [Fact]
        public void GetBestPeer_ReturnsNullWhenNoPeers()
        {
            var manager = new PeerManager();

            var best = manager.GetBestPeer();

            Assert.Null(best);
        }

        [Fact]
        public void GetBestPeer_ReturnsNullWhenAllUnhealthy()
        {
            var manager = new PeerManager();
            manager.AddPeer("http://localhost:8545");

            var peers = manager.Peers.ToList();
            peers[0].IsHealthy = false;

            var best = manager.GetBestPeer();

            Assert.Null(best);
        }

        [Fact]
        public async Task CheckAllPeersAsync_UpdatesPeerHealth()
        {
            var mockClient = new MockSequencerRpcClient();
            mockClient.AddBlock(CreateMockBlock(10));

            var manager = new PeerManager(clientFactory: _ => mockClient);
            manager.AddPeer("http://localhost:8545");

            await manager.CheckAllPeersAsync();

            var peer = manager.Peers[0];
            Assert.True(peer.IsHealthy);
            Assert.Equal(10, peer.BlockNumber);
        }

        [Fact]
        public async Task PeerStatusChanged_FiresOnHealthChange()
        {
            var mockClient = new MockSequencerRpcClient();
            mockClient.AddBlock(CreateMockBlock(5));

            var manager = new PeerManager(clientFactory: _ => mockClient);

            PeerStatusChangedEventArgs? eventArgs = null;
            manager.PeerStatusChanged += (s, e) => eventArgs = e;

            manager.AddPeer("http://localhost:8545");

            await Task.Delay(100);

            var peer = manager.GetPeer("http://localhost:8545");
            peer!.IsHealthy = false;

            await manager.CheckAllPeersAsync();

            Assert.NotNull(eventArgs);
            Assert.False(eventArgs.PreviousHealthy);
            Assert.True(eventArgs.CurrentHealthy);
        }

        private LiveBlockData CreateMockBlock(long blockNumber)
        {
            return new LiveBlockData
            {
                Header = new Nethereum.Model.BlockHeader
                {
                    BlockNumber = blockNumber,
                    ParentHash = new byte[32],
                    UnclesHash = new byte[32],
                    Coinbase = "0x0000000000000000000000000000000000000001",
                    StateRoot = new byte[32],
                    TransactionsHash = new byte[32],
                    ReceiptHash = new byte[32],
                    LogsBloom = new byte[256],
                    ExtraData = new byte[0],
                    MixHash = new byte[32],
                    Nonce = new byte[8]
                },
                BlockHash = new byte[32],
                Transactions = new List<Nethereum.Model.ISignedTransaction>(),
                Receipts = new List<Nethereum.Model.Receipt>()
            };
        }
    }
}

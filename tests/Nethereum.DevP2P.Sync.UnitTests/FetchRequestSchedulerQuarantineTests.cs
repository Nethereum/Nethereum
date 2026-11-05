using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerQuarantineTests
    {
        [Fact]
        public void Given_APeerWithASnapStateBench_When_ItIsRemovedFromThePool_Then_ItsBenchIsForgotten()
        {
            var pool = new RemovablePeerPool();
            var peer = pool.ActivePeers.Single();
            var scheduler = new FetchRequestScheduler(
                pool, new UnusedWorker(), new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));
            scheduler.QuarantineSnapState(peer);
            Assert.True(scheduler.IsSnapStateQuarantined(peer.Id));

            pool.Remove(peer);

            Assert.False(scheduler.IsSnapStateQuarantined(peer.Id));
        }

        [Fact]
        public void Given_TwoPeersWithSnapStateBenches_When_OneIsRemovedFromThePool_Then_TheOtherStaysBenched()
        {
            var pool = new RemovablePeerPool();
            var removed = pool.ActivePeers.Single();
            var kept = pool.Add();
            var scheduler = new FetchRequestScheduler(
                pool, new UnusedWorker(), new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));
            scheduler.QuarantineSnapState(removed);
            scheduler.QuarantineSnapState(kept);

            pool.Remove(removed);

            Assert.True(scheduler.IsSnapStateQuarantined(kept.Id));
        }

        private sealed class UnusedWorker : IPeerRequestWorker
        {
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class RemovablePeerPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers = new List<IEthPeer>();
            private int _next;

            public RemovablePeerPool() { Add(); }

            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;

            public IEthPeer Add()
            {
                var peer = new FakeEthPeer($"enode://{new string('a', 128)}@127.0.0.1:{30000 + _next++}");
                _peers.Add(peer);
                return peer;
            }

            public void Remove(IEthPeer peer)
            {
                _peers.Remove(peer);
                PeerRemoved?.Invoke(this, peer);
            }
        }

        private sealed class FakeEthPeer : IEthPeer
        {
            public FakeEthPeer(string enode) { Enode = enode; Host = enode; }
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}

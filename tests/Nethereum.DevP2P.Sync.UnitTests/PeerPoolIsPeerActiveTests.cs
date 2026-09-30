using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Model.P2P;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolIsPeerActiveTests
    {
        [Fact]
        public void IsPeerActive_ReturnsTrue_ForCurrentlyConnectedPeer()
        {
            IPeerPool pool = new MutableFakePeerPool(new[] { "enode1", "enode2" });
            var snapshot = pool.ActivePeers.ToList();

            foreach (var peer in snapshot)
                Assert.True(pool.IsPeerActive(peer.Id));
        }

        [Fact]
        public void IsPeerActive_ReturnsFalse_AfterPeerDropped()
        {
            var pool = new MutableFakePeerPool(new[] { "enode1", "enode2" });
            IPeerPool ip = pool;
            var snapshot = pool.ActivePeers.ToList();
            var droppedId = snapshot[0].Id;

            pool.Drop(snapshot[0]);

            Assert.False(ip.IsPeerActive(droppedId));
            Assert.True(ip.IsPeerActive(snapshot[1].Id));
        }

        [Fact]
        public void IsPeerActive_DefaultImplementation_FallsBackToActivePeersScan()
        {
            IPeerPool pool = new SnapshotOnlyPool(new[] { "enode-a", "enode-b" });
            var present = pool.ActivePeers.First().Id;
            var absent = Guid.NewGuid();

            Assert.True(pool.IsPeerActive(present));
            Assert.False(pool.IsPeerActive(absent));
        }

        [Fact]
        public void IsPeerActive_OnPeerPoolManagerOverride_DoesNotScan()
        {
            var method = typeof(PeerPoolManager).GetMethod(
                nameof(IPeerPool.IsPeerActive),
                new[] { typeof(Guid) });
            Assert.NotNull(method);
            Assert.Equal(typeof(PeerPoolManager), method!.DeclaringType);
        }

        [Fact]
        public void PeerDisconnected_BetweenSnapshotAndReserve_NotReserved()
        {
            var pool = new StaleSnapshotPool(new[] { "live-1", "stale-1" });
            pool.MarkInactive("stale-1");
            IPeerPool ip = pool;

            int reserved = 0;
            int skippedAsInactive = 0;
            foreach (var peer in ip.ActivePeers)
            {
                if (!ip.IsPeerActive(peer.Id))
                {
                    skippedAsInactive++;
                    continue;
                }
                reserved++;
            }

            Assert.Equal(1, skippedAsInactive);
            Assert.Equal(1, reserved);
        }

        private sealed class StaleSnapshotPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            private readonly HashSet<string> _inactiveEnodes = new(StringComparer.OrdinalIgnoreCase);

            public StaleSnapshotPool(IEnumerable<string> enodes)
            {
                _peers = enodes.Select(e => (IEthPeer)new FakeEthPeer(e)).ToList();
            }

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

            public void MarkInactive(string enode) => _inactiveEnodes.Add(enode);

            public bool IsPeerActive(Guid peerId)
            {
                foreach (var peer in _peers)
                {
                    if (peer.Id == peerId)
                        return !_inactiveEnodes.Contains(peer.Enode);
                }
                return false;
            }

            private void TouchEvents()
            {
                PeerAdded?.Invoke(this, _peers[0]);
                PeerRemoved?.Invoke(this, _peers[0]);
            }
        }

        private sealed class MutableFakePeerPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;

            public MutableFakePeerPool(IEnumerable<string> enodes)
            {
                _peers = enodes.Select(e => (IEthPeer)new FakeEthPeer(e)).ToList();
            }

            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers.ToList();
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;

            public void Drop(IEthPeer peer)
            {
                _peers.Remove(peer);
                PeerRemoved?.Invoke(this, peer);
            }

            private void TouchEvents() { PeerAdded?.Invoke(this, _peers[0]); }
        }

        private sealed class SnapshotOnlyPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            public SnapshotOnlyPool(IEnumerable<string> enodes)
            {
                _peers = enodes.Select(e => (IEthPeer)new FakeEthPeer(e)).ToList();
            }

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

            private void TouchEvents()
            {
                PeerAdded?.Invoke(this, _peers[0]);
                PeerRemoved?.Invoke(this, _peers[0]);
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

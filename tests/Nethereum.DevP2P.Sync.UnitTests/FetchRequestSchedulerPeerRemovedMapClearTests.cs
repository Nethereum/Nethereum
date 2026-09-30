using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerPeerRemovedMapClearTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task OnPeerRemoved_ClearsLatencyHeaderQuarantineAndConsecutiveTimeoutMaps()
        {
            var enode = MakeEnode(1);
            var pool = new RemovablePeerPool(enode);
            var peer = pool.ActivePeers.Single();
            var worker = new SuccessfulHeadersWorker();

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);
            scheduler.RegisterTimeoutForQuarantine(peer.Id, TimeoutStreakKind.Eth, isTrusted: false, out _);
            scheduler.RegisterTimeoutForQuarantine(peer.Id, TimeoutStreakKind.Snap, isTrusted: false, out _);
            scheduler.QuarantineHeaderPeer(peer.Id);

            Assert.True(scheduler.GetLatencyEmaForTest(peer.Id) > 0);
            Assert.True(scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Eth) > 0);
            Assert.True(scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Snap) > 0);
            Assert.True(scheduler.IsHeaderPeerQuarantined(peer.Id));

            pool.Remove(peer);

            Assert.Equal(0.0, scheduler.GetLatencyEmaForTest(peer.Id));
            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Eth));
            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Snap));
            Assert.False(scheduler.IsHeaderPeerQuarantined(peer.Id));
            Assert.Equal(0, scheduler.GetInFlightCountForTest(peer.Id));
        }

        private sealed class SuccessfulHeadersWorker : IPeerRequestWorker
        {
            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                var list = new List<BlockHeader>();
                for (ulong i = 0; i < limit; i++)
                    list.Add(new BlockHeader { BlockNumber = (long)(startBlock + i) });
                return Task.FromResult(list);
            }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class RemovablePeerPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            public RemovablePeerPool(string enode) { _peers = new List<IEthPeer> { new FakeEthPeer(enode) }; }

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

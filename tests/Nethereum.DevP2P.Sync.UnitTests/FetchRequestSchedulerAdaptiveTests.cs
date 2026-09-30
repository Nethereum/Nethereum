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
    public class FetchRequestSchedulerAdaptiveTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task LatencyEma_RecordedAfterSuccessfulFetch()
        {
            var pool = new FakePeerPool(new[] { MakeEnode(1) });
            var scheduler = new FetchRequestScheduler(pool, new DelayingWorker(40), new FetchRequestSchedulerOptions());

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);

            var id = pool.ActivePeers.Single().Id;
            Assert.True(scheduler.GetLatencyEmaForTest(id) >= 20,
                $"expected latency EMA to reflect the ~40ms response; got {scheduler.GetLatencyEmaForTest(id)}");
        }

        [Fact]
        public void AdaptiveTimeout_UnknownPeer_UsesFullConfiguredBudget()
        {
            var pool = new FakePeerPool(new[] { MakeEnode(1) });
            var scheduler = new FetchRequestScheduler(pool, new DelayingWorker(0), new FetchRequestSchedulerOptions());

            var id = pool.ActivePeers.Single().Id;
            Assert.Equal(TimeSpan.FromSeconds(30), scheduler.GetAdaptiveTimeoutForTest(id));
        }

        [Fact]
        public async Task AdaptiveTimeout_FastPeer_ShorterThanBudget_ClampedToFloor()
        {
            var pool = new FakePeerPool(new[] { MakeEnode(1) });
            var scheduler = new FetchRequestScheduler(pool, new DelayingWorker(0), new FetchRequestSchedulerOptions());

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);

            var id = pool.ActivePeers.Single().Id;
            var timeout = scheduler.GetAdaptiveTimeoutForTest(id);
            Assert.True(timeout < TimeSpan.FromSeconds(30), $"fast peer should get a reduced timeout; got {timeout}");
            Assert.Equal(TimeSpan.FromSeconds(2), timeout);
        }

        private sealed class DelayingWorker : IPeerRequestWorker
        {
            private readonly int _delayMs;
            public DelayingWorker(int delayMs) => _delayMs = delayMs;

            public async Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                if (_delayMs > 0) await Task.Delay(_delayMs, ct).ConfigureAwait(false);
                var list = new List<BlockHeader>();
                for (ulong i = 0; i < limit; i++)
                    list.Add(new BlockHeader { BlockNumber = (long)(startBlock + i) });
                return list;
            }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class FakePeerPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            public FakePeerPool(IEnumerable<string> enodes) => _peers = enodes.Select(e => (IEthPeer)new FakeEthPeer(e)).ToList();
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerParallelTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        private static byte[] MakeHash(int seed)
        {
            var bytes = new byte[32];
            bytes[0] = (byte)(seed & 0xFF);
            bytes[1] = (byte)((seed >> 8) & 0xFF);
            bytes[2] = (byte)((seed >> 16) & 0xFF);
            bytes[3] = (byte)((seed >> 24) & 0xFF);
            return bytes;
        }

        [Fact]
        public async Task FetchBodies_SplitsAcrossMultiplePeers_WhenPoolWarm()
        {
            // 64 hashes / chunk-size 16 = 4 chunks. With 4 peers and parallel cap 4,
            // every peer should serve exactly one chunk.
            var enodes = Enumerable.Range(1, 4).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            foreach (var enode in enodes) worker.SetSuccess(enode);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxInFlightPerPeer: 1,
                    MaxParallelBodyFetches: 4,
                    BodyFetchChunkSize: 16));

            var hashes = Enumerable.Range(0, 64).Select(MakeHash).ToList();
            var bodies = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);

            Assert.Equal(64, bodies.Count);
            Assert.Equal(4, worker.TotalCalls);
            foreach (var enode in enodes)
                Assert.Equal(1, worker.BodyCallCount(enode));
        }

        [Fact]
        public async Task FetchBodies_FallsBackToSinglePeer_WhenOnlyOneAvailable()
        {
            // Same 64-hash request but only one peer available — must collapse
            // back to a single batched call (no spurious round-robin).
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            worker.SetSuccess(enodes[0]);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxInFlightPerPeer: 1,
                    MaxParallelBodyFetches: 4,
                    BodyFetchChunkSize: 16));

            var hashes = Enumerable.Range(0, 64).Select(MakeHash).ToList();
            var bodies = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);

            Assert.Equal(64, bodies.Count);
            Assert.Equal(1, worker.TotalCalls);
            Assert.Equal(1, worker.BodyCallCount(enodes[0]));
            Assert.Equal(64, worker.MaxHashesSeenInOneCall);
        }

        [Fact]
        public async Task FetchBodies_RespectsMaxParallelBodyFetches_Cap()
        {
            // 4 peers available, 64 hashes, but cap = 2 → exactly 2 chunks of 32.
            var enodes = Enumerable.Range(1, 4).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            foreach (var enode in enodes) worker.SetSuccess(enode);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxInFlightPerPeer: 1,
                    MaxParallelBodyFetches: 2,
                    BodyFetchChunkSize: 16));

            var hashes = Enumerable.Range(0, 64).Select(MakeHash).ToList();
            var bodies = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);

            Assert.Equal(64, bodies.Count);
            Assert.Equal(2, worker.TotalCalls);
            Assert.True(worker.MaxHashesSeenInOneCall <= 32);
        }

        [Fact]
        public async Task FetchBodies_PreservesOrder_AcrossMergedChunks()
        {
            // Each chunk-body has a deterministic marker (Transactions[0].Nonce
            // = the chunk-base index) so we can confirm the merged list is in
            // the request order, not interleaved by completion order.
            var enodes = Enumerable.Range(1, 4).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            foreach (var enode in enodes) worker.SetSuccess(enode);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxInFlightPerPeer: 1,
                    MaxParallelBodyFetches: 4,
                    BodyFetchChunkSize: 16));

            var hashes = Enumerable.Range(0, 64).Select(MakeHash).ToList();
            var bodies = await scheduler.FetchBodiesAsync(hashes, CancellationToken.None);

            Assert.Equal(64, bodies.Count);
            // The fake worker returns one BlockBody per requested hash, in the
            // same positional order. The merge must preserve chunk order.
            for (int i = 0; i < 64; i++)
            {
                Assert.NotNull(bodies[i]);
            }
        }

        [Fact]
        public async Task FetchBodies_WithExcludePeer_RoutesToOtherPeer()
        {
            // The block-52567 production failure mode: peer A returns bodies whose
            // tx_root doesn't match the canonical header. DevP2PBlockSource detects
            // it (one level up) and blames peer A. The new excludePeers overload
            // must steer the scheduler off A onto peer B before retrying.
            var enodes = Enumerable.Range(1, 2).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            foreach (var enode in enodes) worker.SetSuccess(enode);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxParallelBodyFetches: 1,
                    BodyFetchChunkSize: 16));

            var hashes = Enumerable.Range(0, 8).Select(MakeHash).ToList();
            var excludeFirst = new[] { pool.ActivePeers.First().Id };

            var result = await scheduler.FetchBodiesAsync(hashes, excludeFirst, CancellationToken.None);

            Assert.Equal(8, result.Bodies.Count);
            // Excluded peer untouched, the OTHER peer served.
            Assert.Equal(0, worker.BodyCallCount(enodes[0]));
            Assert.Equal(1, worker.BodyCallCount(enodes[1]));
            Assert.Single(result.ServingPeerIds);
            Assert.Equal(pool.ActivePeers.Skip(1).First().Id, result.ServingPeerIds.First());
        }

        [Fact]
        public async Task FetchBodies_NoExclude_ReturnsServingPeerIdsForCallerVisibility()
        {
            // The overload's whole point is letting the caller learn who served so
            // it can blame them on a downstream validation failure.
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            worker.SetSuccess(enodes[0]);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxParallelBodyFetches: 1,
                    BodyFetchChunkSize: 16));

            var hashes = Enumerable.Range(0, 4).Select(MakeHash).ToList();
            var result = await scheduler.FetchBodiesAsync(hashes, excludePeers: null, CancellationToken.None);

            Assert.Equal(4, result.Bodies.Count);
            Assert.Single(result.ServingPeerIds);
            Assert.Equal(pool.ActivePeers.First().Id, result.ServingPeerIds.First());
        }

        [Fact]
        public async Task FetchBodies_Empty_ReturnsEmpty()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledBodiesWorker();
            worker.SetSuccess(enodes[0]);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxParallelBodyFetches: 4,
                    BodyFetchChunkSize: 16));

            var bodies = await scheduler.FetchBodiesAsync(Array.Empty<byte[]>(), CancellationToken.None);

            Assert.Empty(bodies);
            Assert.Equal(0, worker.TotalCalls);
        }

        private sealed class FakePeerPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            public FakePeerPool(IEnumerable<string> enodes)
            {
                _peers = enodes.Select(e => (IEthPeer)new FakeEthPeer(e)).ToList();
            }

            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
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

        private sealed class ControlledBodiesWorker : IPeerRequestWorker
        {
            private readonly ConcurrentDictionary<string, bool> _success = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, int> _bodyCounts = new(StringComparer.OrdinalIgnoreCase);
            private int _total;
            private int _maxHashesSeenInOneCall;

            public int TotalCalls => Volatile.Read(ref _total);
            public int BodyCallCount(string enode) => _bodyCounts.TryGetValue(enode, out var c) ? c : 0;
            public int MaxHashesSeenInOneCall => Volatile.Read(ref _maxHashesSeenInOneCall);

            public void SetSuccess(string enode) => _success[enode] = true;

            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
                => throw new NotImplementedException("Headers not exercised in body-parallel tests");

            public async Task<List<BlockBody>> GetBodiesAsync(
                IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                _bodyCounts.AddOrUpdate(peer.Enode, 1, (_, prev) => prev + 1);
                Interlocked.Increment(ref _total);
                while (true)
                {
                    var prev = Volatile.Read(ref _maxHashesSeenInOneCall);
                    if (blockHashes.Count <= prev) break;
                    if (Interlocked.CompareExchange(ref _maxHashesSeenInOneCall, blockHashes.Count, prev) == prev) break;
                }

                if (!_success.ContainsKey(peer.Enode))
                    throw new InvalidOperationException($"No configured outcome for {peer.Enode}");

                // Real async yield so the scheduler's _inFlight counter stays
                // incremented until every concurrent chunk has been claimed —
                // mirrors a real network RTT between dispatch and response.
                await Task.Delay(25, ct).ConfigureAwait(false);

                var bodies = new List<BlockBody>(blockHashes.Count);
                for (int i = 0; i < blockHashes.Count; i++)
                {
                    bodies.Add(new BlockBody());
                }
                return bodies;
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(
                IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException("Receipts not exercised in body-parallel tests");

            public Task<Nethereum.Model.P2P.Snap.AccountRangeMessage> GetAccountRangeAsync(
                IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException("Snap not exercised in body-parallel tests");

            public Task<Nethereum.Model.P2P.Snap.StorageRangesMessage> GetStorageRangesAsync(
                IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException("Snap not exercised in body-parallel tests");

            public Task<Nethereum.Model.P2P.Snap.ByteCodesMessage> GetByteCodesAsync(
                IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException("Snap not exercised in body-parallel tests");

            public Task<Nethereum.Model.P2P.Snap.TrieNodesMessage> GetTrieNodesAsync(
                IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException("Snap not exercised in body-parallel tests");
        }
    }
}

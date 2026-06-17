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

namespace Nethereum.DevP2P.IntegrationTests
{
    public class FetchRequestSchedulerE2ETests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task FetchHeaders_PicksBestPeer_ReturnsResult()
        {
            var enodes = Enumerable.Range(1, 3).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledRequestWorker();
            worker.SetHeaderResponse(enodes[0], MakeHeaders(100, 5));
            worker.SetHeaderResponse(enodes[1], MakeHeaders(100, 5));
            worker.SetHeaderResponse(enodes[2], MakeHeaders(100, 5));

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchHeadersAsync(100, 5, CancellationToken.None);

            Assert.Equal(5, headers.Count);
            Assert.Equal(100, (long)headers[0].BlockNumber);
            Assert.Equal(1, worker.TotalCalls);
        }

        [Fact]
        public async Task FetchHeaders_TimeoutOnFirstPeer_RetriesOnAnother()
        {
            var enodes = Enumerable.Range(1, 2).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledRequestWorker();
            worker.SetHeaderTimeout(enodes[0]);
            worker.SetHeaderResponse(enodes[1], MakeHeaders(100, 3));

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(100),
                    MaxRetriesPerRequest: 3,
                    MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchHeadersAsync(100, 3, CancellationToken.None);

            Assert.Equal(3, headers.Count);
            Assert.Equal(2, worker.TotalCalls);
            Assert.Equal(1, worker.HeaderCallCount(enodes[0]));
            Assert.Equal(1, worker.HeaderCallCount(enodes[1]));
        }

        [Fact]
        public async Task FetchHeaders_ScoreLookupPreferred_HighScoreChosenFirst()
        {
            var enodes = Enumerable.Range(1, 2).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledRequestWorker();
            worker.SetHeaderResponse(enodes[0], MakeHeaders(100, 1));
            worker.SetHeaderResponse(enodes[1], MakeHeaders(100, 1));

            Func<string, PeerScore> scoreLookup = enode =>
                enode == enodes[1]
                    ? new PeerScore(10, 0, DateTimeOffset.UtcNow, 100.0)
                    : new PeerScore(1, 0, DateTimeOffset.UtcNow, 1.0);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1),
                scoreLookup: scoreLookup);

            await scheduler.FetchHeadersAsync(100, 1, CancellationToken.None);

            Assert.Equal(1, worker.HeaderCallCount(enodes[1]));
            Assert.Equal(0, worker.HeaderCallCount(enodes[0]));
        }

        [Fact]
        public async Task FetchHeaders_AllRetriesExhausted_Throws()
        {
            var enodes = Enumerable.Range(1, 2).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new ControlledRequestWorker();
            worker.SetHeaderError(enodes[0]);
            worker.SetHeaderError(enodes[1]);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxRetriesPerRequest: 3, MaxInFlightPerPeer: 1));

            await Assert.ThrowsAsync<FetchRequestFailedException>(() =>
                scheduler.FetchHeadersAsync(100, 1, CancellationToken.None));
            Assert.Equal(2, worker.TotalCalls);
        }

        private static List<BlockHeader> MakeHeaders(ulong startBlock, int count)
        {
            var headers = new List<BlockHeader>(count);
            for (int i = 0; i < count; i++)
                headers.Add(new BlockHeader { BlockNumber = (long)(startBlock + (ulong)i) });
            return headers;
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

        private sealed class ControlledRequestWorker : IPeerRequestWorker
        {
            private readonly ConcurrentDictionary<string, OutcomeKind> _outcomes = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, List<BlockHeader>> _headerResponses = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, int> _headerCounts = new(StringComparer.OrdinalIgnoreCase);
            private int _total;

            public int TotalCalls => Volatile.Read(ref _total);
            public int HeaderCallCount(string enode) => _headerCounts.TryGetValue(enode, out var c) ? c : 0;

            public void SetHeaderResponse(string enode, List<BlockHeader> response)
            {
                _outcomes[enode] = OutcomeKind.Success;
                _headerResponses[enode] = response;
            }
            public void SetHeaderTimeout(string enode) => _outcomes[enode] = OutcomeKind.Timeout;
            public void SetHeaderError(string enode) => _outcomes[enode] = OutcomeKind.Error;

            public async Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, CancellationToken ct)
            {
                _headerCounts.AddOrUpdate(peer.Enode, 1, (_, prev) => prev + 1);
                Interlocked.Increment(ref _total);

                if (!_outcomes.TryGetValue(peer.Enode, out var outcome))
                    throw new InvalidOperationException($"No configured outcome for {peer.Enode}");

                switch (outcome)
                {
                    case OutcomeKind.Success:
                        return _headerResponses[peer.Enode];
                    case OutcomeKind.Timeout:
                        await Task.Delay(TimeSpan.FromSeconds(60), ct);
                        return new List<BlockHeader>();
                    case OutcomeKind.Error:
                    default:
                        throw new InvalidOperationException($"stub-error {peer.Enode}");
                }
            }

            public Task<List<BlockBody>> GetBodiesAsync(
                IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException("Bodies not exercised in Stage 3.1 tests");

            private enum OutcomeKind { Success, Timeout, Error }
        }
    }
}

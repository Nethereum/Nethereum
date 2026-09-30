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
using Nethereum.Model.P2P.Snap;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerInFlightTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task InFlightCounter_DropsToZero_AfterSuccessfulRequest()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new ScriptedHeadersWorker();
            worker.SetSuccess(enodes[0]);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);

            var peerId = pool.ActivePeers.Single().Id;
            Assert.Equal(0, scheduler.GetInFlightCountForTest(peerId));
        }

        [Fact]
        public async Task InFlightCounter_CorrectAfterMixedSuccessAndException()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new AlternatingWorker();

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxInFlightPerPeer: 1,
                    MaxRetriesPerRequest: 1));

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    await scheduler.FetchHeadersAsync((ulong)i, 1, CancellationToken.None);
                }
                catch (FetchRequestFailedException) { }
            }

            var peerId = pool.ActivePeers.Single().Id;
            Assert.Equal(0, scheduler.GetInFlightCountForTest(peerId));
        }

        [Fact]
        public async Task InFlightCounter_NeverNegative_UnderConcurrentExceptionLoad()
        {
            var enodes = Enumerable.Range(1, 4).Select(MakeEnode).ToArray();
            var pool = new FakePeerPool(enodes);
            var worker = new AlternatingWorker();

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    MaxInFlightPerPeer: 4,
                    MaxRetriesPerRequest: 1));

            var tasks = new List<Task>();
            for (int i = 0; i < 80; i++)
            {
                int local = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await scheduler.FetchHeadersAsync((ulong)local, 1, CancellationToken.None);
                    }
                    catch (FetchRequestFailedException) { }
                    catch (TimeoutException) { }
                }));
            }
            await Task.WhenAll(tasks);

            foreach (var peer in pool.ActivePeers)
            {
                var n = scheduler.GetInFlightCountForTest(peer.Id);
                Assert.True(n >= 0, $"peer {peer.Id} negative in-flight: {n}");
                Assert.Equal(0, n);
            }
        }

        private sealed class ScriptedHeadersWorker : IPeerRequestWorker
        {
            private readonly ConcurrentDictionary<string, bool> _success = new(StringComparer.OrdinalIgnoreCase);
            public void SetSuccess(string enode) => _success[enode] = true;

            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                if (!_success.TryGetValue(peer.Enode, out _))
                    throw new InvalidOperationException($"no-script peer {peer.Enode}");
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

        private sealed class AlternatingWorker : IPeerRequestWorker
        {
            private int _counter;

            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                var n = Interlocked.Increment(ref _counter);
                if ((n & 1) == 0)
                    throw new InvalidOperationException("alternating-fail");
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

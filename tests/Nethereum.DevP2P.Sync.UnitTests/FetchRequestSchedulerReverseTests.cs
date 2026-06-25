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

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerReverseTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task FetchHeaders_DefaultReverseFalse_ReturnsAscending()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new ReverseAwareWorker();
            worker.SetCanonicalForward(enodes[0], 100, 5);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchHeadersAsync(100, 5, CancellationToken.None);

            Assert.False(worker.LastReverse);
            Assert.Equal(5, headers.Count);
            Assert.Equal(100, (long)headers[0].BlockNumber);
            Assert.Equal(101, (long)headers[1].BlockNumber);
            Assert.Equal(104, (long)headers[4].BlockNumber);
        }

        [Fact]
        public async Task FetchHeaders_ReverseTrue_PlumbsFlagAndReturnsDescending()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new ReverseAwareWorker();
            worker.SetCanonicalForward(enodes[0], 100, 5);

            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: 1));

            var headers = await scheduler.FetchHeadersAsync(104, 5, CancellationToken.None, reverse: true);

            Assert.True(worker.LastReverse);
            Assert.Equal(5, headers.Count);
            Assert.Equal(104, (long)headers[0].BlockNumber);
            Assert.Equal(103, (long)headers[1].BlockNumber);
            Assert.Equal(100, (long)headers[4].BlockNumber);
        }

        private sealed class ReverseAwareWorker : IPeerRequestWorker
        {
            private readonly ConcurrentDictionary<string, (ulong start, int count)> _canonical = new(StringComparer.OrdinalIgnoreCase);
            private int _lastReverseFlag;

            public bool LastReverse => Volatile.Read(ref _lastReverseFlag) != 0;

            public void SetCanonicalForward(string enode, ulong startBlock, int count)
                => _canonical[enode] = (startBlock, count);

            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                Volatile.Write(ref _lastReverseFlag, reverse ? 1 : 0);
                if (!_canonical.TryGetValue(peer.Enode, out var spec))
                    throw new InvalidOperationException($"No canonical headers for {peer.Enode}");

                var headers = new List<BlockHeader>((int)limit);
                if (reverse)
                {
                    for (ulong i = 0; i < limit; i++)
                    {
                        var n = startBlock - i;
                        headers.Add(new BlockHeader { BlockNumber = (long)n });
                    }
                }
                else
                {
                    for (ulong i = 0; i < limit; i++)
                    {
                        var n = startBlock + i;
                        headers.Add(new BlockHeader { BlockNumber = (long)n });
                    }
                }
                return Task.FromResult(headers);
            }

            public Task<List<BlockBody>> GetBodiesAsync(
                IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<List<List<Receipt>>> GetReceiptsAsync(
                IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<AccountRangeMessage> GetAccountRangeAsync(
                IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash,
                ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<StorageRangesMessage> GetStorageRangesAsync(
                IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes,
                byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<ByteCodesMessage> GetByteCodesAsync(
                IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<TrieNodesMessage> GetTrieNodesAsync(
                IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths,
                ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
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
    }
}

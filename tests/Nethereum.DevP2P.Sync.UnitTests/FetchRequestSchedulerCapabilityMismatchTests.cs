using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class FetchRequestSchedulerCapabilityMismatchTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task Given_ACapabilityMismatch_When_FetchingHeaders_Then_FailsFastWithoutClaimingAnotherPeer()
        {
            var enodes = new[] { MakeEnode(1), MakeEnode(2) };
            var pool = new FakePeerPool(enodes);
            var worker = new CapabilityMismatchWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromSeconds(5),
                    MaxRetriesPerRequest: 3));

            var thrown = await Assert.ThrowsAsync<SnapPeerCapabilityMismatchException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Contains("snap/2 defines no GetTrieNodes", thrown.Message);
            Assert.Equal(1, worker.CallCount);
            Assert.Empty(pool.ReportSuccessCalls);
        }

        [Fact]
        public async Task Given_ATransientTimeout_When_FetchingHeaders_Then_StillReassignsToAnotherPeer()
        {
            var enodes = new[] { MakeEnode(1), MakeEnode(2) };
            var pool = new FakePeerPool(enodes);
            var worker = new CountingHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 2));

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Equal(2, worker.CallCount);
        }

        private sealed class CapabilityMismatchWorker : IPeerRequestWorker
        {
            public int CallCount;
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                Interlocked.Increment(ref CallCount);
                throw new SnapPeerCapabilityMismatchException(
                    "snap/2 defines no GetTrieNodes — negotiate snap/1 to fetch trie nodes by path");
            }
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class CountingHangsWorker : IPeerRequestWorker
        {
            public int CallCount;
            public async Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                Interlocked.Increment(ref CallCount);
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new List<BlockHeader>();
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
            public List<(Guid PeerId, string Reason)> DropCalls { get; } = new();
            public List<Guid> ReportSuccessCalls { get; } = new();
            public FakePeerPool(IEnumerable<string> enodes, bool trusted = false)
                => _peers = enodes.Select(e => (IEthPeer)new FakeEthPeer(e, trusted)).ToList();
            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct)
            {
                DropCalls.Add((peerId, reason));
                return Task.CompletedTask;
            }
            public void ReportSuccess(Guid peerId) => ReportSuccessCalls.Add(peerId);
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class FakeEthPeer : IEthPeer
        {
            public FakeEthPeer(string enode, bool isTrusted = false) { Enode = enode; Host = enode; IsTrusted = isTrusted; }
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public bool IsTrusted { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}

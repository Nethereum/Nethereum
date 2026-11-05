using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BackfillerPeerPenaltyOwnerTests
    {
        [Fact]
        public void Given_APeerThatFailsBodyRequests_When_TheBackfillerRecordsFiveFailures_Then_TheSchedulerQuarantineCountsThemAndDropsThePeerOnce()
        {
            var pool = new CapturingDropPool();
            var scheduler = new FetchRequestScheduler(pool, new UnusedWorker(), new FetchRequestSchedulerOptions());
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(scheduler, pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            for (int i = 0; i < 5; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: i % 2 == 0);

            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Eth));
            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
        }

        [Fact]
        public void Given_APeerWithFailuresCountedByTheScheduler_When_TheBackfillerRecordsASuccess_Then_TheSchedulerStreakIsCleared()
        {
            var pool = new CapturingDropPool();
            var scheduler = new FetchRequestScheduler(pool, new UnusedWorker(), new FetchRequestSchedulerOptions());
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(scheduler, pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            backfiller.RecordPeerRequestFailureForTest(peer, "receipt", wasTimeout: true);
            backfiller.RecordPeerRequestFailureForTest(peer, "receipt", wasTimeout: false);
            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Eth));

            backfiller.RecordPeerRequestSuccessForTest(peer);

            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Eth));
            Assert.Contains(peer.Id, pool.ReportSuccessCalls);
            Assert.Empty(pool.DropCalls);
        }

        private sealed class CapturingDropPool : IPeerPool
        {
            public List<(Guid PeerId, string Reason)> DropCalls { get; } = new();
            public List<Guid> ReportSuccessCalls { get; } = new();
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct)
            {
                lock (DropCalls) DropCalls.Add((peerId, reason));
                return Task.CompletedTask;
            }
            public void ReportSuccess(Guid peerId) => ReportSuccessCalls.Add(peerId);
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class FakeEthPeer : IEthPeer
        {
            public FakeEthPeer(bool isTrusted) { IsTrusted = isTrusted; }
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode => "enode://peer@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public bool IsTrusted { get; }
            public int EthVersion => 69;
            public ulong PeerLatestBlock => 9;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
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
    }
}

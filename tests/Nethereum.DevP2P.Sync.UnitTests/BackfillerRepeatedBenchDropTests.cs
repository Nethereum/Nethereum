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

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BackfillerRepeatedBenchDropTests
    {
        [Fact]
        public void SingleTimeout_DoesNotDropThePeer()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);

            Assert.Empty(pool.DropCalls);
        }

        [Fact]
        public void RepeatedTimeouts_NonTrustedPeer_DropsOnTheFirstBenchOutcome()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            for (int i = 0; i < ParallelBlockBackfiller.PeerFailureDropThreshold - 1; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);
            Assert.Empty(pool.DropCalls);

            backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);

            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
            Assert.Empty(pool.BanCalls);
        }

        [Fact]
        public void RepeatedTimeouts_TrustedPeer_NeedsMultipleBenchOutcomes_BeforeDrop()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: true);

            var callsBeforeDrop =
                (ParallelBlockBackfiller.PeerFailureDropThreshold - 1)
                + (ParallelBlockBackfiller.TrustedRepeatedBenchDropThreshold - 1);
            for (int i = 0; i < callsBeforeDrop; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);
            Assert.Empty(pool.DropCalls);

            backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);

            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
            Assert.Empty(pool.BanCalls);
        }

        [Fact]
        public void RepeatedNonTimeoutFailures_NonTrustedPeer_DropsViaPoolOnTheFirstDisposeOutcome()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            for (int i = 0; i < ParallelBlockBackfiller.PeerFailureDropThreshold - 1; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: false);
            Assert.Empty(pool.DropCalls);

            backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: false);

            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
            Assert.Empty(pool.BanCalls);
        }

        [Fact]
        public void RepeatedNonTimeoutFailures_TrustedPeer_NeedsMultipleDisposeOutcomes_BeforeDrop()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: true);

            var callsBeforeDrop =
                (ParallelBlockBackfiller.PeerFailureDropThreshold - 1)
                + (ParallelBlockBackfiller.TrustedRepeatedBenchDropThreshold - 1);
            for (int i = 0; i < callsBeforeDrop; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: false);
            Assert.Empty(pool.DropCalls);

            backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: false);

            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
            Assert.Empty(pool.BanCalls);
        }

        [Fact]
        public void ReceiptDelivery_MatchedZero_RepeatedlyDoesNotBenchOrDropThePeer()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            for (int i = 0; i < ParallelBlockBackfiller.PeerFailureDropThreshold * 4; i++)
                backfiller.RecordReceiptDeliveryOutcomeForTest(peer, matched: 0);

            Assert.Empty(pool.DropCalls);
        }

        [Fact]
        public void ReceiptDelivery_MatchedNonZero_ReportsSuccessToThePool()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: false);

            backfiller.RecordReceiptDeliveryOutcomeForTest(peer, matched: 3);

            Assert.Contains(peer.Id, pool.ReportSuccessCalls);
        }

        [Fact]
        public void SuccessResetsTheTrustedBenchDropCounter()
        {
            var pool = new CapturingDropPool();
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, new UnusedWorker(), bundle);
            var peer = new FakeEthPeer(isTrusted: true);

            for (int i = 0; i < ParallelBlockBackfiller.PeerFailureDropThreshold; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);
            Assert.Empty(pool.DropCalls);

            backfiller.RecordPeerRequestSuccessForTest(peer);

            for (int i = 0; i < ParallelBlockBackfiller.PeerFailureDropThreshold - 1; i++)
                backfiller.RecordPeerRequestFailureForTest(peer, "body", wasTimeout: true);
            Assert.Empty(pool.DropCalls);
        }

        private sealed class CapturingDropPool : IPeerPool
        {
            public List<(Guid PeerId, string Reason)> DropCalls { get; } = new();
            public List<(string Enode, string Reason)> BanCalls { get; } = new();
            public List<Guid> ReportSuccessCalls { get; } = new();
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct)
            {
                BanCalls.Add((enode, reason));
                return Task.CompletedTask;
            }
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

        private sealed class UnusedScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}

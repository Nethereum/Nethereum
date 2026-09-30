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
    public class FetchRequestSchedulerRepeatedTimeoutTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public void RegisterTimeoutForQuarantine_ReturnsTrueOnlyAtThreshold_ThenResets()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peerId = Guid.NewGuid();

            Assert.False(scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out var count1));
            Assert.Equal(1, count1);
            Assert.Equal(1, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));

            Assert.False(scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out var count2));
            Assert.Equal(2, count2);
            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));

            Assert.True(scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out var count3));
            Assert.Equal(3, count3);
            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));

            Assert.False(scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out var count4));
            Assert.Equal(1, count4);
            Assert.Equal(1, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));
        }

        [Fact]
        public void RegisterTimeoutForQuarantine_TrustedPeer_NeedsHigherThreshold_ThanOrdinaryPeer()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());

            var trusted = Guid.NewGuid();
            for (int i = 1; i < 20; i++)
                Assert.False(
                    scheduler.RegisterTimeoutForQuarantine(trusted, TimeoutStreakKind.Eth, isTrusted: true, out _),
                    $"trusted peer must not quarantine at {i} consecutive timeouts");
            Assert.True(scheduler.RegisterTimeoutForQuarantine(trusted, TimeoutStreakKind.Eth, isTrusted: true, out var trip));
            Assert.Equal(20, trip);
            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(trusted, TimeoutStreakKind.Eth));

            var ordinary = Guid.NewGuid();
            Assert.False(scheduler.RegisterTimeoutForQuarantine(ordinary, TimeoutStreakKind.Eth, isTrusted: false, out _));
            Assert.False(scheduler.RegisterTimeoutForQuarantine(ordinary, TimeoutStreakKind.Eth, isTrusted: false, out _));
            Assert.True(scheduler.RegisterTimeoutForQuarantine(ordinary, TimeoutStreakKind.Eth, isTrusted: false, out _));
        }

        [Fact]
        public void AdaptiveTimeout_TrustedPeer_UsesLongerCeiling_ThanOrdinaryPeer()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(
                pool, new NeverCalledWorker(),
                new FetchRequestSchedulerOptions(PerRequestTimeout: TimeSpan.FromSeconds(8)));
            var peerId = Guid.NewGuid();

            var ordinary = scheduler.GetAdaptiveTimeoutForTest(peerId, isTrusted: false);
            var trustedTimeout = scheduler.GetAdaptiveTimeoutForTest(peerId, isTrusted: true);
            Assert.True(trustedTimeout > ordinary, $"trusted timeout {trustedTimeout} must exceed ordinary {ordinary}");
            Assert.Equal(TimeSpan.FromTicks(ordinary.Ticks * 4), trustedTimeout);
        }

        [Fact]
        public void RegisterSuccessForQuarantine_ClearsAnExistingStreak()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peerId = Guid.NewGuid();

            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out _);
            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out _);
            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));

            scheduler.RegisterSuccessForQuarantine(peerId, TimeoutStreakKind.Eth);

            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));
        }

        [Fact]
        public async Task SingleEthHeaderTimeout_TouchesQuarantineCounter_ButDoesNotDropThePeer()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Equal(1, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));
            Assert.Empty(pool.DropCalls);
        }

        [Fact]
        public async Task RepeatedEthHeaderTimeouts_DropsThePeerForRedial()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            for (int i = 0; i < 3; i++)
                await Assert.ThrowsAsync<FetchRequestFailedException>(
                    () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Single(pool.DropCalls);
            Assert.Equal(peerId, pool.DropCalls[0].PeerId);
            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));
        }

        [Fact]
        public async Task Given_ThreeConsecutiveEthTimeouts_NoSnap_When_Evaluated_Then_ThePeerIsDropped()
        {
            var enode = MakeEnode(1);
            var pool = new FakePeerPool(new[] { enode });
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            for (int i = 0; i < 3; i++)
                await Assert.ThrowsAsync<FetchRequestFailedException>(
                    () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Single(pool.DropCalls);
            Assert.Equal(peerId, pool.DropCalls[0].PeerId);
            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Snap));
        }

        [Fact]
        public async Task Given_TwoSnapTimeoutsThenOneEthTimeout_When_Evaluated_Then_ThePeerIsNotDropped()
        {
            var enode = MakeEnode(1);
            var pool = new FakePeerPool(new[] { enode });
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Snap, isTrusted: false, out _);
            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Snap, isTrusted: false, out _);
            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Snap));

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Empty(pool.DropCalls);
            Assert.Equal(1, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));
            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Snap));
        }

        [Fact]
        public async Task RepeatedEthHeaderTimeouts_TrustedPeer_NeedsHigherThreshold_BeforeDrop()
        {
            var enode = MakeEnode(1);
            var pool = new FakePeerPool(new[] { enode }, trusted: true);
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(10),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            for (int i = 0; i < 19; i++)
                await Assert.ThrowsAsync<FetchRequestFailedException>(
                    () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));
            Assert.Empty(pool.DropCalls);

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));
            Assert.Single(pool.DropCalls);
            Assert.Equal(peerId, pool.DropCalls[0].PeerId);
        }

        [Fact]
        public async Task WorkerHardTimeoutException_TouchesQuarantineCounter_SameAsCancellationTimeout()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var worker = new AlwaysThrowsWorkerTimeoutWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromSeconds(30),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));

            Assert.Equal(1, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth));
            Assert.Empty(pool.DropCalls);
        }

        [Fact]
        public async Task RepeatedWorkerHardTimeoutExceptions_TrustedPeer_NeedsHigherThreshold_BeforeDrop()
        {
            var enode = MakeEnode(1);
            var pool = new FakePeerPool(new[] { enode }, trusted: true);
            var worker = new AlwaysThrowsWorkerTimeoutWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromSeconds(30),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;

            for (int i = 0; i < 19; i++)
                await Assert.ThrowsAsync<FetchRequestFailedException>(
                    () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));
            Assert.Empty(pool.DropCalls);

            await Assert.ThrowsAsync<FetchRequestFailedException>(
                () => scheduler.FetchHeadersAsync(0, 1, CancellationToken.None));
            Assert.Single(pool.DropCalls);
            Assert.Equal(peerId, pool.DropCalls[0].PeerId);
        }

        [Fact]
        public async Task RepeatedEthBodyTimeouts_DropsThePeerForRedial()
        {
            var enode = MakeEnode(1);
            var pool = new FakePeerPool(new[] { enode });
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;
            var hashes = new List<byte[]> { new byte[32] };

            for (int i = 0; i < 3; i++)
                await Assert.ThrowsAsync<FetchRequestFailedException>(
                    () => scheduler.FetchBodiesAsync(hashes, CancellationToken.None));

            Assert.Single(pool.DropCalls);
            Assert.Equal(peerId, pool.DropCalls[0].PeerId);
        }

        [Fact]
        public async Task RepeatedEthReceiptTimeouts_DropsThePeerForRedial()
        {
            var enode = MakeEnode(1);
            var pool = new FakePeerPool(new[] { enode });
            var worker = new AlwaysHangsWorker();
            var scheduler = new FetchRequestScheduler(
                pool, worker,
                new FetchRequestSchedulerOptions(
                    PerRequestTimeout: TimeSpan.FromMilliseconds(20),
                    MaxRetriesPerRequest: 1));
            var peerId = pool.ActivePeers.Single().Id;
            var hashes = new List<byte[]> { new byte[32] };

            for (int i = 0; i < 3; i++)
                await Assert.ThrowsAsync<FetchRequestFailedException>(
                    () => scheduler.FetchReceiptsAsync(hashes, CancellationToken.None));

            Assert.Single(pool.DropCalls);
            Assert.Equal(peerId, pool.DropCalls[0].PeerId);
        }

        [Fact]
        public async Task SuccessOnNonSnapRequest_DoesNotClearAnExistingSnapTimeoutStreak()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var scheduler = new FetchRequestScheduler(pool, new AlwaysSucceedsWorker(), new FetchRequestSchedulerOptions());
            var peerId = pool.ActivePeers.Single().Id;

            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Snap, isTrusted: false, out _);
            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Snap, isTrusted: false, out _);
            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Snap));

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);

            Assert.Equal(2, scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Snap));
        }

        [Fact]
        public async Task SuccessfulFetch_ReportsSuccessToThePool()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var scheduler = new FetchRequestScheduler(pool, new AlwaysSucceedsWorker(), new FetchRequestSchedulerOptions());
            var peerId = pool.ActivePeers.Single().Id;

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);

            Assert.Contains(peerId, pool.ReportSuccessCalls);
        }

        [Fact]
        public void SingleSnapQuarantineCycle_DoesNotDropThePeer()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peer = new FakeEthPeer(MakeEnode(1));

            scheduler.QuarantineSnapState(peer);

            Assert.Empty(pool.DropCalls);
            Assert.Equal(1, scheduler.GetSnapQuarantineCyclesForTest(peer.Id));
        }

        [Fact]
        public void RepeatedSnapQuarantineCycles_NoInterveningSuccess_DropsThePeer()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peer = new FakeEthPeer(MakeEnode(1));

            scheduler.QuarantineSnapState(peer);
            scheduler.QuarantineSnapState(peer);
            Assert.Empty(pool.DropCalls);

            scheduler.QuarantineSnapState(peer);

            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
            Assert.Equal(0, scheduler.GetSnapQuarantineCyclesForTest(peer.Id));
        }

        [Fact]
        public void SnapSuccessBetweenQuarantineCycles_ResetsCounter_PreventsDrop()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peer = new FakeEthPeer(MakeEnode(1));

            scheduler.QuarantineSnapState(peer);
            scheduler.QuarantineSnapState(peer);
            scheduler.RegisterSuccessForQuarantine(peer.Id, TimeoutStreakKind.Snap);

            scheduler.QuarantineSnapState(peer);
            scheduler.QuarantineSnapState(peer);

            Assert.Empty(pool.DropCalls);
            Assert.Equal(2, scheduler.GetSnapQuarantineCyclesForTest(peer.Id));
        }

        [Fact]
        public void RepeatedSnapQuarantineCycles_TrustedPeer_NeedsHigherThreshold_ThanOrdinaryPeer()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peer = new FakeEthPeer(MakeEnode(1), isTrusted: true);

            for (int i = 0; i < 19; i++)
                scheduler.QuarantineSnapState(peer);
            Assert.Empty(pool.DropCalls);

            scheduler.QuarantineSnapState(peer);

            Assert.Single(pool.DropCalls);
            Assert.Equal(peer.Id, pool.DropCalls[0].PeerId);
        }

        [Fact]
        public void SnapQuarantineCycles_DoNotAffectTheEthTimeoutStreakOrDrop()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peer = new FakeEthPeer(MakeEnode(1));

            scheduler.QuarantineSnapState(peer);
            scheduler.QuarantineSnapState(peer);

            Assert.Equal(0, scheduler.GetConsecutiveTimeoutsForTest(peer.Id, TimeoutStreakKind.Eth));
            Assert.Empty(pool.DropCalls);
        }

        [Fact]
        public void EthTimeoutStreak_DoesNotAffectTheSnapQuarantineCycleCount()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peerId = Guid.NewGuid();

            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out _);
            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out _);
            scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out _);

            Assert.Equal(0, scheduler.GetSnapQuarantineCyclesForTest(peerId));
            Assert.Empty(pool.DropCalls);
        }

        [Fact]
        public async Task SuccessOnNonSnapRequest_DoesNotClearAnExistingSnapQuarantineCycleCount()
        {
            var enodes = new[] { MakeEnode(1) };
            var pool = new FakePeerPool(enodes);
            var scheduler = new FetchRequestScheduler(pool, new AlwaysSucceedsWorker(), new FetchRequestSchedulerOptions());
            var peer = (FakeEthPeer)pool.ActivePeers.Single();

            scheduler.QuarantineSnapState(peer);
            scheduler.QuarantineSnapState(peer);
            Assert.Equal(2, scheduler.GetSnapQuarantineCyclesForTest(peer.Id));

            await scheduler.FetchHeadersAsync(0, 1, CancellationToken.None);

            Assert.Equal(2, scheduler.GetSnapQuarantineCyclesForTest(peer.Id));
        }

        [Fact]
        public void RegisterTimeoutForQuarantine_UnderConcurrentLoad_NeverLosesATrigger()
        {
            var pool = new FakePeerPool(Array.Empty<string>());
            var scheduler = new FetchRequestScheduler(pool, new NeverCalledWorker(), new FetchRequestSchedulerOptions());
            var peerId = Guid.NewGuid();

            const int totalTimeouts = 300;
            var triggerCount = 0;
            Parallel.For(0, totalTimeouts, _ =>
            {
                if (scheduler.RegisterTimeoutForQuarantine(peerId, TimeoutStreakKind.Eth, isTrusted: false, out _))
                    Interlocked.Increment(ref triggerCount);
            });

            Assert.True(triggerCount >= 1, "300 concurrent timeouts against one peer must trigger quarantine at least once");
            var final = scheduler.GetConsecutiveTimeoutsForTest(peerId, TimeoutStreakKind.Eth);
            Assert.True(final >= 0, $"counter must never go negative; got {final}");
        }

        private sealed class NeverCalledWorker : IPeerRequestWorker
        {
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class AlwaysSucceedsWorker : IPeerRequestWorker
        {
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                var list = new List<BlockHeader>();
                for (ulong i = 0; i < limit; i++) list.Add(new BlockHeader { BlockNumber = (long)(startBlock + i) });
                return Task.FromResult(list);
            }
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class AlwaysHangsWorker : IPeerRequestWorker
        {
            public async Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new List<BlockHeader>();
            }
            public async Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new List<BlockBody>();
            }
            public async Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return new List<List<Receipt>>();
            }
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class AlwaysThrowsWorkerTimeoutWorker : IPeerRequestWorker
        {
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
                => throw new TimeoutException("simulated SyncPeerSession.RequestTimeout hard timeout");
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new TimeoutException("simulated SyncPeerSession.RequestTimeout hard timeout");
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new TimeoutException("simulated SyncPeerSession.RequestTimeout hard timeout");
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

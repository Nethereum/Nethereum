using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientEmptyRangeProofTests
    {
        private static byte[] Filled(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private sealed class EmptyRangePeer : ISnapPeer
        {
            private readonly CancellationTokenSource _cts;
            private readonly int _cancelAfter;
            public int CallCount;

            public EmptyRangePeer(CancellationTokenSource cts = null, int cancelAfter = int.MaxValue)
            {
                _cts = cts;
                _cancelAfter = cancelAfter;
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                var n = Interlocked.Increment(ref CallCount);
                if (n >= _cancelAfter) _cts?.Cancel();
                return Task.FromResult(new AccountRangeMessage
                {
                    RequestId = r.RequestId,
                    Accounts = new List<AccountRangeMessage.AccountEntry>(),
                    Proof = new List<byte[]>(),
                });
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => Task.FromResult(new StorageRangesMessage { RequestId = r.RequestId, Slots = new(), Proof = new() });
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        private sealed class AlwaysThrowsStorageRangePeer : ISnapPeer
        {
            public int GetStorageRangesCallCount;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => throw new NotSupportedException("not exercised by this test");

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref GetStorageRangesCallCount);
                throw new FetchRequestFailedException("simulated peer failure", null);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        private sealed class StubSink : ISnapSyncSink
        {
            private byte[] _root;
            public void SetFinaliseRoot(byte[] root) => _root = root;
            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct) => default;
            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => new(new Scope());
            private sealed class Scope : IStorageScope
            {
                public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct) => default;
                public ValueTask EndAsync(CancellationToken ct) => default;
                public ValueTask AbortAsync(CancellationToken ct) => default;
            }
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct) => default;
            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct) => new(_root ?? new byte[32]);
        }

        [Fact]
        public async Task Empty_AccountRange_With_Valid_AbsenceProof_Completes()
        {
            var peer = new EmptyRangePeer();
            var sink = new StubSink();
            sink.SetFinaliseRoot(DefaultValues.EMPTY_TRIE_HASH);
            var client = new SnapSyncClient(peer, sink);

            await client.SyncStateAsync(DefaultValues.EMPTY_TRIE_HASH);

            Assert.Equal(16, peer.CallCount);
        }

        [Fact]
        public async Task Empty_AccountRange_Without_Valid_AbsenceProof_Is_Rejected_And_Retried()
        {
            using var cts = new CancellationTokenSource();
            var peer = new EmptyRangePeer(cts, cancelAfter: 32);
            var client = new SnapSyncClient(peer, new StubSink());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.SyncStateAsync(Filled(0x01), resumeFrom: null, checkpointSink: null, ct: cts.Token));

            Assert.True(peer.CallCount >= 32, $"expected retries past the 16 partitions; got {peer.CallCount}");
        }

        [Fact]
        public async Task Given_OneConsumerRepeatedlyFailingTheSameStage_When_FailureBudgetExceeded_Then_SnapTaskLeaseStalledExceptionThrown()
        {
            var peer = new EmptyRangePeer();
            var client = new SnapSyncClient(peer, new StubSink())
            {
                AccountConcurrency = 1,
                ActiveLeaseSameStageFailureBudget = 3,
            };

            await Assert.ThrowsAsync<SnapSyncClient.SnapTaskLeaseStalledException>(
                () => client.SyncStateAsync(Filled(0x01)));

            Assert.Equal(3, peer.CallCount);
        }

        [Fact]
        public async Task Given_StorageRangeFetchAlwaysFails_When_CallerFailureBudgetExceeded_Then_ExceptionPropagatesAndPeerStopsBeingCalled()
        {
            var peer = new AlwaysThrowsStorageRangePeer();
            var client = new SnapSyncClient(peer, new StubSink());
            var page = new SnapSyncClient.AccountWorkerResult();
            var owner = Filled(0x02);
            var storageRoot = Filled(0x03);
            var stateRoot = Filled(0x04);

            int failureCount = 0;
            const int budget = 3;
            void MarkFailure()
            {
                failureCount++;
                if (failureCount >= budget)
                    throw new SnapSyncClient.SnapTaskLeaseStalledException(TimeSpan.Zero, null, null);
            }

            await Assert.ThrowsAsync<SnapSyncClient.SnapTaskLeaseStalledException>(() =>
                client.FetchPageStorageAsync(
                    stateRoot,
                    new List<(byte[] Hash, byte[] Root)> { (owner, storageRoot) },
                    page,
                    new System.Collections.Concurrent.ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(),
                    new System.Collections.Concurrent.ConcurrentDictionary<string, DeferredStorageDebt>(),
                    fetchPivotBlock: null,
                    reqId: 1,
                    markProductive: () => { },
                    markFailure: MarkFailure,
                    taskSet: null,
                    taskIndex: 0,
                    cursoredWhalesSupported: false,
                    getLiveRoot: () => stateRoot,
                    CancellationToken.None));

            Assert.Equal(budget, peer.GetStorageRangesCallCount);
        }
    }
}

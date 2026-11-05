using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class SchedulerTargetRootChangedTests
    {
        private static byte[] Fill(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        private static BlockHeader Header(int blockNumber, byte[] stateRoot)
            => new() { BlockNumber = blockNumber, StateRoot = stateRoot };

        [Fact]
        public async Task Given_ReconcileFlatStateAsyncWithAScheduler_When_TheHealRetargets_Then_TheSchedulerIsToldTheTargetRootChanged()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new RecordingScheduler();
            var pivot = new SnapBootstrapper.RollingPivot(Header(100, Fill(0xCD)), Fill(0x01));
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> refresher =
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((Header(200, DefaultValues.EMPTY_TRIE_HASH), Fill(0x02)));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var committed = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler, pivot, refresher, metrics: null,
                NullLogger.Instance, cts.Token);

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, committed.Header.StateRoot);
            Assert.True(scheduler.TargetRootChangedCalls >= 1, "the scheduler was never told the target root changed");
        }

        [Fact]
        public async Task Given_ReconcileFlatStateAsyncWithAScheduler_When_TheHealDoesNotRetarget_Then_TheSchedulerIsNotToldTheTargetRootChanged()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new RecordingScheduler();
            var pivot = new SnapBootstrapper.RollingPivot(Header(100, DefaultValues.EMPTY_TRIE_HASH), Fill(0x01));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var committed = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler, pivot, pivotRefresher: null, metrics: null,
                NullLogger.Instance, cts.Token);

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, committed.Header.StateRoot);
            Assert.Equal(0, scheduler.TargetRootChangedCalls);
        }

        private sealed class RecordingScheduler : IFetchRequestScheduler
        {
            private int _targetRootChangedCalls;

            public int TargetRootChangedCalls => Volatile.Read(ref _targetRootChangedCalls);

            public void OnTargetRootChanged() => Interlocked.Increment(ref _targetRootChangedCalls);

            public Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new FetchRequestFailedException("account proof unavailable at the scripted root", null);

            public Task<StorageRangesMessage> FetchStorageRangesAsync(
                byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash,
                ulong responseBytes, CancellationToken ct)
                => throw new FetchRequestFailedException("storage range unavailable at the scripted root", null);

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}

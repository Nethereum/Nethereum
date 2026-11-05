using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperPhase1BackfillCharacterizationTests
    {
        private const ulong Pivot = 6;
        private const int ChainLength = 12;

        private sealed class NoCodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private sealed class Harness : IDisposable
        {
            public InMemoryContentNodeStore State { get; } = new();
            public byte[] Root { get; }
            public CharChain Chain { get; }
            public InMemoryChainStoreBundle Bundle { get; } = InMemoryChainStoreBundle.Open();
            public CharLog Log { get; } = new();
            public List<(ulong Start, ulong Limit, bool Reverse)> HeaderRequests { get; } = new();

            public Harness()
            {
                var keccak = Sha3Keccack.Current;
                var trie = new PatriciaTrie(State);
                for (byte i = 1; i <= 3; i++)
                    trie.Put(keccak.CalculateHash(new[] { i }), new AccountEncoder().Encode(new Account
                    {
                        Nonce = (EvmUInt256)i,
                        Balance = (EvmUInt256)(i * 10u),
                        StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                        CodeHash = DefaultValues.EMPTY_DATA_HASH,
                    }));
                trie.SaveDirtyNodesToStorage();
                Root = trie.Root.GetHash();
                Chain = new CharChain(ChainLength, new Dictionary<ulong, byte[]> { [Pivot] = Root, [Pivot + 3] = Root });
                Bundle.Metadata.SetLastFetchedBody(ChainLength);
            }

            public BlockHeader PivotHeader => Chain.HeaderAt(Pivot);
            public byte[] PivotHash => Chain.HashAt(Pivot);

            public CharScheduler Scheduler(Action<ulong, ulong, bool> onHeaders = null) => new()
            {
                HeadersDirected = (start, limit, reverse, ct) =>
                {
                    lock (HeaderRequests) HeaderRequests.Add((start, limit, reverse));
                    onHeaders?.Invoke(start, limit, reverse);
                    return Chain.Serve(start, limit, reverse);
                },
            };

            public async Task<SnapBootstrapper.Result> RunAsync(
                CharScheduler scheduler, bool externalHeaderFollow, (ulong From, ulong To)? sweepOverride = null,
                Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>> refresher = null,
                SnapBootstrapper.RollingPivot rolling = null, bool layPivotHeader = true,
                CancellationToken ct = default, bool awaitHistory = true)
            {
                if (layPivotHeader) await Bundle.Blocks.SaveAsync(PivotHeader, PivotHash);
                var result = await SnapBootstrapper.RunAsync(
                    Bundle, new InProcessSnapPeer(new PatriciaSnapRequestHandler(State, new NoCodes())),
                    PivotHeader, PivotHash, Log,
                    new SnapRunOptions
                    {
                        Scheduler = scheduler,
                        Pool = new CharPool(),
                        RunBackfill = true,
                        UseBackwardSkeleton = true,
                        ExternalHeaderFollow = externalHeaderFollow,
                        HeaderSweepOverride = sweepOverride,
                        PivotRefresher = refresher,
                        RollingPivot = rolling,
                        AccountConcurrency = 1,
                    },
                    ct);
                if (awaitHistory) await result.HistoryBackfill.WaitAsync(TimeSpan.FromSeconds(30));
                return result;
            }

            public void Dispose() => Bundle.Dispose();
        }

        [Fact]
        public async Task Given_TheLegacySkeletonBackfillAndNoCursor_When_Phase1Runs_Then_ItSweepsFromThePivotToGenesisAndRecordsTheDescent()
        {
            using var h = new Harness();

            var result = await h.RunAsync(h.Scheduler(), externalHeaderFollow: false);

            Assert.True(result.Ran);
            Assert.Equal(
                "snap.phase1.headers sweep source=Pivot from=6 to=0 pivot=6",
                Assert.Single(h.Log.With("snap.phase1.headers sweep")).Message);
            Assert.Equal(Pivot, HeaderSubchains.TrustedTip(h.Bundle.Metadata.GetHeaderSyncState()));
            Assert.False(h.Log.Saw("override"));
        }

        [Fact]
        public async Task Given_TheLegacySkeletonBackfillAndACursorWhoseHeaderIsMissing_When_Phase1Runs_Then_ItLogsTheDesyncAsAnErrorAndSweepsFromThePivot()
        {
            using var h = new Harness();
            h.Bundle.Metadata.SetLastFetchedHeader(3);

            await h.RunAsync(h.Scheduler(), externalHeaderFollow: false);

            var error = Assert.Single(h.Log.With("points to a MISSING header"));
            Assert.Equal(LogLevel.Error, error.Level);
            Assert.StartsWith(
                "snap.phase1.headers cursor=3 points to a MISSING header (data/cursor desync). Sweeping from pivot will NOT descend past existing headers",
                error.Message);
            Assert.False(h.Log.Saw("snap.phase1.headers sweep source="));
        }

        [Fact]
        public async Task Given_TheLegacySkeletonBackfillAndACursorWhoseHeaderIsStored_When_Phase1Runs_Then_ItResumesTheSweepFromTheCursor()
        {
            using var h = new Harness();
            await h.Bundle.Blocks.SaveAsync(h.Chain.HeaderAt(3), h.Chain.HashAt(3));
            h.Bundle.Metadata.SetLastFetchedHeader(3);

            await h.RunAsync(h.Scheduler(), externalHeaderFollow: false);

            Assert.Equal(
                "snap.phase1.headers sweep source=Cursor from=3 to=0 pivot=6",
                Assert.Single(h.Log.With("snap.phase1.headers sweep")).Message);
        }

        [Fact]
        public async Task Given_TheLegacySkeletonBackfillAndAnOverrideAnchoredOnAStoredHeader_When_Phase1Runs_Then_TheCursorIsHealedAndTheOverrideRangeIsRefetched()
        {
            using var h = new Harness();
            await h.Bundle.Blocks.SaveAsync(h.Chain.HeaderAt(5), h.Chain.HashAt(5));

            await h.RunAsync(h.Scheduler(), externalHeaderFollow: false, sweepOverride: (5UL, 1UL));

            Assert.Equal(
                "snap.phase1.headers sweep source=Override from=5 to=1 pivot=6",
                Assert.Single(h.Log.With("snap.phase1.headers sweep")).Message);
            Assert.Equal(
                "snap.phase1.headers override healed the header cursor to 5 (progress + future cursor resumes now track the real frontier)",
                Assert.Single(h.Log.With("override healed the header cursor")).Message);
            Assert.Contains(h.HeaderRequests, r => r.Start == 5 && r.Reverse);
        }

        [Fact]
        public async Task Given_TheLegacySkeletonBackfillAndAnOverrideWithNoStoredAnchor_When_Phase1Runs_Then_TheSweepIsSkippedWithAnError()
        {
            using var h = new Harness();

            await h.RunAsync(h.Scheduler(), externalHeaderFollow: false, sweepOverride: (9UL, 1UL));

            var error = Assert.Single(h.Log.With("has no stored header to anchor on"));
            Assert.Equal(LogLevel.Error, error.Level);
            Assert.Equal(
                "snap.phase1.headers sweep source=Override from=9 has no stored header to anchor on; skipping header sweep (set HeadersFrom/HeadersTo to a stored block)",
                error.Message);
            Assert.Empty(h.HeaderRequests);
        }

        [Fact]
        public async Task Given_ThePivotRefresherReturnsAHigherPivotDuringTheSweep_When_TheLegacySkeletonBackfillRuns_Then_TheSkeletonIsExtendedToTheNewTipAndTheExtensionIsLogged()
        {
            using var h = new Harness();
            await h.Bundle.Blocks.SaveAsync(h.Chain.HeaderAt(9), h.Chain.HashAt(9));
            var refreshes = new List<bool>();

            await h.RunAsync(
                h.Scheduler(), externalHeaderFollow: false,
                refresher: (force, ct) =>
                {
                    lock (refreshes) refreshes.Add(force);
                    return Task.FromResult<(BlockHeader Header, byte[] Hash)?>(
                        force ? null : (h.Chain.HeaderAt(9), h.Chain.HashAt(9)));
                });

            Assert.Contains(false, refreshes);
            Assert.StartsWith("snap.phase1.pivot_extension headers [", Assert.Single(h.Log.With("snap.phase1.pivot_extension")).Message);
            Assert.Equal(9UL, HeaderSubchains.TrustedTip(h.Bundle.Metadata.GetHeaderSyncState()));
        }

        [Fact]
        public async Task Given_ExternalHeaderFollowAndAnOverrideAnchoredOnAStoredHeader_When_Phase1Runs_Then_TheRangeIsRelaidWithoutTouchingTheSkeleton()
        {
            using var h = new Harness();
            await h.Bundle.Blocks.SaveAsync(h.Chain.HeaderAt(5), h.Chain.HashAt(5));

            await h.RunAsync(h.Scheduler(), externalHeaderFollow: true, sweepOverride: (5UL, 1UL));

            Assert.Equal(
                "snap.phase1.headers override forced re-lay from=5 to=1 (data + cursor only; skeleton owned by the Headers job)",
                Assert.Single(h.Log.With("override forced re-lay")).Message);
            Assert.Contains(h.HeaderRequests, r => r.Start == 5 && r.Reverse);
            Assert.False(h.Log.Saw("snap.phase1.headers sweep source="));
            Assert.Equal(0UL, HeaderSubchains.TrustedTip(h.Bundle.Metadata.GetHeaderSyncState()));
        }

        [Fact]
        public async Task Given_ExternalHeaderFollowAndAnOverrideWithNoStoredAnchor_When_Phase1Runs_Then_TheRepairIsSkippedWithAnError()
        {
            using var h = new Harness();

            await h.RunAsync(h.Scheduler(), externalHeaderFollow: true, sweepOverride: (9UL, 1UL));

            var error = Assert.Single(h.Log.With("has no stored header to anchor on"));
            Assert.Equal(LogLevel.Error, error.Level);
            Assert.Equal(
                "snap.phase1.headers override from=9 has no stored header to anchor on; skipping repair",
                error.Message);
            Assert.Empty(h.HeaderRequests);
        }

        [Fact]
        public async Task Given_ExternalHeaderFollowWhoseRepairAdoptsAHigherPivot_When_Phase1Runs_Then_TheFillIsRerunToTheExtendedPivotAndWaitsForTheMissingBodies()
        {
            using var control = new Harness();
            control.Bundle.Metadata.SetLastFetchedBody(Pivot);
            await control.Bundle.Blocks.SaveAsync(control.Chain.HeaderAt(5), control.Chain.HashAt(5));
            await control.RunAsync(control.Scheduler(), externalHeaderFollow: true, sweepOverride: (5UL, 1UL));

            using var h = new Harness();
            h.Bundle.Metadata.SetLastFetchedBody(Pivot);
            await h.Bundle.Blocks.SaveAsync(h.Chain.HeaderAt(5), h.Chain.HashAt(5));
            await h.Bundle.Blocks.SaveAsync(h.Chain.HeaderAt(9), h.Chain.HashAt(9));
            var rolling = new SnapBootstrapper.RollingPivot(h.PivotHeader, h.PivotHash);
            using var cts = new CancellationTokenSource();
            var scheduler = h.Scheduler((start, limit, reverse) =>
                rolling.Adopt(new SnapBootstrapper.PivotState(h.Chain.HeaderAt(9), h.Chain.HashAt(9))));

            var result = await h.RunAsync(
                scheduler, externalHeaderFollow: true, sweepOverride: (5UL, 1UL), rolling: rolling,
                ct: cts.Token, awaitHistory: false);
            var finished = await Task.WhenAny(result.HistoryBackfill, Task.Delay(TimeSpan.FromSeconds(1.5)));
            var completedWithinTheWindow = finished == result.HistoryBackfill;
            cts.Cancel();
            try { await result.HistoryBackfill.WaitAsync(TimeSpan.FromSeconds(30)); } catch (OperationCanceledException) { }

            Assert.Equal(9UL, (ulong)rolling.Current.Header.BlockNumber);
            Assert.False(completedWithinTheWindow);
        }
    }
}

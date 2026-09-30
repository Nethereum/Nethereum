using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class Snap2BootstrapTests
    {
        private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

        private static string Root(Snap2BootstrapBed bed, ulong block) => bed.Fixture.HeaderAt(block).StateRoot.ToHex();

        private static string FirstHalfStart => new byte[32].ToHex();

        [Fact]
        public async Task Given_ASnap2Download_When_TheRollingPivotAdvancesTwiceBeforeTheDrainCompletes_Then_CatchUpAppliesEveryBlockUpToTheLatestAdoptedPivot()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(6));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            peer.OnHeld = () => bed.MoveTipTo(4);
            peer.OnReleased = () => bed.MoveTipTo(6);
            peer.DrainDelay = TimeSpan.FromMilliseconds(300);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, peer, cts.Token);

            Assert.Equal(bed.Hashes(3, 6), bed.RequestedBalBlocks());
            Assert.Equal(6UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 6), result.PivotStateRoot.ToHex());
            Assert.Equal((EvmUInt256)Snap2Chain.BalanceAt(6), (await bed.FlatAccountAsync(Snap2Chain.LowA)).Balance);
        }

        [Fact]
        public async Task Given_ASnap2Download_When_TheRollingPivotAdvancesOnce_Then_CatchUpAppliesExactlyTheGap()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(6));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            peer.OnHeld = () => bed.MoveTipTo(4);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, peer, cts.Token);

            Assert.Equal(bed.Hashes(3, 4), bed.RequestedBalBlocks());
            Assert.Equal(4UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 4), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap2Download_When_ThePivotMovesAndTheCatchUpAppliesNothing_Then_GenerationFails()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(6));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            peer.OnHeld = () => bed.MoveTipTo(4);
            using var cts = new CancellationTokenSource(RunTimeout);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bed.RunAsync(2, peer, cts.Token, applier: new NoOpApplier()));

            Assert.Contains("snap.generate.root_mismatch", ex.Message);
            Assert.Contains($"expected=0x{Root(bed, 4)}", ex.Message);
            Assert.Equal(bed.Hashes(3, 4), bed.RequestedBalBlocks());
        }

        [Fact]
        public async Task Given_TwoSuccessivePivotMoves_When_BothAreCaughtUpInTheSameRun_Then_TheGeneratedRootReflectsBoth()
            => await CatchUpSuccessiveMovesAsync(2, 4, 6);

        [Fact]
        public async Task Given_FiveSuccessivePivotMoves_When_EachCatchesUpAMultiBlockGapInTheSameRun_Then_TheGeneratedRootReflectsAll()
            => await CatchUpSuccessiveMovesAsync(2, 4, 6, 8, 10, 12);

        private static async Task CatchUpSuccessiveMovesAsync(params ulong[] pivots)
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build((int)pivots[^1]));
            bed.SaveState(SnapPhase.Phase2Running, pivots[0], Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(pivots[0]);
            var heldRoots = pivots.Take(pivots.Length - 1).Select(p => Root(bed, p)).ToHashSet();
            var moves = 0;
            var peer = bed.Peer();
            peer.Hold = r => heldRoots.Contains(r.RootHash.ToHex()) && ByteUtil.AreEqual(r.StartingHash, Snap2BootstrapBed.SecondHalfStart);
            peer.OnHeld = () => bed.MoveTipTo(pivots[Math.Min(Interlocked.Increment(ref moves), pivots.Length - 1)]);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(pivots[0], peer, cts.Token);

            Assert.Equal(pivots.Length - 1, moves);
            Assert.Equal(pivots.Length - 1, bed.Log.Messages.Count(m => m.StartsWith("snap.phase2.pivot_move.bal_healed", StringComparison.Ordinal)));
            Assert.Equal(bed.Hashes(pivots[0] + 1, pivots[^1]), bed.RequestedBalBlocks());
            Assert.Equal((EvmUInt256)Snap2Chain.BalanceAt(pivots[^1]), (await bed.FlatAccountAsync(Snap2Chain.LowA)).Balance);
            Assert.Equal(pivots[^1], result.PivotBlockNumber);
            Assert.Equal(Root(bed, pivots[^1]), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap2DownloadAtPivotP_When_TheRefresherAdoptsPPrimeBeforeACheckpoint_Then_TheCheckpointPersistsP()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4));

            var persisted = await PivotPersistedWhenPhase2EndsAsync(bed, balHealEnabled: true);

            Assert.Equal(2UL, persisted);
        }

        [Fact]
        public async Task Given_ASnap1DownloadAtPivotP_When_TheRefresherAdoptsPPrimeBeforeACheckpoint_Then_TheCheckpointPersistsPPrime()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4));

            var persisted = await PivotPersistedWhenPhase2EndsAsync(bed, balHealEnabled: false);

            Assert.Equal(4UL, persisted);
        }

        private static async Task<ulong?> PivotPersistedWhenPhase2EndsAsync(Snap2BootstrapBed bed, bool balHealEnabled)
        {
            bed.MoveTipTo(2);
            ulong? persisted = null;
            bed.Log.OnMessage = m =>
            {
                if (m.StartsWith("Snap-bootstrap: state populated", StringComparison.Ordinal))
                    persisted = bed.Bundle.Metadata.GetSnapSyncState()?.PivotBlockNumber;
            };
            var peer = bed.Peer();
            peer.AfterRequest = _ => bed.Rolling.Adopt(new SnapBootstrapper.PivotState(bed.Fixture.HeaderAt(4), bed.Fixture.HashAt(4)));
            using var cts = new CancellationTokenSource(RunTimeout);

            try
            {
                await bed.RunAsync(2, peer, cts.Token, balHealEnabled: balHealEnabled, rootRefreshIntervalMs: 60_000);
            }
            catch (Exception) when (persisted.HasValue)
            {
            }

            Assert.Equal(4UL, (ulong)bed.Rolling.Current.Header.BlockNumber);
            return persisted;
        }

        [Fact]
        public async Task Given_ASnap2CheckpointAtPivotP_When_TheNodeRestartsWithTheTipPastP_Then_BalsForPPlus1ToTheNewPivotAreAppliedBeforePhase2Resumes()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(5));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.MoveTipTo(5);
            var peer = bed.Peer();
            int? balsBeforeFirstRange = null;
            peer.OnAccountRange = _ => balsBeforeFirstRange ??= bed.RequestedBalBlocks().Count;
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(5, peer, cts.Token);

            Assert.Equal(bed.Hashes(3, 5), bed.RequestedBalBlocks());
            Assert.Equal(3, balsBeforeFirstRange);
            Assert.All(peer.AccountRanges, r => Assert.Equal(Root(bed, 5), r.Root));
            Assert.DoesNotContain(peer.AccountRanges, r => r.Start == FirstHalfStart);
            Assert.Equal(5UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 5), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap2CheckpointAtPivotP_When_TheNodeRestartsWithTheTipStillAtP_Then_NoBalIsRequested()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(5));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.MoveTipTo(2);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, bed.Peer(), cts.Token);

            Assert.Empty(bed.RequestedBalBlocks());
            Assert.Equal(2UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 2), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASavedPhase2PivotNoLongerCanonical_When_RunAsyncStarts_Then_TheRouterResetsAndTheSameRunProceedsFresh()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(3));
            await bed.DownloadFlatAtAsync(2);
            var ghost = BalCatchUpBed.Filled(0x11);
            await bed.WriteGhostAccountAsync(ghost);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched(), pivotHash: BalCatchUpBed.Filled(0xab));
            bed.MoveTipTo(3);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(3, bed.Peer(), cts.Token);

            Assert.True(bed.Log.Contains("snap.bootstrap.snap2_reset reason=not_canonical"), "the router did not reset the non-canonical saved pivot");
            Assert.Null(await bed.Flat.GetAccountByHashAsync(ghost));
            Assert.Equal(3UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 3), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ACanonicalAmsterdamPhase2StateWithTasks_When_RunAsyncStarts_Then_ItIsKeptAndItsFlatRowsSurvive()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(3));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.Peer();
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, peer, cts.Token);

            Assert.False(bed.Log.Contains("snap.bootstrap.snap2_reset"), "a usable saved state was reset");
            Assert.DoesNotContain(peer.AccountRanges, r => r.Start == FirstHalfStart);
            Assert.Equal(2UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 2), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_FlatRowsAboveTheDurableFrontierFromARevertedPage_When_ThePivotMovesAndAnAccountThereWasDeleted_Then_TheRowsArePrunedAndTheGeneratedRootMatches()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(3, emptyHighBAt: 3));
            await bed.DownloadFlatAtAsync(2);
            Assert.NotNull(await bed.FlatAccountAsync(Snap2Chain.HighB));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.MoveTipTo(3);
            var peer = bed.Peer();
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(3, peer, cts.Token);

            Assert.Null(await bed.FlatAccountAsync(Snap2Chain.HighB));
            Assert.Equal((EvmUInt256)Snap2Chain.BalanceAt(3), (await bed.FlatAccountAsync(Snap2Chain.LowA)).Balance);
            Assert.NotNull(await bed.FlatAccountAsync(Snap2Chain.LowB));
            Assert.DoesNotContain(peer.AccountRanges, r => r.Start == FirstHalfStart);
            Assert.Equal(3UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 3), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap2WhaleWhosePagesSpanTwoPivots_When_ItsLastSubtaskCompletes_Then_NoDebtIsRecordedAndTheGeneratedRootMatches()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.BuildWithWhale(4, rewriteWhaleFrom: 3));
            bed.MoveTipTo(2);
            var whale = new WhaleServing(bed);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, whale.Peer, cts.Token);

            Assert.True(whale.PagesAt(2) >= 2, "the whale was not paged at the first pivot");
            Assert.True(whale.CursoredPagesAt(4) >= 1, "the whale did not resume as cursored subtasks at the second pivot");
            Assert.False(bed.Log.Contains("snap.phase2.bigaccount.deferred"), "snap/2 recorded a big-account debt");
            Assert.Equal(bed.Hashes(3, 4), bed.RequestedBalBlocks());
            Assert.Equal(4UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 4), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap1WhaleWhosePagesSpanTwoPivots_When_ItsLastSubtaskCompletes_Then_ABigAccountDebtIsRecorded()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.BuildWithWhale(4, rewriteWhaleFrom: 3));
            bed.MoveTipTo(2);
            var whale = new WhaleServing(bed);
            using var cts = new CancellationTokenSource(RunTimeout);

            try { await bed.RunAsync(2, whale.Peer, cts.Token, balHealEnabled: false); }
            catch (Exception) { }

            Assert.True(whale.CursoredPagesAt(4) >= 1, "the whale did not resume as cursored subtasks at the second pivot");
            Assert.True(bed.Log.Contains("snap.phase2.bigaccount.deferred reason=BigAccountChunked"), "snap/1 no longer records the big-account debt");
        }

        private sealed class WhaleServing
        {
            private readonly byte[] _whale = Sha3Keccack.Current.CalculateHash(Snap2Chain.Whale.HexToByteArray());
            private readonly System.Collections.Concurrent.ConcurrentBag<(ulong Block, bool Cursored)> _pages = new();

            public WhaleServing(Snap2BootstrapBed bed)
            {
                var firstRoot = bed.Fixture.HeaderAt(2).StateRoot;
                Peer = bed.Peer(softResponseLimit: 400);
                Peer.HoldStorage = r => ByteUtil.AreEqual(r.RootHash, firstRoot) && PagesAt(2) >= 2;
                Peer.AfterStorage = r =>
                {
                    if (!r.AccountHashes.Any(h => ByteUtil.AreEqual(h, _whale))) return;
                    var block = ByteUtil.AreEqual(r.RootHash, firstRoot) ? 2UL : ByteUtil.AreEqual(r.RootHash, bed.Fixture.HeaderAt(4).StateRoot) ? 4UL : 0UL;
                    _pages.Add((block, r.StartingHash is { Length: > 0 }));
                    if (block == 2 && PagesAt(2) == 2) bed.MoveTipTo(4);
                };
            }

            public HoldingSnapPeer Peer { get; }

            public int PagesAt(ulong block) => _pages.Count(p => p.Block == block);

            public int CursoredPagesAt(ulong block) => _pages.Count(p => p.Block == block && p.Cursored);
        }

        [Fact]
        public async Task Given_ASnap2BootstrapWhoseFlatStateCannotProduceTheFrozenRoot_When_RunAsyncReachesTheEnd_Then_ItThrowsNamingBothRootsAndThePersistedPhaseIsGenerating()
        {
            using var bed = await InconsistentFlatAtPivot2Async();
            using var cts = new CancellationTokenSource(RunTimeout);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bed.RunAsync(2, bed.Peer(), cts.Token));

            Assert.Contains("generated=0x", ex.Message);
            Assert.Contains($"expected=0x{Root(bed, 2)}", ex.Message);
            var saved = bed.Bundle.Metadata.GetSnapSyncState();
            Assert.Equal(SnapPhase.Generating, saved.Phase);
            Assert.Equal(2UL, saved.PivotBlockNumber);
        }

        [Fact]
        public async Task Given_AConsistentSnap2FlatState_When_RunAsyncReachesTheEndWhileTheRollingPivotHasMovedOn_Then_ItPublishesTheAppliedPivot()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.Peer();
            peer.AfterRequest = _ => bed.Rolling.Adopt(new SnapBootstrapper.PivotState(bed.Fixture.HeaderAt(4), bed.Fixture.HashAt(4)));
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, peer, cts.Token, rootRefreshIntervalMs: 60_000);

            Assert.Equal(4UL, (ulong)bed.Rolling.Current.Header.BlockNumber);
            Assert.Equal(2UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 2), result.PivotStateRoot.ToHex());
        }

        private static async Task<Snap2BootstrapBed> InconsistentFlatAtPivot2Async()
        {
            var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            await bed.DownloadFlatAtAsync(2);
            await bed.CorruptFlatAccountAsync(Snap2Chain.LowA);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.MoveTipTo(2);
            return bed;
        }

        [Fact]
        public async Task Given_ASnap2GeneratingStatePersisted_When_TheNodeRestartsWithTheTipFarAhead_Then_GenerationResumesAgainstTheFrozenPivotWithNoPhase2AndNoBalFetch()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(6));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Generating, 2, Array.Empty<SnapSyncAccountTask>());
            bed.MoveTipTo(6);
            var peer = bed.Peer();
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(6, peer, cts.Token);

            Assert.Equal(0, peer.Requests);
            Assert.Empty(peer.AccountRanges);
            Assert.Empty(bed.RequestedBalBlocks());
            Assert.Equal(2UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 2), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap2GeneratingStateWhosePivotIsNoLongerCanonical_When_TheNodeRestarts_Then_TheRouterResetsAndTheSameRunProceedsFresh()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(6));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Generating, 2, Array.Empty<SnapSyncAccountTask>(), pivotHash: BalCatchUpBed.Filled(0xab));
            bed.MoveTipTo(6);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(6, bed.Peer(), cts.Token);

            Assert.True(bed.Log.Contains("snap.bootstrap.snap2_reset reason=not_canonical"), "the non-canonical frozen pivot was not reset");
            Assert.Equal(6UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 6), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ACompletedSnap2PhaseTwo_When_TheGateAndTheGeneratingPersistRun_Then_GeneratingIsDurableBeforeGenerationStartsAndAFailedGateLeavesPhase2Running()
        {
            using (var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2)))
            {
                bed.MoveTipTo(2);
                SnapPhase? phaseWhenGenerationStarted = null;
                bed.Log.OnMessage = m =>
                {
                    if (m.StartsWith("snap.generate.start", StringComparison.Ordinal))
                        phaseWhenGenerationStarted = bed.Bundle.Metadata.GetSnapSyncState()?.Phase;
                };
                using var cts = new CancellationTokenSource(RunTimeout);

                var result = await bed.RunAsync(2, bed.Peer(), cts.Token);

                Assert.Equal(SnapPhase.Generating, phaseWhenGenerationStarted);
                Assert.Equal(2UL, result.PivotBlockNumber);
            }

            using (var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2)))
            {
                await bed.DownloadFlatAtAsync(2);
                bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
                bed.Bundle.Metadata.SaveDeferredHealAccountsBlob(BalCatchUpBed.Filled(0x11).Concat(BalCatchUpBed.Filled(0x22)).ToArray());
                bed.MoveTipTo(2);
                using var cts = new CancellationTokenSource(RunTimeout);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bed.RunAsync(2, bed.Peer(), cts.Token));

                Assert.Contains("storage completeness gate failed", ex.Message);
                Assert.Equal(SnapPhase.Phase2Running, bed.Bundle.Metadata.GetSnapSyncState().Phase);
            }
        }

        [Fact]
        public async Task Given_ASnap2PhaseTwoThatCompletedButNeverPersistedGenerating_When_TheNodeRestartsWithTheTipAhead_Then_ItResumesPhase2WithAllTasksDoneCatchesUpAndGenerates()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4));
            await bed.DownloadFlatAtAsync(2);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched());
            bed.Bundle.Metadata.SaveDeferredHealAccountsBlob(BalCatchUpBed.Filled(0x11).Concat(BalCatchUpBed.Filled(0x22)).ToArray());
            bed.MoveTipTo(2);
            using (var cts = new CancellationTokenSource(RunTimeout))
                await Assert.ThrowsAsync<InvalidOperationException>(() => bed.RunAsync(2, bed.Peer(), cts.Token));
            var afterCrash = bed.Bundle.Metadata.GetSnapSyncState();
            Assert.Equal(SnapPhase.Phase2Running, afterCrash.Phase);
            Assert.NotEmpty(afterCrash.Tasks);

            bed.Bundle.Metadata.ClearDeferredHealAccountsBlob();
            bed.MoveTipTo(4);
            using var restart = new CancellationTokenSource(RunTimeout);
            var result = await bed.RunAsync(4, bed.Peer(), restart.Token);

            Assert.Equal(bed.Hashes(3, 4), bed.RequestedBalBlocks());
            Assert.Equal(4UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 4), result.PivotStateRoot.ToHex());
        }

        [Theory]
        [InlineData("Phase3Running", "phase")]
        [InlineData("CompleteOrphan", "phase")]
        [InlineData("NotStarted", "phase")]
        [InlineData("PreAmsterdamPhase2Running", "pre_amsterdam")]
        [InlineData("Phase2RunningWithZeroTasks", "no_tasks")]
        public async Task Given_Snap2AndASavedState_When_RunAsyncStarts_Then_UnusableStatesAreResetBeforeAnySinkOpens(string state, string reason)
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4));
            await bed.DownloadFlatAtAsync(2);
            var ghost = BalCatchUpBed.Filled(0x11);
            await bed.WriteGhostAccountAsync(ghost);
            switch (state)
            {
                case "Phase3Running": bed.SaveState(SnapPhase.Phase3Running, 4, Snap2BootstrapBed.SecondHalfUnfetched()); break;
                case "CompleteOrphan": bed.SaveState(SnapPhase.Complete, 4, Array.Empty<SnapSyncAccountTask>()); break;
                case "NotStarted": bed.SaveState(SnapPhase.NotStarted, 4, Snap2BootstrapBed.SecondHalfUnfetched()); break;
                case "PreAmsterdamPhase2Running": bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.SecondHalfUnfetched()); break;
                case "Phase2RunningWithZeroTasks": bed.SaveState(SnapPhase.Phase2Running, 4, Array.Empty<SnapSyncAccountTask>()); break;
            }
            bed.MoveTipTo(4);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(4, bed.Peer(), cts.Token, activations: new ForkFromBlock(3));

            Assert.True(bed.Log.Contains($"snap.bootstrap.snap2_reset reason={reason}"), $"expected a snap2_reset with reason={reason}");
            Assert.Null(await bed.Flat.GetAccountByHashAsync(ghost));
            Assert.Equal(4UL, result.PivotBlockNumber);
            Assert.Equal(Root(bed, 4), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_ASnap2DownloadOnARocksDbBundle_When_Phase2CompletesBeforeGeneration_Then_NoTrieColumnFamilyHoldsAnyRow()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            bed.MoveTipTo(2);
            long? trieRowsWhenGenerationStarted = null;
            bed.Log.OnMessage = m =>
            {
                if (m.StartsWith("snap.generate.start", StringComparison.Ordinal))
                    trieRowsWhenGenerationStarted = bed.CountTrieRows();
            };
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, bed.Peer(), cts.Token);

            Assert.Equal(0L, trieRowsWhenGenerationStarted);
            Assert.True(bed.CountTrieRows() > 0, "generation wrote no trie");
            Assert.Equal(Root(bed, 2), result.PivotStateRoot.ToHex());
        }

        [Fact]
        public async Task Given_CatchUpThrowsResetRequiredMidFlight_When_RunSnap2AsyncHandlesIt_Then_TheSinkIsAbandonedTheBackfillCancelledTheStateResetAndTheExceptionPropagates()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            peer.OnHeld = () =>
            {
                bed.Bundle.Blocks.UpdateBlockHashAsync(2, BalCatchUpBed.Filled(0xcd)).GetAwaiter().GetResult();
                bed.MoveTipTo(4);
            };
            using var cts = new CancellationTokenSource(RunTimeout);

            var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => bed.RunAsync(2, peer, cts.Token, runBackfill: true));

            Assert.Equal("SnapSyncResetRequiredException", ex.GetType().Name);
            Assert.Contains("pivot_reorged", ex.Message);
            Assert.True(bed.Scheduler.MaxLiveRequests >= 1, "the backfill never ran, so its cancellation is unobserved");
            Assert.Equal(0, bed.Scheduler.LiveRequests);
            Assert.Null(bed.Bundle.Metadata.GetSnapSyncState());
            Assert.Null(await bed.FlatAccountAsync(Snap2Chain.LowA));
        }

        [Fact]
        public async Task Given_CatchUpFailsWithARetryableErrorMidFlight_When_RunSnap2AsyncHandlesIt_Then_TheFlatRowsAndTheSavedStateSurvive()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(4), missingCanonical: 3);
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            peer.OnHeld = () => bed.MoveTipTo(4);
            using var cts = new CancellationTokenSource(RunTimeout);

            var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => bed.RunAsync(2, peer, cts.Token, runBackfill: true));

            Assert.NotEqual("SnapSyncResetRequiredException", ex.GetType().Name);
            Assert.Contains("canonical header 3", ex.Message);
            Assert.Equal(0, bed.Scheduler.LiveRequests);
            var saved = bed.Bundle.Metadata.GetSnapSyncState();
            Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
            Assert.Equal(2UL, saved.PivotBlockNumber);
            Assert.NotNull(await bed.FlatAccountAsync(Snap2Chain.LowA));
        }

        [Fact]
        public async Task Given_ASnap2BootstrapOnTheDefaultLoopBackfillWhoseEndStepFails_When_RunAsyncIsCalledThreeTimesInARow_Then_AtMostOneBackfillIsEverLiveAndNoneIsLiveAfterEachThrow()
        {
            using var bed = await InconsistentFlatAtPivot2Async();

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using var cts = new CancellationTokenSource(RunTimeout);
                await Assert.ThrowsAnyAsync<Exception>(() => bed.RunAsync(2, bed.Peer(), cts.Token, runBackfill: true));
                Assert.Equal(0, bed.Scheduler.LiveRequests);
            }

            Assert.Equal(1, bed.Scheduler.MaxLiveRequests);
        }

        [Fact]
        public async Task Given_ASnap2BootstrapOnTheDefaultLoopBackfillThatCompletes_When_RunAsyncReturns_Then_ResultHistoryBackfillIsTheRunningBackfillAndResultStateCompactionIsStarted()
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            bed.MoveTipTo(2);
            using var cts = new CancellationTokenSource(RunTimeout);

            var result = await bed.RunAsync(2, bed.Peer(), cts.Token, runBackfill: true);

            Assert.False(result.HistoryBackfill.IsCompleted, "Result.HistoryBackfill completed while the backfill still blocks");
            Assert.NotSame(Task.CompletedTask, result.StateCompaction);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (bed.Scheduler.LiveRequests == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.Equal(1, bed.Scheduler.LiveRequests);

            cts.Cancel();
            await result.HistoryBackfill.WaitAsync(TimeSpan.FromSeconds(10));
            await result.StateCompaction.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, bed.Scheduler.LiveRequests);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_APhase2RunThatFinishedTheFirstHalf_When_TheRunIsCancelled_Then_TheFinalCheckpointPersistsThatProgressAndTheRestartSkipsIt(bool balHealEnabled)
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            using (var cts = new CancellationTokenSource(RunTimeout))
            {
                peer.OnHeld = () => Task.Run(cts.Cancel);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bed.RunAsync(2, peer, cts.Token, balHealEnabled: balHealEnabled));
            }

            Assert.False(bed.Log.Contains("snap.phase2.checkpoint.final_failed_suppressed"), "the final checkpoint on stop was not persisted");
            var saved = bed.Bundle.Metadata.GetSnapSyncState();
            Assert.Equal(SnapPhase.Phase2Running, saved.Phase);
            Assert.True(saved.Counters.AccountsSynced > 0, "the stopped run persisted zero synced accounts");
            Assert.DoesNotContain(saved.Tasks, t => t.Next.ToHex() == FirstHalfStart);
            await AssertFirstHalfFlatRowsAtBlock2Async(bed);

            var restartPeer = bed.Peer();
            using var restart = new CancellationTokenSource(RunTimeout);
            await bed.RunAsync(2, restartPeer, restart.Token, balHealEnabled: balHealEnabled);

            Assert.NotEmpty(restartPeer.AccountRanges);
            Assert.DoesNotContain(restartPeer.AccountRanges, r => r.Start == FirstHalfStart);
            await AssertFirstHalfFlatRowsAtBlock2Async(bed);
        }

        private static async Task AssertFirstHalfFlatRowsAtBlock2Async(Snap2BootstrapBed bed)
        {
            Assert.Equal((EvmUInt256)Snap2Chain.BalanceAt(2), (await bed.FlatAccountAsync(Snap2Chain.LowA))?.Balance);
            Assert.Equal((EvmUInt256)200, (await bed.FlatAccountAsync(Snap2Chain.LowB))?.Balance);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_TwoPhase2ConsumersOneStillDrainingAHeldRequest_When_TheRunIsCancelled_Then_RunAsyncReturnsOnlyAfterEveryConsumerHasStopped(bool balHealEnabled)
        {
            using var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
            bed.MoveTipTo(2);
            var peer = bed.PeerHoldingSecondHalfAt(2);
            peer.DrainDelay = TimeSpan.FromSeconds(1);
            var released = 0;
            peer.OnReleased = () => Interlocked.Increment(ref released);
            int inFlightWhenRunReturned;
            using (var cts = new CancellationTokenSource(RunTimeout))
            {
                peer.OnHeld = () => Task.Run(cts.Cancel);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => bed.RunAsync(2, peer, cts.Token, balHealEnabled: balHealEnabled, accountConcurrency: 2));
                inFlightWhenRunReturned = peer.InFlight;
            }

            Assert.Equal(1, Volatile.Read(ref released));
            Assert.Equal(0, inFlightWhenRunReturned);
            Assert.False(bed.Log.Contains("snap.phase2.final_checkpoint.undrained"), "a consumer did not stop within the drain bound");
            Assert.False(bed.Log.Contains("snap.phase2.checkpoint.final_failed_suppressed"), "the final checkpoint on stop was not persisted");
            Assert.Contains(bed.Bundle.Metadata.GetSnapSyncState().Tasks, t => ByteUtil.AreEqual(t.Next, Snap2BootstrapBed.SecondHalfStart));
        }

        [Fact]
        public async Task Given_ASnap1HealWithALivePhase1Backfill_When_TheHealIsCancelled_Then_TheBackfillHasStoppedBeforeRunAsyncReturnsAndTheBundleDisposesCleanly()
        {
            var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            bed.Scheduler.DrainDelay = TimeSpan.FromMilliseconds(500);
            int liveWhenRunReturned;
            int maxLive;
            int trieNodeRequests;
            Exception disposeError;
            try
            {
                bed.SaveState(SnapPhase.Phase3Running, 2, Array.Empty<SnapSyncAccountTask>());
                bed.MoveTipTo(2);
                using var cts = new CancellationTokenSource(RunTimeout);
                bed.Scheduler.OnTrieNodes = () =>
                {
                    SpinWait.SpinUntil(() => bed.Scheduler.LiveRequests > 0, RunTimeout);
                    cts.Cancel();
                };

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => bed.RunAsync(2, bed.Peer(), cts.Token, balHealEnabled: false, runBackfill: true));
                liveWhenRunReturned = bed.Scheduler.LiveRequests;
                maxLive = bed.Scheduler.MaxLiveRequests;
                trieNodeRequests = bed.Scheduler.TrieNodeRequests;
            }
            finally
            {
                disposeError = Record.Exception(bed.Dispose);
            }

            Assert.True(trieNodeRequests >= 1, "the heal never ran, so its failure exit is unobserved");
            Assert.True(maxLive >= 1, "the backfill never issued a request, so its shutdown is unobserved");
            Assert.Equal(0, liveWhenRunReturned);
            Assert.Null(disposeError);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Given_APhase2RunWithALivePhase1Backfill_When_TheRunIsCancelled_Then_TheBackfillHasStoppedBeforeRunAsyncReturnsAndTheBundleDisposesCleanly(bool balHealEnabled)
        {
            var bed = await Snap2BootstrapBed.OpenAsync(Snap2Chain.Build(2));
            bed.Scheduler.DrainDelay = TimeSpan.FromMilliseconds(500);
            int liveWhenRunReturned;
            int maxLive;
            Exception disposeError;
            try
            {
                bed.SaveState(SnapPhase.Phase2Running, 2, Snap2BootstrapBed.BothHalvesUnfetched());
                bed.MoveTipTo(2);
                var peer = bed.PeerHoldingSecondHalfAt(2);
                using var cts = new CancellationTokenSource(RunTimeout);
                peer.OnHeld = () => Task.Run(async () =>
                {
                    while (bed.Scheduler.LiveRequests == 0 && !cts.IsCancellationRequested) await Task.Delay(5);
                    cts.Cancel();
                });

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => bed.RunAsync(2, peer, cts.Token, balHealEnabled: balHealEnabled, runBackfill: true));
                liveWhenRunReturned = bed.Scheduler.LiveRequests;
                maxLive = bed.Scheduler.MaxLiveRequests;
            }
            finally
            {
                disposeError = Record.Exception(bed.Dispose);
            }

            Assert.True(maxLive >= 1, "the backfill never issued a request, so its shutdown is unobserved");
            Assert.Equal(0, liveWhenRunReturned);
            Assert.Null(disposeError);
        }
    }
}

using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.MainnetChain.Observability;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests.Observability
{
    public class SnapSyncProgressReporterStageDerivationTests
    {
        private static SnapSyncState State(SnapPhase phase, ulong pivot) =>
            StateWithCounters(phase, pivot, accountsSynced: 0, slotsSynced: 0);

        private static SnapSyncState StateWithCounters(SnapPhase phase, ulong pivot, ulong accountsSynced, ulong slotsSynced) => new()
        {
            SchemaVersion = 1,
            Phase = phase,
            PivotBlockNumber = pivot,
            PivotBlockHash = new byte[32],
            HealTargetRoot = new byte[32],
            Tasks = Array.Empty<SnapSyncAccountTask>(),
            Counters = new SnapSyncCounters
            {
                AccountsSynced = accountsSynced,
                AccountBytes = 0,
                StorageSlotsSynced = slotsSynced,
                StorageBytes = 0,
                BytecodesSynced = 0,
                BytecodeBytes = 0,
                TrieNodesHealed = 0,
                TrieNodeBytesHealed = 0,
                BytecodesHealed = 0,
            },
        };

        private static HeaderSyncState HeaderSegments(params (ulong Head, ulong Tail)[] segments)
        {
            var subs = new List<HeaderSubchain>();
            foreach (var (head, tail) in segments)
                subs.Add(new HeaderSubchain { Head = head, Tail = tail, Next = tail == 0 ? 0 : tail - 1 });
            return new HeaderSyncState { SchemaVersion = 1, Subchains = subs };
        }

        [Fact]
        public void Given_Phase2RunningStampedButPhase1Incomplete_When_DeriveStage_Then_HeadersOrBodies_NotState()
        {
            var stillDescending = HeaderSegments((21_000_000, 20_500_000));
            var headersCase = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase2Running, pivot: 21_000_000),
                headerTip: 21_000_000,
                headerState: stillDescending,
                lastBody: 0,
                phase2Reached: false);

            Assert.Equal(SnapStage.Headers, headersCase.Stage);
            Assert.NotEqual(SnapStage.State, headersCase.Stage);
            Assert.False(headersCase.ShouldArmStateStall);

            var toGenesis = HeaderSegments((21_000_000, 0));
            var bodiesCase = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase2Running, pivot: 21_000_000),
                headerTip: 21_000_000,
                headerState: toGenesis,
                lastBody: 10_000_000,
                phase2Reached: false);

            Assert.Equal(SnapStage.Bodies, bodiesCase.Stage);
            Assert.NotEqual(SnapStage.State, bodiesCase.Stage);
            Assert.False(bodiesCase.ShouldArmStateStall);
        }

        [Fact]
        public void Given_HeadersContiguousToGenesisAndBodiesFilledToPivot_When_DeriveStage_Then_State()
        {
            var toGenesis = HeaderSegments((21_000_000, 0));
            var derivation = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase2Running, pivot: 21_000_000),
                headerTip: 21_000_000,
                headerState: toGenesis,
                lastBody: 21_000_000,
                phase2Reached: false);

            Assert.Equal(SnapStage.State, derivation.Stage);
            Assert.True(derivation.ShouldArmStateStall);
        }

        [Fact]
        public void Given_Phase3Running_When_DeriveStage_Then_Heal()
        {
            var derivation = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase3Running, pivot: 21_000_000),
                headerTip: 21_000_000,
                headerState: HeaderSegments((21_000_000, 0)),
                lastBody: 21_000_000,
                phase2Reached: false);

            Assert.Equal(SnapStage.Heal, derivation.Stage);
            Assert.False(derivation.ShouldArmStateStall);
        }

        [Fact]
        public void Given_Complete_When_DeriveStage_Then_CatchUp()
        {
            var derivation = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Complete, pivot: 21_000_000),
                headerTip: 21_500_000,
                headerState: HeaderSegments((21_500_000, 0)),
                lastBody: 21_000_000,
                phase2Reached: false);

            Assert.Equal(SnapStage.CatchUp, derivation.Stage);
            Assert.False(derivation.ShouldArmStateStall);
        }

        [Fact]
        public void Given_FreshNode_When_DeriveStage_Then_Headers()
        {
            var derivation = SnapSyncProgressReporter.DeriveStage(
                state: null,
                headerTip: 0,
                headerState: HeaderSyncState.Empty,
                lastBody: 0,
                phase2Reached: false);

            Assert.Equal(SnapStage.Headers, derivation.Stage);
            Assert.False(derivation.ShouldArmStateStall);
        }

        [Fact]
        public void Given_StateReached_When_PivotRollsAboveLastBody_Then_StageStaysState_NoFlipFlop()
        {
            var toGenesis = HeaderSegments((21_000_000, 0));

            var first = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase2Running, pivot: 21_000_000),
                headerTip: 21_000_000,
                headerState: toGenesis,
                lastBody: 21_000_000,
                phase2Reached: false);

            Assert.Equal(SnapStage.State, first.Stage);
            Assert.True(first.Phase2Reached);

            var second = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase2Running, pivot: 21_050_000),
                headerTip: 21_000_000,
                headerState: toGenesis,
                lastBody: 21_000_000,
                phase2Reached: first.Phase2Reached);

            Assert.Equal(SnapStage.State, second.Stage);
            Assert.True(second.ShouldArmStateStall);
            Assert.True(second.Phase2Reached);
        }

        [Fact]
        public void Given_PrematurePhase2Running_When_AccountsZeroAndBodiesBehindPivot_Then_DoesNotLatch()
        {
            var stillDescending = HeaderSegments((21_000_000, 20_500_000));
            var derivation = SnapSyncProgressReporter.DeriveStage(
                State(SnapPhase.Phase2Running, pivot: 21_000_000),
                headerTip: 21_000_000,
                headerState: stillDescending,
                lastBody: 0,
                phase2Reached: false);

            Assert.NotEqual(SnapStage.State, derivation.Stage);
            Assert.False(derivation.Phase2Reached);
            Assert.False(derivation.ShouldArmStateStall);
        }

        [Fact]
        public void Given_Phase2ReachedThenAccountsFlatAtZero_When_DeriveStage_Then_StallStillArmable()
        {
            var toGenesis = HeaderSegments((21_000_000, 0));

            var first = SnapSyncProgressReporter.DeriveStage(
                StateWithCounters(SnapPhase.Phase2Running, pivot: 21_000_000, accountsSynced: 1_000, slotsSynced: 0),
                headerTip: 21_000_000,
                headerState: toGenesis,
                lastBody: 20_000_000,
                phase2Reached: false);

            Assert.Equal(SnapStage.State, first.Stage);
            Assert.True(first.Phase2Reached);

            var second = SnapSyncProgressReporter.DeriveStage(
                StateWithCounters(SnapPhase.Phase2Running, pivot: 21_000_000, accountsSynced: 0, slotsSynced: 0),
                headerTip: 21_000_000,
                headerState: toGenesis,
                lastBody: 20_000_000,
                phase2Reached: first.Phase2Reached);

            Assert.Equal(SnapStage.State, second.Stage);
            Assert.True(second.ShouldArmStateStall);
        }
    }

    public class SnapSyncProgressReporterFormatterTests
    {
        [Theory]
        [InlineData(0UL, "0B")]
        [InlineData(999UL, "999B")]
        [InlineData(1024UL, "1KiB")]
        [InlineData(1024UL * 1024, "1MiB")]
        [InlineData(1024UL * 1024 * 1024, "1GiB")]
        public void FmtBytes_RendersAtUnitBoundaries(ulong bytes, string expected)
        {
            Assert.Equal(expected, SnapSyncProgressReporter.FmtBytes(bytes));
        }

        [Fact]
        public void FmtBytes_RendersFractionalMiB()
        {
            var bytes = (ulong)(3.2 * 1024 * 1024);
            Assert.Equal("3.2MiB", SnapSyncProgressReporter.FmtBytes(bytes));
        }

        [Fact]
        public void FmtBytes_RendersWholeGiBWithoutDecimal()
        {
            var bytes = 41UL * 1024 * 1024 * 1024;
            Assert.Equal("41GiB", SnapSyncProgressReporter.FmtBytes(bytes));
        }

        [Fact]
        public void FmtEta_UnknownRendersQuestionMark()
        {
            Assert.Equal("?", SnapSyncProgressReporter.FmtEta(-1));
        }

        [Fact]
        public void FmtEta_MultiHourRendersCompactHoursMinutes()
        {
            var seconds = 5 * 3600 + 48 * 60;
            Assert.Equal("5h48m", SnapSyncProgressReporter.FmtEta(seconds));
        }

        [Fact]
        public void FmtEta_SubHourRendersCompactMinutesSeconds()
        {
            var seconds = 12 * 60 + 3;
            Assert.Equal("12m03s", SnapSyncProgressReporter.FmtEta(seconds));
        }

        [Fact]
        public void HeaderProgress_ColdStart_ReportsZeroDownloadedAndFullLeft()
        {
            var (downloaded, left) = SnapSyncProgressReporter.HeaderProgress(headerTip: 21_000_000, lastHeader: 0);

            Assert.Equal(0UL, downloaded);
            Assert.Equal(21_000_000UL, left);
        }

        [Fact]
        public void HeaderProgress_MidDescent_ReportsDownloadedAndRemaining()
        {
            var (downloaded, left) = SnapSyncProgressReporter.HeaderProgress(headerTip: 21_000_000, lastHeader: 15_000_000);

            Assert.Equal(6_000_000UL, downloaded);
            Assert.Equal(15_000_000UL, left);
        }

        [Fact]
        public void HeaderProgress_FrontierAtTip_ReportsZeroDownloaded()
        {
            var (downloaded, left) = SnapSyncProgressReporter.HeaderProgress(headerTip: 21_000_000, lastHeader: 21_000_000);

            Assert.Equal(0UL, downloaded);
            Assert.Equal(21_000_000UL, left);
        }
    }
}

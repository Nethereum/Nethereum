using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperHealEntryTests
    {
        private static byte[] Fill(byte b) { var a = new byte[32]; for (int i = 0; i < 32; i++) a[i] = b; return a; }

        private static IChainMetadataStore CreateMetadataStore() => new InMemoryChainMetadataStore();

        private static SnapSyncAccountTask Task(byte nextByte) => new()
        {
            Next = Fill(nextByte),
            Last = Fill(0xff),
            StorageCompleted = Array.Empty<byte[]>(),
            SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
        };

        private static SnapSyncState State(SnapPhase phase, SnapSyncAccountTask task) => new()
        {
            SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
            Phase = phase,
            PivotBlockNumber = 1,
            PivotBlockHash = new byte[32],
            HealTargetRoot = new byte[32],
            Tasks = new[] { task },
            Counters = SnapSyncCounters.Zero,
        };

        [Fact]
        public void HealEntry_PreservesPersistedCompletedCursor_NotStaleResume()
        {
            var persistedNow = State(SnapPhase.Phase2Running, Task(0xff));
            var resumeFrom = State(SnapPhase.Phase2Running, Task(0x92));

            var result = SnapBootstrapper.BuildHealEntryState(
                persistedNow, resumeFrom, pivotBlock: 100, pivotHash: new byte[32], healTarget: Fill(0xab));

            Assert.Equal(SnapPhase.Phase3Running, result.Phase);
            Assert.Equal(100UL, result.PivotBlockNumber);
            Assert.Same(persistedNow.Tasks, result.Tasks);
        }

        [Fact]
        public void HealEntry_FallsBackToResume_WhenNoPersistedState()
        {
            var resumeFrom = State(SnapPhase.Phase3Running, Task(0x92));

            var result = SnapBootstrapper.BuildHealEntryState(
                persistedNow: null, resumeFrom, pivotBlock: 100, pivotHash: new byte[32], healTarget: Fill(0xab));

            Assert.Same(resumeFrom.Tasks, result.Tasks);
        }

        [Fact]
        public void Given_ADeferredAccountsBlobIsSaved_When_RetrievedAfterAWipeOfEverythingElse_Then_TheBlobSurvives()
        {
            var metadata = CreateMetadataStore();
            Assert.Null(metadata.GetDeferredHealAccountsBlob());

            var blob = new byte[64];
            for (int i = 0; i < 64; i++) blob[i] = (byte)i;
            metadata.SaveDeferredHealAccountsBlob(blob);

            Assert.Equal(blob, metadata.GetDeferredHealAccountsBlob());

            metadata.ClearDeferredHealAccountsBlob();
            Assert.Null(metadata.GetDeferredHealAccountsBlob());
        }

        [Fact]
        public void Given_FreshExceptionListIsEmptyButAPersistedBlobExists_When_ResolvingDeferredHealAccounts_Then_ThePersistedListIsUsed()
        {
            var accountHash = new byte[32];
            var storageRoot = new byte[32];
            for (int i = 0; i < 32; i++) { accountHash[i] = (byte)i; storageRoot[i] = (byte)(255 - i); }
            var persisted = new List<SnapSyncClient.AccountNeedingHeal> { new(accountHash, storageRoot) };
            var persistedBlob = DeferredHealAccountsCodec.Encode(persisted);

            var resolved = SnapBootstrapper.ResolveDeferredHealAccounts(
                Array.Empty<SnapSyncClient.AccountNeedingHeal>(), persistedBlob);

            Assert.NotNull(resolved);
            Assert.Single(resolved);
            Assert.Equal(accountHash, resolved[0].AccountHash);
            Assert.Equal(storageRoot, resolved[0].ExpectedStorageRoot);
        }

        [Fact]
        public void Given_FreshExceptionListIsNonEmpty_When_ResolvingDeferredHealAccounts_Then_TheFreshListIsPreferredOverAnyPersistedBlob()
        {
            var freshHash = new byte[32];
            var freshRoot = new byte[32];
            for (int i = 0; i < 32; i++) { freshHash[i] = (byte)(i + 1); freshRoot[i] = (byte)(200 - i); }
            var fresh = new List<SnapSyncClient.AccountNeedingHeal> { new(freshHash, freshRoot) };

            var persisted = new List<SnapSyncClient.AccountNeedingHeal> { new(Fill(0xaa), Fill(0xbb)) };
            var persistedBlob = DeferredHealAccountsCodec.Encode(persisted);

            var resolved = SnapBootstrapper.ResolveDeferredHealAccounts(fresh, persistedBlob);

            Assert.NotNull(resolved);
            Assert.Single(resolved);
            Assert.Equal(freshHash, resolved[0].AccountHash);
            Assert.Equal(freshRoot, resolved[0].ExpectedStorageRoot);
        }

        [Fact]
        public void Given_FreshExceptionListIsEmptyAndNoPersistedBlobExists_When_ResolvingDeferredHealAccounts_Then_NullIsReturned()
        {
            var resolved = SnapBootstrapper.ResolveDeferredHealAccounts(
                Array.Empty<SnapSyncClient.AccountNeedingHeal>(), persistedBlob: null);

            Assert.Null(resolved);
        }

        [Fact]
        public void Given_DurableDeferredStorageDebtExistsWithoutBlob_When_CheckingDeferredHealWork_Then_WorkIsRequired()
        {
            var metadata = CreateMetadataStore();
            metadata.UpsertDeferredStorageDebt(new DeferredStorageDebt
            {
                AccountHash = Fill(0x11),
                DiscoveredStorageRoot = Fill(0x22),
                FetchStateRoot = Fill(0x33),
                FetchPivotBlock = 123UL,
                Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                Status = StorageCompleteness.DeferredBigAccount,
            });

            var shouldHeal = SnapBootstrapper.HasDeferredStorageHealWork(
                metadata,
                Array.Empty<SnapSyncClient.AccountNeedingHeal>());

            Assert.True(shouldHeal);
        }

        [Fact]
        public void Given_APersistedBlobHasCorruptedLength_When_ResolvingDeferredHealAccounts_Then_AWarningIsLogged()
        {
            var corruptBlob = new byte[70];
            var logger = new CapturingLogger();

            var resolved = SnapBootstrapper.ResolveDeferredHealAccounts(
                Array.Empty<SnapSyncClient.AccountNeedingHeal>(), corruptBlob, logger);

            Assert.Empty(resolved);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning
                && e.Message.Contains("deferredheal.corrupt", StringComparison.Ordinal));
        }

        [Fact]
        public void Given_APersistedBlobDecodesCleanly_When_ResolvingDeferredHealAccounts_Then_NoWarningIsLogged()
        {
            var accountHash = Fill(0x01);
            var storageRoot = Fill(0x02);
            var persistedBlob = DeferredHealAccountsCodec.Encode(
                new List<SnapSyncClient.AccountNeedingHeal> { new(accountHash, storageRoot) });
            var logger = new CapturingLogger();

            var resolved = SnapBootstrapper.ResolveDeferredHealAccounts(
                Array.Empty<SnapSyncClient.AccountNeedingHeal>(), persistedBlob, logger);

            Assert.Single(resolved);
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (formatter != null) Entries.Add((logLevel, formatter(state, exception)));
            }
        }

        private static Nethereum.Model.BlockHeader Header(int blockNumber, byte stateRootFill)
            => new() { BlockNumber = blockNumber, StateRoot = Fill(stateRootFill) };

        [Fact]
        public async System.Threading.Tasks.Task Given_TheRefresherReturnsAFreshPivot_When_TheHealerPivotRefresherRuns_Then_ItAdoptsIntoTheRollingPivotAndReturnsIt()
        {
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, 0x01), Fill(0x11));
            var freshHeader = Header(200, 0x02);
            var freshHash = Fill(0x22);
            var refresher = SnapBootstrapper.BuildHealerPivotRefresher(
                (forceFresh, ct) => System.Threading.Tasks.Task.FromResult<(Nethereum.Model.BlockHeader, byte[])?>((freshHeader, freshHash)),
                rollingPivot);

            var result = await refresher(false, default);

            Assert.Equal(freshHeader.StateRoot, result!.Value.Root);
            Assert.Equal(200UL, result.Value.Block);
            Assert.Equal(freshHeader.StateRoot, rollingPivot.Current.Header.StateRoot);
            Assert.Equal(freshHash, rollingPivot.Current.Hash);
        }

        [Fact]
        public async System.Threading.Tasks.Task Given_TheRefresherReturnsNull_When_TheHealerPivotRefresherRuns_Then_ItFallsBackToTheCurrentRollingPivotUnchanged()
        {
            var originalHeader = Header(100, 0x01);
            var originalHash = Fill(0x11);
            var rollingPivot = new SnapBootstrapper.RollingPivot(originalHeader, originalHash);
            var refresher = SnapBootstrapper.BuildHealerPivotRefresher(
                (forceFresh, ct) => System.Threading.Tasks.Task.FromResult<(Nethereum.Model.BlockHeader, byte[])?>(null),
                rollingPivot);

            var result = await refresher(true, default);

            Assert.Equal(originalHeader.StateRoot, result!.Value.Root);
            Assert.Equal(100UL, result.Value.Block);
            Assert.Equal(originalHash, rollingPivot.Current.Hash);
        }

        [Fact]
        public async System.Threading.Tasks.Task Given_TheHealerPivotRefresherRuns_When_Called_Then_ItForwardsTheForceFreshFlagToTheCallersRefresher()
        {
            bool? observedForceFresh = null;
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, 0x01), Fill(0x11));
            var refresher = SnapBootstrapper.BuildHealerPivotRefresher(
                (forceFresh, ct) =>
                {
                    observedForceFresh = forceFresh;
                    return System.Threading.Tasks.Task.FromResult<(Nethereum.Model.BlockHeader, byte[])?>(null);
                },
                rollingPivot);

            await refresher(true, default);

            Assert.True(observedForceFresh);
        }

        [Fact]
        public async System.Threading.Tasks.Task Given_AnotherConsumerAlreadyAdoptedAHigherPivot_When_TheHealerPivotRefresherRuns_Then_ItReturnsTheAuthoritativeRollingPivotNotItsOwnLowerLocalRotate()
        {
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, 0x01), Fill(0x11));
            var higherHeader = Header(500, 0x99);
            var higherHash = Fill(0x99);
            rollingPivot.Adopt(new SnapBootstrapper.PivotState(higherHeader, higherHash));

            var lowerRolledHeader = Header(300, 0x02);
            var lowerRolledHash = Fill(0x22);
            var refresher = SnapBootstrapper.BuildHealerPivotRefresher(
                (forceFresh, ct) => System.Threading.Tasks.Task.FromResult<(Nethereum.Model.BlockHeader, byte[])?>(
                    (lowerRolledHeader, lowerRolledHash)),
                rollingPivot);

            var result = await refresher(false, default);

            Assert.Equal(higherHeader.StateRoot, result!.Value.Root);
            Assert.Equal(500UL, result.Value.Block);
            Assert.NotEqual(lowerRolledHeader.StateRoot, result.Value.Root);
        }
    }
}

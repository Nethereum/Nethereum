using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperFinalizeCertifyTests
    {
        private static FlatStateReconcileResult Certified(long slotsAdded) =>
            new FlatStateReconcileResult(7, 11, 1, 2, 3, slotsAdded, 4, 5)
            {
                RangesWalked = 6,
                LeavesWalked = 8,
                TrieNodesMissing = 9,
                TrieNodesDiffering = 10,
            };

        [Fact]
        public async Task Given_AReconcilerThatCertifies_When_FinalizeReconcilesTheFlatState_Then_ItRunsTheReconcilerExactlyOnceAndNoVerifyWalk()
        {
            var reconciler = new RecordingReconciler(() => Certified(516_000_000));

            await SnapBootstrapper.ReconcileAndCertifyFlatAsync(
                reconciler, new byte[32], pivotBlockNumber: 1UL, new CapturingLogger(), CancellationToken.None);

            Assert.Equal(1, reconciler.ReconcileCalls);
            Assert.Equal(0, reconciler.VerifyCalls);
        }

        [Fact]
        public async Task Given_ACertifiedResult_When_FinalizeReconcilesTheFlatState_Then_TheDoneLineCarriesTheRepairAndTrieCounters()
        {
            var reconciler = new RecordingReconciler(() => Certified(13));
            var logger = new CapturingLogger();

            await SnapBootstrapper.ReconcileAndCertifyFlatAsync(
                reconciler, new byte[32], pivotBlockNumber: 1UL, logger, CancellationToken.None);

            var done = Assert.Single(logger.Entries, e => e.Message.Contains("snap.flat.reconcile done", StringComparison.Ordinal)).Message;
            Assert.Contains("accounts=7 slots=11", done);
            Assert.Contains("ghost_accounts=1 ghost_slots=2", done);
            Assert.Contains("added=3/13", done);
            Assert.Contains("patched=4/5", done);
            Assert.Contains("ranges_walked=6 leaves_walked=8", done);
            Assert.EndsWith("trie_missing=9 trie_differ=10", done);
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("snap.flat.verify", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Given_AReconcilerThatReportsUnresolvableNodes_When_FinalizeReconcilesTheFlatState_Then_TheHolesPropagateWithTheirMissingNodesAndNoVerifyRuns()
        {
            var subtree = (AccountHash: Enumerable.Repeat((byte)0xAA, 32).ToArray(), StorageRoot: Enumerable.Repeat((byte)0xBB, 32).ToArray());
            var missing = (Owner: subtree.AccountHash, Path: new byte[] { 0x03, 0x0A });
            var reconciler = new RecordingReconciler(() => throw new FlatReconcileUnresolvableNodeException(
                "holes", new[] { subtree }, new[] { missing }, null));

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(() =>
                SnapBootstrapper.ReconcileAndCertifyFlatAsync(
                    reconciler, new byte[32], pivotBlockNumber: 1UL, new CapturingLogger(), CancellationToken.None));

            var node = Assert.Single(thrown.MissingNodes);
            Assert.Equal(missing.Owner, node.Owner);
            Assert.Equal(missing.Path, node.Path);
            Assert.Equal(1, reconciler.ReconcileCalls);
            Assert.Equal(0, reconciler.VerifyCalls);
        }

        private static SnapBootstrapper.RollingPivot EmptyStatePivot()
            => new SnapBootstrapper.RollingPivot(
                new Nethereum.Model.BlockHeader { BlockNumber = 1, StateRoot = Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH },
                Enumerable.Repeat((byte)0x01, 32).ToArray());

        [Fact]
        public async Task Given_ABundleWithAReconciler_When_Finalizing_Then_GenerationRunsAndNoIgnoredFlagWarningIsLogged()
        {
            using var bundle = new ScriptedReconcileBundle(Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0);
            var logger = new CapturingLogger();

            await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler: null, EmptyStatePivot(), pivotRefresher: null, metrics: null,
                logger, CancellationToken.None);

            Assert.Equal(1, bundle.ReconcileCalls);
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("is ignored", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task Given_ABundleWithoutAReconciler_When_Finalized_Then_TheNoReconcilerSkipIsLoggedAndNoPhase3StateIsStamped()
        {
            using var bundle = Nethereum.CoreChain.Storage.InMemory.InMemoryChainStoreBundle.Open();
            var logger = new CapturingLogger();

            await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler: null, EmptyStatePivot(), pivotRefresher: null, metrics: null,
                logger, CancellationToken.None);

            Assert.Contains(logger.Entries, e => e.Message.StartsWith(
                "snap.flat.reconcile SKIPPED: bundle ", StringComparison.Ordinal)
                && e.Message.Contains("does not expose IFlatStateReconciler", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("EnableFlatReconcile", StringComparison.Ordinal));
            Assert.Null(bundle.Metadata.GetSnapSyncState());
        }

        [Fact]
        public async Task Given_ThePivotAdvancesDuringGeneration_When_Finalized_Then_TheCommittedRootIsTheConvergedRoot()
        {
            using var bundle = new ScriptedReconcileBundle(Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0);
            var refreshes = 0;
            var advanced = new Nethereum.Model.BlockHeader { BlockNumber = 900, StateRoot = Enumerable.Repeat((byte)0x77, 32).ToArray() };

            var committed = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler: null, EmptyStatePivot(),
                (_, _) =>
                {
                    Interlocked.Increment(ref refreshes);
                    return Task.FromResult<(Nethereum.Model.BlockHeader, byte[])?>((advanced, Enumerable.Repeat((byte)0x09, 32).ToArray()));
                },
                metrics: null, new CapturingLogger(), CancellationToken.None);

            Assert.Equal(Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH, committed.Header.StateRoot);
            Assert.Equal(1, (long)committed.Header.BlockNumber);
            Assert.Equal(new[] { Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH }, bundle.ReconcileRoots.ToArray());
            Assert.Equal(0, refreshes);
        }

        [Fact]
        public async Task Given_AMatchedStreamWhoseStorageHealEndsAtAnotherRoot_When_Finalized_Then_CertifyRunsAtTheCurrentPivot()
        {
            using var bundle = new ScriptedReconcileBundle(Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0);
            var pivot = EmptyStatePivot();
            pivot.Adopt(new SnapBootstrapper.PivotState(
                new Nethereum.Model.BlockHeader { BlockNumber = 5, StateRoot = Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH },
                Enumerable.Repeat((byte)0x05, 32).ToArray()));

            var committed = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler: null, pivot, pivotRefresher: null, metrics: null,
                new CapturingLogger(), CancellationToken.None);

            Assert.Equal(5, (long)committed.Header.BlockNumber);
            Assert.Equal(Enumerable.Repeat((byte)0x05, 32).ToArray(), committed.Hash);
            Assert.Equal(new[] { Nethereum.Model.DefaultValues.EMPTY_TRIE_HASH }, bundle.ReconcileRoots.ToArray());
        }

        [Fact]
        public async Task Given_APersistedDamageInventory_When_TheFlatCertifierRuns_Then_ItIsDiscardedBeforeTheGateAndTheRunProceeds()
        {
            var damage = new[] { (AccountHash: Enumerable.Repeat((byte)0xAA, 32).ToArray(), StorageRoot: Enumerable.Repeat((byte)0xBB, 32).ToArray()) };
            using var bundle = new ScriptedReconcileBundle(Array.Empty<(byte[], byte[])>(), unresolvableCalls: 0, persistedDamage: damage);
            Assert.Single(bundle.GetPersistedDamage());

            var committed = await SnapBootstrapper.ReconcileFlatStateAsync(
                bundle, scheduler: null, EmptyStatePivot(), pivotRefresher: null, metrics: null,
                new CapturingLogger(), CancellationToken.None);

            Assert.Equal(1, bundle.ClearPersistedDamageCalls);
            Assert.Empty(bundle.GetPersistedDamage());
            Assert.Equal(1, bundle.ReconcileCalls);
            Assert.Equal(1, (long)committed.Header.BlockNumber);
        }

        private sealed class RecordingReconciler : IFlatStateReconciler
        {
            private readonly Func<FlatStateReconcileResult> _reconcile;
            public int ReconcileCalls;
            public int VerifyCalls;

            public RecordingReconciler(Func<FlatStateReconcileResult> reconcile) => _reconcile = reconcile;

            public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct)
            {
                ReconcileCalls++;
                return Task.FromResult(_reconcile());
            }

            public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            {
                VerifyCalls++;
                return Task.FromResult(new FlatStateReconcileResult(0, 0, 0, 0, 0, 0, 0, 0));
            }

            public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage() => Array.Empty<(byte[], byte[])>();
            public void ClearPersistedDamage() { }
            public IReadOnlyList<byte[]> GetPersistedMissingCode() => Array.Empty<byte[]>();
            public void ClearPersistedMissingCode() { }
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
    }
}

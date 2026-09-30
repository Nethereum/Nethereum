using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
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
    public class SnapBootstrapperFinalizeVerifyTests
    {
        private static FlatStateReconcileResult Repairs(long slotsAdded) =>
            new FlatStateReconcileResult(0, 0, 0, 0, 0, slotsAdded, 0, 0);

        [Fact]
        public async Task Verify_true_and_clean_runs_reconcile_then_verify_without_throwing()
        {
            var fake = new RecordingReconciler(reconcile: Repairs(516_000_000), verify: Repairs(0));

            await SnapBootstrapper.ReconcileAndCertifyFlatAsync(
                fake, new byte[32], pivotBlockNumber: 1UL, verify: true, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(1, fake.ReconcileCalls);
            Assert.Equal(1, fake.VerifyCalls);
        }

        [Fact]
        public async Task Verify_true_and_surviving_diff_throws_and_refuses_finalize()
        {
            var fake = new RecordingReconciler(reconcile: Repairs(10), verify: Repairs(3));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SnapBootstrapper.ReconcileAndCertifyFlatAsync(
                    fake, new byte[32], pivotBlockNumber: 1UL, verify: true, NullLogger.Instance, CancellationToken.None));

            Assert.Contains("verify FAILED", ex.Message);
            Assert.Equal(1, fake.VerifyCalls);
        }

        [Fact]
        public async Task Verify_false_skips_the_verify_walk_entirely()
        {
            var fake = new RecordingReconciler(reconcile: Repairs(516_000_000), verify: Repairs(999));

            await SnapBootstrapper.ReconcileAndCertifyFlatAsync(
                fake, new byte[32], pivotBlockNumber: 1UL, verify: false, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(1, fake.ReconcileCalls);
            Assert.Equal(0, fake.VerifyCalls);
        }

        private sealed class RecordingReconciler : IFlatStateReconciler
        {
            private readonly FlatStateReconcileResult _reconcile;
            private readonly FlatStateReconcileResult _verify;
            public int ReconcileCalls;
            public int VerifyCalls;

            public RecordingReconciler(FlatStateReconcileResult reconcile, FlatStateReconcileResult verify)
            {
                _reconcile = reconcile;
                _verify = verify;
            }

            public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct)
            {
                ReconcileCalls++;
                return Task.FromResult(_reconcile);
            }

            public Task<FlatStateReconcileResult> VerifyFlatStateAsync(byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            {
                VerifyCalls++;
                return Task.FromResult(_verify);
            }

            public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage() => Array.Empty<(byte[], byte[])>();
            public void ClearPersistedDamage() { }
            public IReadOnlyList<byte[]> GetPersistedMissingCode() => Array.Empty<byte[]>();
            public void ClearPersistedMissingCode() { }
        }
    }
}

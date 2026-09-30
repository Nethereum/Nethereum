using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeMaintenanceRunnerTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"maintrunner_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private RocksDbChainStoreBundle OpenRocksDb() => RocksDbChainStoreBundle.Open(_dir);

        [Fact]
        public void Given_NoMaintenanceFlagSet_When_AnyRequestedIsChecked_Then_ItIsFalse()
        {
            Assert.False(ChainNodeMaintenanceRunner.AnyRequested(new ChainNodeMaintenanceConfig()));
        }

        [Theory]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        public void Given_AnyMaintenanceFlagSet_When_AnyRequestedIsChecked_Then_ItIsTrue(
            bool wipe, bool compact, bool rebuild, bool verify)
        {
            Assert.True(ChainNodeMaintenanceRunner.AnyRequested(new ChainNodeMaintenanceConfig
            {
                WipeState = wipe,
                CompactAll = compact,
                RebuildStateFromFlat = rebuild,
                VerifyFlat = verify,
            }));
        }

        [Fact]
        public async Task Given_WipeStateOnRocksDb_When_RunRequestedOpsRuns_Then_DeferredHealStateIsCleared()
        {
            using var bundle = OpenRocksDb();
            bundle.Metadata.SaveDeferredHealAccountsBlob(new byte[] { 1, 2, 3 });
            Assert.NotNull(bundle.Metadata.GetDeferredHealAccountsBlob());

            await ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { WipeState = true }, NullLogger.Instance, CancellationToken.None);

            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
        }

        [Fact]
        public async Task Given_WipeStateOnInMemory_When_RunRequestedOpsRuns_Then_DeferredHealStateIsCleared()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveDeferredHealAccountsBlob(new byte[] { 9, 9 });
            Assert.NotNull(bundle.Metadata.GetDeferredHealAccountsBlob());

            await ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { WipeState = true }, NullLogger.Instance, CancellationToken.None);

            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
        }

        [Fact]
        public async Task Given_NoMaintenanceFlagSet_When_RunRequestedOpsRuns_Then_ItIsANoOpAndDeferredHealStateSurvives()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveDeferredHealAccountsBlob(new byte[] { 7 });

            await ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig(), NullLogger.Instance, CancellationToken.None);

            Assert.NotNull(bundle.Metadata.GetDeferredHealAccountsBlob());
        }

        [Fact]
        public async Task Given_CompactAllOnRocksDb_When_RunRequestedOpsRuns_Then_ItCompletesWithoutError()
        {
            using var bundle = OpenRocksDb();

            var exception = await Record.ExceptionAsync(() => ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { CompactAll = true }, NullLogger.Instance, CancellationToken.None));

            Assert.Null(exception);
        }

        [Fact]
        public async Task Given_CompactAllOnInMemory_When_RunRequestedOpsRuns_Then_ItThrowsAnExplicitRefusalRatherThanSucceedingSilently()
        {
            var bundle = InMemoryChainStoreBundle.Open();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { CompactAll = true }, NullLogger.Instance, CancellationToken.None));

            Assert.Contains("IStateCompaction", exception.Message);
        }

        [Fact]
        public async Task Given_RebuildStateFromFlatOnInMemory_When_RunRequestedOpsRuns_Then_ItThrowsAnExplicitRefusalRatherThanSucceedingSilently()
        {
            var bundle = InMemoryChainStoreBundle.Open();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { RebuildStateFromFlat = true }, NullLogger.Instance, CancellationToken.None));

            Assert.Contains("IFlatStateReconciler", exception.Message);
        }

        [Fact]
        public async Task Given_VerifyFlatOnRocksDbWithNoCommittedState_When_RunRequestedOpsRuns_Then_ItCompletesCleanlyAsAGoodStore()
        {
            using var bundle = OpenRocksDb();
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());

            var exception = await Record.ExceptionAsync(() => ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { VerifyFlat = true }, NullLogger.Instance, CancellationToken.None));

            Assert.Null(exception);
        }

        [Fact]
        public async Task Given_VerifyFlatOnInMemoryEvenWithNoCommittedState_When_RunRequestedOpsRuns_Then_ItStillThrowsAnExplicitRefusal()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ChainNodeMaintenanceRunner.RunRequestedOpsAsync(
                bundle, new ChainNodeMaintenanceConfig { VerifyFlat = true }, NullLogger.Instance, CancellationToken.None));

            Assert.Contains("IFlatStateReconciler", exception.Message);
        }

        [Fact]
        public async Task Given_ARequestedOpThatSucceeds_When_RunAndReportExitCodeAsyncRuns_Then_ItReturnsZero()
        {
            var bundle = InMemoryChainStoreBundle.Open();

            var exitCode = await ChainNodeMaintenanceRunner.RunAndReportExitCodeAsync(
                bundle, new ChainNodeMaintenanceConfig { WipeState = true }, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task Given_ARequestedOpThatFails_When_RunAndReportExitCodeAsyncRuns_Then_ItReturnsNonZeroRatherThanThrowing()
        {
            var bundle = InMemoryChainStoreBundle.Open();

            var exitCode = await ChainNodeMaintenanceRunner.RunAndReportExitCodeAsync(
                bundle, new ChainNodeMaintenanceConfig { CompactAll = true }, NullLogger.Instance, CancellationToken.None);

            Assert.NotEqual(0, exitCode);
        }
    }
}

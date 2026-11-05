using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    [Collection("Sequential")]
    public class MainnetMaintenanceTests : IDisposable
    {
        private const string Address = "0x6666666666666666666666666666666666666666";
        private const ulong ArchivedBlock = 42;

        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"mainnet_maint_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true); } catch { }
        }

        [Fact]
        public async Task Given_WipeStateOnAPopulatedMainnetStore_When_MainnetMaintenanceRuns_Then_ItReturnsZeroWipesStateAndSnapMetadataAndKeepsTheArchiveAndCursors()
        {
            var config = new MainnetChainServerConfig { DataDir = _dataDir, WipeState = true };
            var blockHash = Enumerable.Repeat((byte)0x42, 32).ToArray();
            var txHash = Enumerable.Repeat((byte)0x43, 32).ToArray();
            await using (var storage = ChainNodeStorage.Open(config.ToChainNodeConfig()))
                await SeedAsync(storage.Bundle, blockHash, txHash);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(config, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(0, exitCode);
            await using var reopened = ChainNodeStorage.Open(config.ToChainNodeConfig());
            var bundle = reopened.Bundle;
            Assert.Null(await bundle.State.GetAccountAsync(Address));
            var snap = bundle.Metadata.GetSnapSyncState();
            Assert.True(snap is null || snap.Phase == SnapPhase.NotStarted);
            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
            Assert.Equal(ArchivedBlock, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(ArchivedBlock, bundle.Metadata.GetLastFetchedBody());
            Assert.NotNull(await bundle.Blocks.GetByHashAsync(blockHash));
            Assert.NotNull(await bundle.Receipts.GetByTxHashAsync(txHash));
        }

        [Fact]
        public async Task Given_NoMaintenanceFlagOnAPopulatedMainnetStore_When_MainnetMaintenanceRuns_Then_ItReturnsZeroAndLeavesStateUntouched()
        {
            var config = new MainnetChainServerConfig { DataDir = _dataDir };
            var blockHash = Enumerable.Repeat((byte)0x42, 32).ToArray();
            var txHash = Enumerable.Repeat((byte)0x43, 32).ToArray();
            await using (var storage = ChainNodeStorage.Open(config.ToChainNodeConfig()))
                await SeedAsync(storage.Bundle, blockHash, txHash);

            var exitCode = await MainnetNodeComposition.RunMaintenanceAsync(config, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(0, exitCode);
            await using var reopened = ChainNodeStorage.Open(config.ToChainNodeConfig());
            Assert.NotNull(await reopened.Bundle.State.GetAccountAsync(Address));
            Assert.NotNull(reopened.Bundle.Metadata.GetDeferredHealAccountsBlob());
        }

        [Theory]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        public async Task Given_AMaintenanceFlag_When_TheMainnetNodeIsComposedForSync_Then_CompositionRefusesInsteadOfIgnoringTheFlag(
            bool wipe, bool compact, bool rebuild, bool verify)
        {
            var config = new MainnetChainServerConfig
            {
                DataDir = _dataDir,
                ListenPort = -1,
                DisableDiscv4 = true,
                DisableDiscv5 = true,
                TargetPeers = 1,
                WipeState = wipe,
                CompactAll = compact,
                RebuildStateFromFlat = rebuild,
                VerifyFlat = verify,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new ServiceCollection().AddMainnetNodeAsync(config, NullLoggerFactory.Instance));

            Assert.Contains(nameof(MainnetNodeComposition.RunMaintenanceAsync), exception.Message);
            Assert.False(Directory.Exists(_dataDir));
        }

        private static async Task SeedAsync(IChainStoreBundle bundle, byte[] blockHash, byte[] txHash)
        {
            await bundle.State.SaveAccountAsync(Address, new Account { Balance = 777, Nonce = 9 });
            await bundle.Blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = ArchivedBlock,
                ParentHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
            }, blockHash);
            await bundle.Receipts.SaveAsync(
                new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new() },
                txHash, blockHash, ArchivedBlock, txIndex: 0, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 0);
            bundle.Metadata.SetLastFetchedHeader(ArchivedBlock);
            bundle.Metadata.SetLastFetchedBody(ArchivedBlock);
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 999,
                PivotBlockHash = Enumerable.Repeat((byte)0x11, 32).ToArray(),
                HealTargetRoot = new byte[32],
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });
            bundle.Metadata.SaveDeferredHealAccountsBlob(Enumerable.Repeat((byte)0x31, 64).ToArray());
        }
    }
}

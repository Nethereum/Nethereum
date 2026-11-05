using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

#pragma warning disable CS0618

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public class SnapBootstrapStateWipeTests : IDisposable
    {
        private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"snap_wipe_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true); } catch { }
        }

        [Fact]
        public async Task Given_ManyPersistedDeferredStorageDebts_When_TheSnapBootstrapStateIsWiped_Then_TheMetadataColumnFamilyIsLeftWithNoDeleteTombstones()
        {
            using (var seeding = RocksDbChainStoreBundle.Open(_dataDir))
                SeedDeferredStorageDebts(seeding, 2000);
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Equal(0L, MetadataDeleteTombstones(bundle));
        }

        [Fact]
        public void Given_ManyPersistedDeferredStorageDebts_When_TheyAreClearedWithoutAWipe_Then_TheirDeleteTombstonesAreCounted()
        {
            using (var seeding = RocksDbChainStoreBundle.Open(_dataDir))
                SeedDeferredStorageDebts(seeding, 2000);
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            bundle.Metadata.ClearAllDeferredStorageDebts();

            Assert.True(MetadataDeleteTombstones(bundle) >= 2000);
        }

        [Fact]
        public async Task Given_APopulatedStore_When_TheSnapBootstrapStateIsWipedWithProgress_Then_EveryWipedColumnFamilyTheCompactionAndCompletionAreReported()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.State.SaveAccountAsync(
                "0x5555555555555555555555555555555555555555", new Account { Balance = 1, Nonce = 1 });
            SeedDeferredStorageDebts(bundle, 10);
            var progress = new List<string>();

            await bundle.ResetSnapBootstrapStateAsync(progress.Add, CancellationToken.None);

            var wiped = RocksDbManager.StateTrieCfs
                .Concat(RocksDbManager.BinaryTrieCfs)
                .Concat(RocksDbManager.StateHistoryCfs)
                .Concat(RocksDbManager.NodeHistoryCfs);
            foreach (var cf in wiped)
                Assert.Contains(progress, line => line.Contains(cf, StringComparison.Ordinal));
            Assert.Contains(progress, line => line.Contains("compact", StringComparison.OrdinalIgnoreCase)
                                              && line.Contains(RocksDbManager.CF_METADATA, StringComparison.Ordinal));
            Assert.Contains("done", progress.Last(), StringComparison.OrdinalIgnoreCase);
            Assert.Null(await bundle.State.GetAccountAsync("0x5555555555555555555555555555555555555555"));
        }

        private static void SeedDeferredStorageDebts(RocksDbChainStoreBundle bundle, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var accountHash = new byte[32];
                BitConverter.GetBytes(i).CopyTo(accountHash, 0);
                var discoveredStorageRoot = Enumerable.Repeat((byte)0x32, 32).ToArray();
                bundle.Rocks.Put(
                    RocksDbManager.CF_METADATA,
                    RocksDbChainMetadataStore.DeferredStorageDebtKey(accountHash, discoveredStorageRoot),
                    DeferredStorageDebtCodec.Encode(new DeferredStorageDebt
                    {
                        AccountHash = accountHash,
                        DiscoveredStorageRoot = discoveredStorageRoot,
                        FetchStateRoot = Enumerable.Repeat((byte)0x33, 32).ToArray(),
                        Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                        Status = StorageCompleteness.DeferredBigAccount,
                    }));
            }
            Assert.Equal((ulong)count, bundle.Metadata.CountOpenDeferredStorageDebts());
        }

        private static long MetadataDeleteTombstones(RocksDbChainStoreBundle bundle)
        {
            using var lease = bundle.Rocks.Lease();
            var cf = bundle.Rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            var memTable = long.Parse(lease.Database.GetProperty("rocksdb.num-deletes-active-mem-table", cf))
                           + long.Parse(lease.Database.GetProperty("rocksdb.num-deletes-imm-mem-tables", cf));
            var tables = lease.Database.GetProperty("rocksdb.aggregated-table-properties", cf) ?? string.Empty;
            var match = Regex.Match(tables, @"# deletions=(\d+)");
            return memTable + (match.Success ? long.Parse(match.Groups[1].Value) : 0L);
        }
    }
}

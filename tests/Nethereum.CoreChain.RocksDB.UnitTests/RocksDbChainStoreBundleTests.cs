using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Documentation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbChainStoreBundleTests : IDisposable
    {
        private readonly string _dataDir;

        public RocksDbChainStoreBundleTests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"bundle_test_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
            foreach (var sib in new[] { _dataDir + ".restore-new", _dataDir + ".restore-old", _dataDir + ".restore-checkpoint.request" })
            {
                try { if (Directory.Exists(sib)) Directory.Delete(sib, recursive: true); } catch { }
                try { if (File.Exists(sib)) File.Delete(sib); } catch { }
            }
        }

        private static byte[] FillBytes(byte value) => Enumerable.Repeat(value, 32).ToArray();

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "rocksdb-quick-start",
            "Checkpoint the database at a block and find the snapshot on disk", Order = 3)]
        public async Task SaveCheckpointAsync_WritesMetadataRowAndSnapshotDir()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            var cp = await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));

            Assert.Equal(100UL, cp.BlockNumber);
            Assert.Equal(FillBytes(0xAA), cp.StateRoot);
            Assert.Equal(FillBytes(0xBB), cp.BlockHash);

            var metadataRow = bundle.Metadata.GetCheckpoint(100);
            Assert.NotNull(metadataRow);

            var snapshotDir = bundle.ResolveCheckpointSnapshotPath(100);
            Assert.True(Directory.Exists(snapshotDir));
            Assert.EndsWith(Path.Combine(".cp", "000000000100"), snapshotDir);
        }

        [Fact]
        public async Task SaveCheckpointAsync_SnapshotDirAlreadyExists_RefreshesMetadataIdempotently()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            var snapshotDir = bundle.ResolveCheckpointSnapshotPath(200);
            Directory.CreateDirectory(snapshotDir);

            var cp = await bundle.SaveCheckpointAsync(200, FillBytes(0x11), FillBytes(0x22));

            Assert.Equal(200UL, cp.BlockNumber);
            Assert.Equal(FillBytes(0x11), cp.StateRoot);
            var metadataRow = bundle.Metadata.GetCheckpoint(200);
            Assert.NotNull(metadataRow);
            Assert.Equal(FillBytes(0x11), metadataRow.Value.StateRoot);
            Assert.True(Directory.Exists(snapshotDir));
        }

        [Fact]
        public void BackfillPauseSwitch_TracksControlFilePresenceUnderDataDir()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            var pauseControl = (IBackfillPauseControl)bundle;
            var switchFile = Path.Combine(bundle.DataDir, "backfill.paused");

            Assert.False(pauseControl.ShouldPauseBackfill());

            File.WriteAllBytes(switchFile, Array.Empty<byte>());
            Assert.True(pauseControl.ShouldPauseBackfill());
            Assert.Contains("backfill.paused", pauseControl.DescribeBackfillPause());

            File.Delete(switchFile);
            Assert.False(pauseControl.ShouldPauseBackfill());
        }

        [Fact]
        public async Task SaveCheckpointAsync_PairsRowAndSnapshot_AcrossManyCalls()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            for (ulong i = 1; i <= 10; i++)
            {
                await bundle.SaveCheckpointAsync(i * 50, FillBytes((byte)i), FillBytes((byte)(i + 0x80)));
            }

            var listed = await bundle.ListCheckpointsAsync();
            Assert.Equal(10, listed.Count);
            foreach (var cp in listed)
            {
                Assert.True(Directory.Exists(bundle.ResolveCheckpointSnapshotPath(cp.BlockNumber)));
                Assert.NotNull(bundle.Metadata.GetCheckpoint(cp.BlockNumber));
            }
        }

        [Fact]
        public async Task ListCheckpointsAsync_FiltersOrphanedMetadataRows()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));
            await bundle.SaveCheckpointAsync(200, FillBytes(0xCC), FillBytes(0xDD));

            Directory.Delete(bundle.ResolveCheckpointSnapshotPath(100), recursive: true);

            var listed = await bundle.ListCheckpointsAsync();
            Assert.Single(listed);
            Assert.Equal(200UL, listed[0].BlockNumber);
        }

        [Fact]
        public async Task DeleteCheckpointAsync_RemovesBothRowAndSnapshotDir()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));
            var snapshotDir = bundle.ResolveCheckpointSnapshotPath(100);
            Assert.True(Directory.Exists(snapshotDir));

            await bundle.DeleteCheckpointAsync(100);

            Assert.Null(bundle.Metadata.GetCheckpoint(100));
            Assert.False(Directory.Exists(snapshotDir));
        }

        [Fact]
        public async Task RestoreCheckpointAsync_NoSnapshot_Throws()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => bundle.RestoreCheckpointAsync(999));
        }

        [Fact]
        public async Task ResetStateOnlyAsync_ClearsMetadataAndKeepsCheckpointArchive()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.SaveCheckpointAsync(100, FillBytes(0xAA), FillBytes(0xBB));
            bundle.Metadata.MarkGenesisLoaded();
            bundle.Metadata.Commit(50, FillBytes(0x99));

            await bundle.ResetStateOnlyAsync();

            Assert.False(bundle.Metadata.IsGenesisLoaded());
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());
            Assert.Null(bundle.Metadata.GetCheckpoint(100));
            Assert.True(Directory.Exists(bundle.ResolveCheckpointSnapshotPath(100)));
        }

        [Fact]
        public async Task ResetStateOnlyAsync_WipesStateColumnFamilies()
        {
            const string addr = "0x2222222222222222222222222222222222222222";

            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);
            await bundle.State.SaveAccountAsync(addr, new Account { Balance = 500, Nonce = 3 });
            var before = await bundle.State.GetAccountAsync(addr);
            Assert.NotNull(before);
            Assert.Equal(500, before.Balance);

            await bundle.ResetStateOnlyAsync();

            var after = await bundle.State.GetAccountAsync(addr);
            Assert.Null(after);
        }

        [Fact]
        public async Task ResetSnapBootstrapStateAsync_ClearsCommittedHeadForResnapButPreservesCursorsCheckpointsAndGenesisFlag()
        {
            const string addr = "0x4444444444444444444444444444444444444444";

            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            await bundle.State.SaveAccountAsync(addr, new Account { Balance = 777, Nonce = 9 });

            bundle.Metadata.SetLastFetchedHeader(42);
            bundle.Metadata.SetLastFetchedBody(42);
            bundle.Metadata.MarkGenesisLoaded();
            bundle.Metadata.Commit(42, FillBytes(0xEE));

            await bundle.SaveCheckpointAsync(42, FillBytes(0xAA), FillBytes(0xBB));

            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 999,
                PivotBlockHash = FillBytes(0x11),
                HealTargetRoot = new byte[32],
                Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });
            bundle.Metadata.SaveDeferredHealAccountsBlob(new byte[] { 1, 2, 3 });
            bundle.Metadata.UpsertDeferredStorageDebt(new DeferredStorageDebt
            {
                AccountHash = FillBytes(0x31),
                DiscoveredStorageRoot = FillBytes(0x32),
                FetchStateRoot = FillBytes(0x33),
                Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                Status = StorageCompleteness.DeferredBigAccount,
            });
            SeedPersistedMissingCode(bundle, FillBytes(0x34));
            Assert.Single(((IFlatStateReconciler)bundle).GetPersistedMissingCode());

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.Null(await bundle.State.GetAccountAsync(addr));

            Assert.Equal(42UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(42UL, bundle.Metadata.GetLastFetchedBody());
            Assert.True(bundle.Metadata.IsGenesisLoaded());

            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());

            Assert.NotNull(bundle.Metadata.GetCheckpoint(42));

            var snapAfter = bundle.Metadata.GetSnapSyncState();
            Assert.True(snapAfter is null || snapAfter.Phase == SnapPhase.NotStarted);
            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Empty(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Empty(((IFlatStateReconciler)bundle).GetPersistedMissingCode());
        }

        [Fact]
        public async Task WipeColumnFamily_EmptiesAPopulatedColumnFamily_ViaRangeTombstone()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            var addrs = new[]
            {
                "0x1000000000000000000000000000000000000000",
                "0x2000000000000000000000000000000000000000",
                "0x3000000000000000000000000000000000000000",
                "0x4000000000000000000000000000000000000000",
                "0x5000000000000000000000000000000000000000",
            };
            for (int i = 0; i < addrs.Length; i++)
                await bundle.State.SaveAccountAsync(addrs[i], new Account { Balance = 100 + i, Nonce = i });
            foreach (var a in addrs) Assert.NotNull(await bundle.State.GetAccountAsync(a));

            await bundle.ResetSnapBootstrapStateAsync();

            foreach (var a in addrs) Assert.Null(await bundle.State.GetAccountAsync(a));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-store-bundle", "Open the bundle, write an account, and checkpoint the state")]
        public async Task SaveAndRestoreCheckpoint_RoundTrip_PreservesStateAtCheckpoint()
        {
            const string addr = "0x3333333333333333333333333333333333333333";

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));

                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 999, Nonce = 9 });
                var afterMutation = await bundle.State.GetAccountAsync(addr);
                Assert.Equal(999, afterMutation.Balance);
                Assert.Equal(9, afterMutation.Nonce);
            }

            var snapshotDir = Path.Combine(_dataDir, ".cp", "000000000050");
            Assert.True(Directory.Exists(snapshotDir));
            Stores.RocksDbCheckpointManager.RestoreFromCheckpointDir(snapshotDir, _dataDir);

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var restored = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(restored);
                Assert.Equal(100, restored.Balance);
                Assert.Equal(1, restored.Nonce);
            }
        }

        [Fact]
        public async Task SaveCheckpoint_SameHeightDifferentState_RebuildsSnapshot_RestoreYieldsNewState()
        {
            const string addr = "0x5555555555555555555555555555555555555555";

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));

                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 999, Nonce = 9 });
                var cp = await bundle.SaveCheckpointAsync(50, FillBytes(0xCC), FillBytes(0xDD));
                Assert.Equal(FillBytes(0xCC), cp.StateRoot);
                Assert.Equal(FillBytes(0xCC), bundle.Metadata.GetCheckpoint(50).Value.StateRoot);
            }

            var snapshotDir = Path.Combine(_dataDir, ".cp", "000000000050");
            Assert.True(Directory.Exists(snapshotDir));
            Stores.RocksDbCheckpointManager.RestoreFromCheckpointDir(snapshotDir, _dataDir);

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var restored = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(restored);
                Assert.Equal(999, restored.Balance);
                Assert.Equal(9, restored.Nonce);
            }
        }

        [Fact]
        public async Task SaveCheckpoint_SameHeightSameState_ReusesSnapshot_RestoreYieldsOriginalState()
        {
            const string addr = "0x6666666666666666666666666666666666666666";

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));

                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 500, Nonce = 5 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));
            }

            var snapshotDir = Path.Combine(_dataDir, ".cp", "000000000050");
            Assert.True(Directory.Exists(snapshotDir));
            Stores.RocksDbCheckpointManager.RestoreFromCheckpointDir(snapshotDir, _dataDir);

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var restored = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(restored);
                Assert.Equal(100, restored.Balance);
                Assert.Equal(1, restored.Nonce);
            }
        }

        [Fact]
        public async Task InterruptedRestore_MidCommit_CompleteInterruptedRestore_YieldsRestoredState()
        {
            const string addr = "0x7777777777777777777777777777777777777777";
            string snapshotDir;
            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 999, Nonce = 9 });
                snapshotDir = bundle.ResolveCheckpointSnapshotPath(50);
            }

            var newDir = _dataDir + ".restore-new";
            var oldDir = _dataDir + ".restore-old";
            using (var temp = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = snapshotDir }))
                temp.CreateDatabaseCheckpoint(newDir);
            File.WriteAllText(Path.Combine(newDir, ".restore-ready"), "nethereum-checkpoint-restore-v1");
            Directory.Move(_dataDir, oldDir);

            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(_dataDir);

            Assert.True(Directory.Exists(_dataDir));
            Assert.False(Directory.Exists(newDir));
            Assert.False(Directory.Exists(oldDir));
            Assert.True(Directory.Exists(Path.Combine(_dataDir, ".cp")));
            Assert.False(File.Exists(Path.Combine(_dataDir, ".restore-ready")));

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var restored = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(restored);
                Assert.Equal(100, restored.Balance);
                Assert.Equal(1, restored.Nonce);
            }
        }

        [Fact]
        public async Task InterruptedRestore_PrepIncomplete_IsDiscarded_TargetDirUntouched()
        {
            const string addr = "0x8888888888888888888888888888888888888888";
            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 42, Nonce = 2 });

            var newDir = _dataDir + ".restore-new";
            Directory.CreateDirectory(newDir);
            File.WriteAllText(Path.Combine(newDir, "partial.sst"), "incomplete");

            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(_dataDir);

            Assert.False(Directory.Exists(newDir));
            Assert.True(Directory.Exists(_dataDir));

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var acct = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(acct);
                Assert.Equal(42, acct.Balance);
            }
        }

        [Fact]
        public async Task InterruptedRestore_ForeignReadyMarker_IsNotInstalled_TargetDirUntouched()
        {
            const string addr = "0x9999999999999999999999999999999999999999";
            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 314, Nonce = 3 });

            var newDir = _dataDir + ".restore-new";
            Directory.CreateDirectory(newDir);
            File.WriteAllText(Path.Combine(newDir, "some.sst"), "foreign");
            File.WriteAllText(Path.Combine(newDir, ".restore-ready"), "not-our-content");

            Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(_dataDir);

            Assert.False(Directory.Exists(newDir));
            Assert.True(Directory.Exists(_dataDir));
            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var acct = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(acct);
                Assert.Equal(314, acct.Balance);
            }
        }

        [Fact]
        public async Task RestoreFromCheckpointDir_CommitFailure_Propagates_AndBootRecoveryDegrades()
        {
            const string addr = "0xaaaa0000000000000000000000000000aaaa0000";
            string snapshotDir;
            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.State.SaveAccountAsync(addr, new Account { Balance = 100, Nonce = 1 });
                await bundle.SaveCheckpointAsync(50, FillBytes(0xAA), FillBytes(0xBB));
                snapshotDir = bundle.ResolveCheckpointSnapshotPath(50);
            }

            var oldPath = _dataDir + ".restore-old";
            File.WriteAllText(oldPath, "blocker");

            Assert.ThrowsAny<Exception>(
                () => Stores.RocksDbCheckpointManager.RestoreFromCheckpointDir(snapshotDir, _dataDir));
            Assert.True(Directory.Exists(_dataDir));

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                var acct = await bundle.State.GetAccountAsync(addr);
                Assert.NotNull(acct);
                Assert.Equal(100, acct.Balance);
            }
        }

        private static void SeedPersistedMissingCode(RocksDbChainStoreBundle bundle, byte[] codeHash)
        {
            var rocks = bundle.Rocks;
            rocks.Put(RocksDbManager.CF_METADATA, System.Text.Encoding.ASCII.GetBytes("flatrepair:missingcode"), codeHash);
        }
    }
}

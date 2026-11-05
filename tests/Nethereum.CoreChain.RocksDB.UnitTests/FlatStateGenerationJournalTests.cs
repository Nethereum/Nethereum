using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public partial class FlatStateGenerationJournalTests : IDisposable
    {
        private static readonly byte[] JournalRootKey = { 0x01 };
        private readonly List<string> _dirs = new();
        private readonly List<IDisposable> _owned = new();

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            foreach (var dir in _dirs)
                try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private string NewDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"genjournal_{Guid.NewGuid():N}");
            _dirs.Add(dir);
            return dir;
        }

        private RocksDbChainStoreBundle OpenPathKeyedBundle(string dir = null)
        {
            var bundle = RocksDbChainStoreBundle.Open(dir ?? NewDir(), storageOptions: new RocksDbStorageOptions { PathKeyedState = true });
            _owned.Add(bundle);
            return bundle;
        }

        private static byte[] RootOf(string label) => Sha3Keccack.Current.CalculateHash(System.Text.Encoding.ASCII.GetBytes(label));

        private static void SeedJournal(RocksDbChainStoreBundle bundle, byte[] root)
        {
            bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey, root);
            bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02, 0x07 }, Array.Empty<byte>());
            bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x03, 0x00, 0x01, 0x02 }, new byte[] { 0x00 });
            bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x04 }.Concat(RootOf("owner")).ToArray(), Array.Empty<byte>());
        }

        private static int JournalKeyCount(RocksDbChainStoreBundle bundle)
        {
            var count = 0;
            using var it = bundle.Rocks.CreateIterator(RocksDbManager.CF_FLAT_GENERATION);
            for (it.SeekToFirst(); it.Valid(); it.Next()) count++;
            return count;
        }

        private static SnapSyncState StateAt(SnapPhase phase) => new SnapSyncState
        {
            SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
            Phase = phase,
            PivotBlockNumber = 900,
            PivotBlockHash = RootOf("pivot"),
            HealTargetRoot = RootOf("heal"),
            Tasks = Array.Empty<SnapSyncAccountTask>(),
            Counters = SnapSyncCounters.Zero,
        };

        [Fact]
        public void Given_AFreshAndAnExistingStore_When_Opened_Then_TheFlatGenerationColumnFamilyExists()
        {
            var fresh = OpenPathKeyedBundle();
            Assert.True(fresh.Rocks.HasColumnFamily(RocksDbManager.CF_FLAT_GENERATION));

            var dir = NewDir();
            using (var existing = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PathKeyedState = true }))
                existing.Rocks.Database.DropColumnFamily(RocksDbManager.CF_FLAT_GENERATION);
            var reopened = OpenPathKeyedBundle(dir);

            Assert.True(reopened.Rocks.HasColumnFamily(RocksDbManager.CF_FLAT_GENERATION));
            reopened.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey, RootOf("r"));
            Assert.Equal(RootOf("r"), reopened.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey));
            Assert.Contains(RocksDbManager.CF_FLAT_GENERATION, RocksDbManager.StateTrieCfs);
        }

        [Fact]
        public void Given_AGenerationJournal_When_Phase2RunningStateIsSaved_Then_TheJournalIsKept()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));

            bundle.Metadata.SaveSnapSyncState(StateAt(SnapPhase.Phase2Running));

            Assert.Equal(4, JournalKeyCount(bundle));
            Assert.Equal(SnapPhase.Phase2Running, bundle.Metadata.GetSnapSyncState().Phase);
        }

        [Fact]
        public void Given_AGenerationJournal_When_TheReconcilerDiscardsIt_Then_TheJournalIsGone()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));
            bundle.Metadata.SaveSnapSyncState(StateAt(SnapPhase.Phase3Running));

            ((IFlatStateReconciler)bundle).DiscardCertifyJournal();

            Assert.Equal(0, JournalKeyCount(bundle));
            Assert.Equal(SnapPhase.Phase3Running, bundle.Metadata.GetSnapSyncState().Phase);
        }

        [Fact]
        public void Given_AGenerationJournal_When_Phase3RunningStateIsSaved_Then_TheJournalIsKept()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));

            bundle.Metadata.SaveSnapSyncState(StateAt(SnapPhase.Phase3Running));

            Assert.Equal(4, JournalKeyCount(bundle));
        }

        [Fact]
        public async Task Given_AGenerationJournal_When_TheCompleteStateIsCommittedInABatch_Then_TheJournalIsKept()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));
            using var batch = bundle.BeginBatch();
            batch.Commit(900, RootOf("block"));
            batch.SaveSnapSyncState(StateAt(SnapPhase.Complete));
            Assert.Equal(4, JournalKeyCount(bundle));

            await batch.CommitAsync(CancellationToken.None);

            Assert.Equal(4, JournalKeyCount(bundle));
            Assert.Equal(900UL, bundle.Metadata.GetLastBlock());
            ((IFlatStateReconciler)bundle).DiscardCertifyJournal();
            Assert.Equal(0, JournalKeyCount(bundle));
        }

        [Fact]
        public void Given_AGenerationJournal_When_TheSnapStateIsCleared_Then_TheJournalIsGone()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));
            bundle.Metadata.SaveSnapSyncState(StateAt(SnapPhase.Phase3Running));

            bundle.Metadata.ClearSnapSyncState();

            Assert.Equal(0, JournalKeyCount(bundle));
            Assert.Null(bundle.Metadata.GetSnapSyncState());
        }

        private static byte[] LegacyCleanShardKey(byte[] root, int shard)
            => System.Text.Encoding.ASCII.GetBytes("flatrecon:clean").Concat(root).Append((byte)shard).ToArray();

        private static FlatStateReconciler Reconciler(RocksDbChainStoreBundle bundle)
            => new FlatStateReconciler(bundle.Rocks, new RocksDbStateStore(bundle.Rocks), bundle.StateTrieNodes);

        [Fact]
        public void Given_AJournalAtARoot_When_TheCleanShardMarkersOfThatRootAreCleared_Then_TheJournalIsDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));

            Reconciler(bundle).ClearCleanShardMarkers(RootOf("rj"));

            Assert.Equal(0, JournalKeyCount(bundle));
        }

        [Fact]
        public void Given_AJournalAtAnotherRoot_When_TheCleanShardMarkersAreCleared_Then_TheJournalIsKept()
        {
            var bundle = OpenPathKeyedBundle();
            SeedJournal(bundle, RootOf("rj"));

            Reconciler(bundle).ClearCleanShardMarkers(RootOf("other"));

            Assert.Equal(4, JournalKeyCount(bundle));
        }

        [Fact]
        public void Given_LegacyCleanShardMarkersOfTwoRoots_When_OneRootIsCleared_Then_OnlyItsSixteenMarkersAreDeleted()
        {
            var bundle = OpenPathKeyedBundle();
            foreach (var root in new[] { RootOf("a"), RootOf("b") })
                for (int shard = 0; shard < 16; shard++)
                    bundle.Rocks.Put(RocksDbManager.CF_METADATA, LegacyCleanShardKey(root, shard), new byte[] { 1 });

            Reconciler(bundle).ClearCleanShardMarkers(RootOf("a"));

            Assert.All(Enumerable.Range(0, 16), shard => Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_METADATA, LegacyCleanShardKey(RootOf("a"), shard))));
            Assert.All(Enumerable.Range(0, 16), shard => Assert.NotNull(bundle.Rocks.Get(RocksDbManager.CF_METADATA, LegacyCleanShardKey(RootOf("b"), shard))));
        }
    }
}

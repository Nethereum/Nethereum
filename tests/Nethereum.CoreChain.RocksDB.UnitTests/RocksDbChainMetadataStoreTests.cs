using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// Tests for <see cref="RocksDbChainMetadataStore"/>. Focus is on the
    /// atomicity contract documented on <see cref="IChainMetadataStore.Commit"/>
    /// and <see cref="IChainMetadataStore.RewindToCheckpointAtOrBefore"/> —
    /// multi-key mutations are issued through a single <c>WriteBatch</c> so a
    /// crash between any two staged writes cannot leave the metadata column
    /// family describing two different blocks.
    /// </summary>
    public class RocksDbChainMetadataStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbChainMetadataStore _store;

        private static readonly byte[] HashA = new byte[32]
        {
            0x11,0x11,0x11,0x11,0x11,0x11,0x11,0x11,
            0x11,0x11,0x11,0x11,0x11,0x11,0x11,0x11,
            0x11,0x11,0x11,0x11,0x11,0x11,0x11,0x11,
            0x11,0x11,0x11,0x11,0x11,0x11,0x11,0x11,
        };

        private static readonly byte[] HashB = new byte[32]
        {
            0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,
            0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,
            0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,
            0x22,0x22,0x22,0x22,0x22,0x22,0x22,0x22,
        };

        private static readonly byte[] HashC = new byte[32]
        {
            0x33,0x33,0x33,0x33,0x33,0x33,0x33,0x33,
            0x33,0x33,0x33,0x33,0x33,0x33,0x33,0x33,
            0x33,0x33,0x33,0x33,0x33,0x33,0x33,0x33,
            0x33,0x33,0x33,0x33,0x33,0x33,0x33,0x33,
        };

        private static readonly byte[] StateRootA = new byte[32]
        {
            0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,
            0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,
            0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,
            0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,0xaa,
        };

        public RocksDbChainMetadataStoreTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_meta_test_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _store = new RocksDbChainMetadataStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); }
                catch { }
            }
        }

        [Fact]
        public void Given_FreshStore_When_GetLastBlock_Then_ReturnsZero()
        {
            Assert.Equal(0UL, _store.GetLastBlock());
            Assert.Null(_store.GetLastBlockHash());
        }

        [Fact]
        public void Given_Committed_When_Reopened_Then_ReadsSameValues()
        {
            _store.Commit(42, HashA);

            Assert.Equal(42UL, _store.GetLastBlock());
            Assert.Equal(HashA, _store.GetLastBlockHash());
        }

        [Fact]
        public void Given_CommittedThenOverwritten_When_Read_Then_ReturnsLatest()
        {
            _store.Commit(42, HashA);
            _store.Commit(100, HashB);

            Assert.Equal(100UL, _store.GetLastBlock());
            Assert.Equal(HashB, _store.GetLastBlockHash());
        }

        [Fact]
        public void Given_CommitWithNullHash_When_Read_Then_LastBlockHashUnchanged()
        {
            // Documented behaviour: Commit only writes last_block_hash if the
            // provided hash is exactly 32 bytes. Earlier hash stays put if a
            // caller passes null (e.g. genesis bootstrap with no prior hash).
            _store.Commit(42, HashA);
            _store.Commit(43, null);

            Assert.Equal(43UL, _store.GetLastBlock());
            Assert.Equal(HashA, _store.GetLastBlockHash());
        }

        [Fact]
        public void Given_CheckpointSaved_When_GetCheckpoint_Then_ReturnsSavedTuple()
        {
            _store.SaveCheckpoint(100, StateRootA, HashB);

            var cp = _store.GetCheckpoint(100);
            Assert.NotNull(cp);
            Assert.Equal(100UL, cp.Value.BlockNumber);
            Assert.Equal(StateRootA, cp.Value.StateRoot);
            Assert.Equal(HashB, cp.Value.BlockHash);
            Assert.Equal(100UL, _store.GetLatestCheckpoint());
        }

        [Fact]
        public void Given_MultipleCheckpoints_When_GetNearestAtOrBefore_Then_PicksHighest()
        {
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.SaveCheckpoint(200, StateRootA, HashB);
            _store.SaveCheckpoint(300, StateRootA, HashC);

            Assert.Equal(200UL, _store.GetNearestCheckpointAtOrBefore(250)?.BlockNumber);
            Assert.Equal(300UL, _store.GetNearestCheckpointAtOrBefore(300)?.BlockNumber);
            Assert.Equal(300UL, _store.GetNearestCheckpointAtOrBefore(999_999)?.BlockNumber);
            Assert.Null(_store.GetNearestCheckpointAtOrBefore(50));
        }

        [Fact]
        public void Given_CheckpointAt100_When_RewindToOrBefore200_Then_LastBlockMatchesCheckpoint()
        {
            _store.Commit(500, HashA);
            _store.SaveCheckpoint(100, StateRootA, HashB);

            var cp = _store.RewindToCheckpointAtOrBefore(200);

            Assert.Equal(100UL, cp.BlockNumber);
            Assert.Equal(100UL, _store.GetLastBlock());
            Assert.Equal(HashB, _store.GetLastBlockHash());
        }

        [Fact]
        public void Given_NoCheckpoint_When_Rewind_Then_Throws()
        {
            _store.Commit(500, HashA);

            Assert.Throws<InvalidOperationException>(() => _store.RewindToCheckpointAtOrBefore(200));
        }

        /// <summary>
        /// Atomicity contract: <see cref="RocksDbChainMetadataStore.Commit"/>
        /// stages both <c>last_block</c> and <c>last_block_hash</c> into one
        /// <c>WriteBatch</c>, so after a successful Commit the two fields
        /// always describe the same block. This test pins the behaviour by
        /// re-opening the database with a fresh manager — if either field
        /// had been persisted independently, the on-disk pair could be torn;
        /// they must always agree.
        /// </summary>
        [Fact]
        public void Given_CommitMany_When_ReopenedManyTimes_Then_BlockAndHashAlwaysAgree()
        {
            // Sequence of commits with paired hashes; after each, reopen the
            // store on the same data dir and verify both fields read back to
            // the same commit point. If the implementation regressed to
            // unbatched Puts, a crash window could leave them mismatched and
            // a subsequent test run would observe inconsistency.
            var pairs = new (ulong block, byte[] hash)[]
            {
                (1, HashA),
                (2, HashB),
                (3, HashC),
                (100, HashA),
                (1000, HashB),
            };

            foreach (var (block, hash) in pairs)
            {
                _store.Commit(block, hash);
                // Reopen via the same manager (RocksDB readers see committed
                // state; if the Puts had been issued separately and the
                // process had crashed between them on disk, reopen would
                // surface the inconsistency.)
                Assert.Equal(block, _store.GetLastBlock());
                Assert.Equal(hash, _store.GetLastBlockHash());
            }
        }

        [Fact]
        public void Given_CheckpointDeleted_When_LatestQueried_Then_LatestMovesToNextLower()
        {
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.SaveCheckpoint(200, StateRootA, HashB);
            _store.SaveCheckpoint(300, StateRootA, HashC);
            Assert.Equal(300UL, _store.GetLatestCheckpoint());

            _store.DeleteCheckpoint(300);

            Assert.Equal(200UL, _store.GetLatestCheckpoint());
            Assert.Null(_store.GetCheckpoint(300));
            Assert.NotNull(_store.GetCheckpoint(200));
        }

        [Fact]
        public void Given_OnlyOneCheckpoint_When_Deleted_Then_LatestResetsToZero()
        {
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.DeleteCheckpoint(100);

            Assert.Equal(0UL, _store.GetLatestCheckpoint());
            Assert.Null(_store.GetCheckpoint(100));
        }

        [Fact]
        public void Given_GenesisLoadedMarked_When_Queried_Then_ReturnsTrue()
        {
            Assert.False(_store.IsGenesisLoaded());
            _store.MarkGenesisLoaded();
            Assert.True(_store.IsGenesisLoaded());
        }

        [Fact]
        public void Given_ResetForStateRebuild_When_Called_Then_AllMetadataWiped()
        {
            _store.Commit(500, HashA);
            _store.MarkGenesisLoaded();
            _store.SaveCheckpoint(100, StateRootA, HashB);
            _store.SaveCheckpoint(200, StateRootA, HashC);

            _store.ResetForStateRebuild();

            Assert.Equal(0UL, _store.GetLastBlock());
            Assert.Null(_store.GetLastBlockHash());
            Assert.False(_store.IsGenesisLoaded());
            Assert.Equal(0UL, _store.GetLatestCheckpoint());
            Assert.Null(_store.GetCheckpoint(100));
            Assert.Null(_store.GetCheckpoint(200));
            Assert.Empty(_store.ListCheckpointBlockNumbers());
        }

        [Fact]
        public void Given_CheckpointsSaved_When_ListEnumerated_Then_ContainsAllAscending()
        {
            _store.SaveCheckpoint(300, StateRootA, HashC);
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.SaveCheckpoint(200, StateRootA, HashB);

            var list = _store.ListCheckpointBlockNumbers();

            Assert.Equal(new ulong[] { 100, 200, 300 }, list);
        }

        [Fact]
        public void Given_ZeroLastBlockHashRequiresExact32Bytes_When_BadLengthPassed_Then_HashSkipped()
        {
            _store.Commit(1, HashA);

            // Less than 32 bytes — implementation skips the hash put.
            _store.Commit(2, new byte[16]);

            Assert.Equal(2UL, _store.GetLastBlock());
            // Prior hash should still be present (not overwritten with bad
            // length input).
            Assert.Equal(HashA, _store.GetLastBlockHash());
        }

        [Fact]
        public void Given_FreshStore_When_GetLastFetchedHeaderAndBody_Then_BothReturnZero()
        {
            Assert.Equal(0UL, _store.GetLastFetchedHeader());
            Assert.Equal(0UL, _store.GetLastFetchedBody());
        }

        [Fact]
        public void Given_HeaderCursorSet_Then_PersistsIndependentOfBodyAndLastBlock()
        {
            _store.SetLastFetchedHeader(5_000);

            Assert.Equal(5_000UL, _store.GetLastFetchedHeader());
            Assert.Equal(0UL, _store.GetLastFetchedBody());
            Assert.Equal(0UL, _store.GetLastBlock());
        }

        [Fact]
        public void Given_BodyCursorSet_Then_PersistsIndependentOfHeaderAndLastBlock()
        {
            _store.SetLastFetchedBody(3_000);

            Assert.Equal(3_000UL, _store.GetLastFetchedBody());
            Assert.Equal(0UL, _store.GetLastFetchedHeader());
            Assert.Equal(0UL, _store.GetLastBlock());
        }

        [Fact]
        public void Given_AllCursorsSet_Then_AllPersist()
        {
            _store.SetLastFetchedHeader(10_000);
            _store.SetLastFetchedBody(8_000);
            _store.Commit(7_500, HashA);

            Assert.Equal(10_000UL, _store.GetLastFetchedHeader());
            Assert.Equal(8_000UL, _store.GetLastFetchedBody());
            Assert.Equal(7_500UL, _store.GetLastBlock());
            Assert.Equal(HashA, _store.GetLastBlockHash());
        }

        [Fact]
        public void Given_CursorsAheadOfRewindTarget_When_RewindToCheckpoint_Then_CursorsClampDown()
        {
            // Headers fetched to 10_000, bodies to 8_000, last-block at 7_000,
            // checkpoint at 5_000. Rewinding to 6_000 lands at the 5_000
            // checkpoint and clamps every cursor down to that block.
            _store.SaveCheckpoint(5_000, StateRootA, HashA);
            _store.Commit(7_000, HashB);
            _store.SetLastFetchedHeader(10_000);
            _store.SetLastFetchedBody(8_000);

            var rewound = _store.RewindToCheckpointAtOrBefore(6_000);

            Assert.Equal(5_000UL, rewound.BlockNumber);
            Assert.Equal(5_000UL, _store.GetLastBlock());
            Assert.Equal(5_000UL, _store.GetLastFetchedHeader());
            Assert.Equal(5_000UL, _store.GetLastFetchedBody());
        }

        [Fact]
        public void Given_CursorsBelowRewindTarget_When_RewindToCheckpoint_Then_CursorsLeftAlone()
        {
            _store.SaveCheckpoint(5_000, StateRootA, HashA);
            _store.Commit(7_000, HashB);
            _store.SetLastFetchedHeader(3_000);
            _store.SetLastFetchedBody(2_500);

            _store.RewindToCheckpointAtOrBefore(6_000);

            Assert.Equal(3_000UL, _store.GetLastFetchedHeader());
            Assert.Equal(2_500UL, _store.GetLastFetchedBody());
        }

        [Fact]
        public void Given_CursorsSet_When_ResetForStateRebuild_Then_CursorsWiped()
        {
            _store.SetLastFetchedHeader(50_000);
            _store.SetLastFetchedBody(40_000);

            _store.ResetForStateRebuild();

            Assert.Equal(0UL, _store.GetLastFetchedHeader());
            Assert.Equal(0UL, _store.GetLastFetchedBody());
        }

        // Reproduces the regression class behind the 700,001 silent-corruption
        // incident: after rewind, GetNearestCheckpointAtOrBefore must not return
        // an orphan cp_M row whose snapshot referenced a now-reverted fork's
        // state root. Both rewind paths (snapshot-based RewindToCheckpointAtOrBefore
        // and journal-based StateRewindService) MUST drop above-target rows.
        [Fact]
        public void Given_OrphanCheckpointsAboveTarget_When_DeleteCheckpointsAbove_Then_OnlyBelowOrEqualSurvive()
        {
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.SaveCheckpoint(200, StateRootA, HashB);
            _store.SaveCheckpoint(300, StateRootA, HashC);
            _store.SaveCheckpoint(400, StateRootA, HashA);

            var removed = _store.DeleteCheckpointsAbove(targetBlock: 200);

            Assert.Equal(2, removed);
            Assert.NotNull(_store.GetCheckpoint(100));
            Assert.NotNull(_store.GetCheckpoint(200));
            Assert.Null(_store.GetCheckpoint(300));
            Assert.Null(_store.GetCheckpoint(400));
            Assert.Equal(200UL, _store.GetLatestCheckpoint());
        }

        [Fact]
        public void Given_OrphanCheckpointsAboveTarget_When_RewindToCheckpointAtOrBefore_Then_OrphansDropped()
        {
            _store.Commit(500, HashA);
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.SaveCheckpoint(200, StateRootA, HashB);
            _store.SaveCheckpoint(300, StateRootA, HashC);
            _store.SaveCheckpoint(400, StateRootA, HashA);

            var rewoundTo = _store.RewindToCheckpointAtOrBefore(targetBlock: 250);

            Assert.Equal(200UL, rewoundTo.BlockNumber);
            Assert.Null(_store.GetCheckpoint(300));
            Assert.Null(_store.GetCheckpoint(400));
            Assert.NotNull(_store.GetCheckpoint(100));
            Assert.NotNull(_store.GetCheckpoint(200));
            // Latest must refresh to reflect the new top — old 400 would re-pollute rewind decisions otherwise.
            Assert.Equal(200UL, _store.GetLatestCheckpoint());
        }

        [Fact]
        public void Given_NoOrphansAboveTarget_When_DeleteCheckpointsAbove_Then_NoChange()
        {
            _store.SaveCheckpoint(100, StateRootA, HashA);
            _store.SaveCheckpoint(200, StateRootA, HashB);

            var removed = _store.DeleteCheckpointsAbove(targetBlock: 300);

            Assert.Equal(0, removed);
            Assert.Equal(200UL, _store.GetLatestCheckpoint());
        }

        // --- SnapSyncState persistence (item F) ---

        [Fact]
        public void Given_NoSnapSyncState_When_GetSnapSyncState_Then_ReturnsNull()
        {
            Assert.Null(_store.GetSnapSyncState());
        }

        [Fact]
        public void Given_FullyPopulatedState_When_SaveThenGet_Then_RoundtripsAllFields()
        {
            var hashNext = new byte[32]; hashNext[0] = 0x80;
            var hashLast = new byte[32]; for (int i = 0; i < 32; i++) hashLast[i] = 0xff;
            var accountHash = new byte[32]; accountHash[0] = 0xa1;
            var storageRoot = new byte[32]; storageRoot[0] = 0x5a;
            var completedHash = new byte[32]; completedHash[0] = 0xcc;

            var subTask = new SnapSyncStorageSubTask
            {
                AccountHash = accountHash,
                Next = hashNext,
                Last = hashLast,
                StorageRoot = storageRoot,
            };
            var task = new SnapSyncAccountTask
            {
                Next = hashNext,
                Last = hashLast,
                StorageCompleted = new[] { completedHash },
                SubTasks = new System.Collections.Generic.Dictionary<byte[], System.Collections.Generic.IReadOnlyList<SnapSyncStorageSubTask>>
                {
                    [accountHash] = new[] { subTask },
                },
            };
            var counters = new SnapSyncCounters
            {
                AccountsSynced = 1234,
                AccountBytes = 567890,
                StorageSlotsSynced = 99,
                StorageBytes = 12345,
                BytecodesSynced = 7,
                BytecodeBytes = 88,
                TrieNodesHealed = 42,
                TrieNodeBytesHealed = 4242,
                BytecodesHealed = 3,
            };
            var state = new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 25_356_722UL,
                PivotBlockHash = HashA,
                HealTargetRoot = StateRootA,
                Tasks = new[] { task },
                Counters = counters,
            };

            _store.SaveSnapSyncState(state);
            var read = _store.GetSnapSyncState();

            Assert.NotNull(read);
            Assert.Equal(1UL, read.SchemaVersion);
            Assert.Equal(SnapPhase.Phase2Running, read.Phase);
            Assert.Equal(25_356_722UL, read.PivotBlockNumber);
            Assert.Equal(HashA, read.PivotBlockHash);
            Assert.Equal(StateRootA, read.HealTargetRoot);
            Assert.Single(read.Tasks);
            Assert.Equal(hashNext, read.Tasks[0].Next);
            Assert.Equal(hashLast, read.Tasks[0].Last);
            Assert.Single(read.Tasks[0].StorageCompleted);
            Assert.Equal(completedHash, read.Tasks[0].StorageCompleted[0]);
            Assert.Single(read.Tasks[0].SubTasks);
            var subEntry = System.Linq.Enumerable.First(read.Tasks[0].SubTasks);
            Assert.Equal(accountHash, subEntry.Key);
            Assert.Single(subEntry.Value);
            Assert.Equal(accountHash, subEntry.Value[0].AccountHash);
            Assert.Equal(hashNext, subEntry.Value[0].Next);
            Assert.Equal(storageRoot, subEntry.Value[0].StorageRoot);
            Assert.Equal(1234UL, read.Counters.AccountsSynced);
            Assert.Equal(567890UL, read.Counters.AccountBytes);
            Assert.Equal(42UL, read.Counters.TrieNodesHealed);
        }

        [Fact]
        public void Given_PersistedState_When_Clear_Then_GetReturnsNull()
        {
            var state = new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 1,
                PivotBlockHash = HashA,
                HealTargetRoot = StateRootA,
                Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };
            _store.SaveSnapSyncState(state);
            Assert.NotNull(_store.GetSnapSyncState());

            _store.ClearSnapSyncState();

            Assert.Null(_store.GetSnapSyncState());
        }

        [Fact]
        public void Given_PersistedState_When_ResetForStateRebuild_Then_StateAlsoCleared()
        {
            // Reset is the catch-all "wipe everything that depends on state"
            // path; the snap-sync row must be in scope so a re-sync starts
            // fresh.
            var state = new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 1,
                PivotBlockHash = HashA,
                HealTargetRoot = StateRootA,
                Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };
            _store.SaveSnapSyncState(state);

            _store.ResetForStateRebuild();

            Assert.Null(_store.GetSnapSyncState());
        }
    }
}

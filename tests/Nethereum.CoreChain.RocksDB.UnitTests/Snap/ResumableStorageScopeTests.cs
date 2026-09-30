using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Snap
{
    public class ResumableStorageScopeTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbPathTrieNodeStore _store;

        private static readonly byte[] AccountHash = Owner(0x42);

        public ResumableStorageScopeTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_resumable_storage_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath, PathKeyedState = true });
            _store = new RocksDbPathTrieNodeStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
                try { Directory.Delete(_dbPath, true); } catch { }
        }

        private static byte[] Owner(byte marker)
        {
            var o = new byte[32];
            o[0] = marker;
            return o;
        }

        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, List<(byte[] key, byte[] value)> entries) BuildOracle(int count, int seed = 0)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new List<(byte[] key, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD };
                entries.Add((key, value));
                trie.Put(key, value);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.key, b.key));
            return (trie, storage, entries);
        }

        private static (List<byte[]> keys, List<byte[]> values, List<byte[]> proof) Page(
            PatriciaTrie oracle, InMemoryContentNodeStore oracleStorage, List<(byte[] key, byte[] value)> entries, int fromIdx, int toIdxInclusive)
        {
            var slice = entries.Skip(fromIdx).Take(toIdxInclusive - fromIdx + 1).ToList();
            var keys = slice.Select(e => e.key).ToList();
            var values = slice.Select(e => e.value).ToList();
            var proof = PatriciaRangeProofGenerator.GenerateProof(oracle.Root, oracleStorage, keys[0], keys[^1]);
            return (keys, values, proof);
        }

        [Fact]
        public void ValidPage_AdvancesCursorToIncrementOfLastSlot_InvalidPage_LeavesCursorAndStoreUntouched()
        {
            var (oracle, oracleStorage, entries) = BuildOracle(40);
            var scope = ResumableStorageScope.Open(_store, AccountHash);

            var (keys, values, proof) = Page(oracle, oracleStorage, entries, 5, 20);
            var result = scope.ApplyVerifiedPage(oracle.Root.GetHash(), keys[0], keys, values, proof);

            Assert.True(result.Accepted);
            Assert.True(result.HasMore);
            Assert.Equal(SnapHashRanges.IncrementHash(keys[^1]), result.Cursor, ByteArrayComparer.Current);
            Assert.Equal(result.Cursor, scope.Cursor, ByteArrayComparer.Current);

            var (badKeys, badValues, badProof) = Page(oracle, oracleStorage, entries, 21, 30);
            badValues[3] = (byte[])badValues[3].Clone();
            badValues[3][0] ^= 0xff;

            var cursorBefore = scope.Cursor;
            var badResult = scope.ApplyVerifiedPage(oracle.Root.GetHash(), badKeys[0], badKeys, badValues, badProof);

            Assert.False(badResult.Accepted);
            Assert.Equal(cursorBefore, scope.Cursor, ByteArrayComparer.Current);
            Assert.Null(scope.Get(badKeys[3]));
            Assert.Equal(values[0], scope.Get(keys[0]));
        }

        [Fact]
        public void PagesAcrossCheckpointAndReopen_AreDurable_AndReopenedWriterNeedsNoInMemoryWholeTrie()
        {
            const int total = 60;
            var (oracle, oracleStorage, entries) = BuildOracle(total);
            var finalRoot = oracle.Root.GetHash();

            var scope1 = ResumableStorageScope.Open(_store, AccountHash);
            var (keys1, values1, proof1) = Page(oracle, oracleStorage, entries, 0, 29);
            var r1 = scope1.ApplyVerifiedPage(finalRoot, keys1[0], keys1, values1, proof1);
            Assert.True(r1.Accepted);
            scope1.CommitDirtyNodes();

            var scope2 = ResumableStorageScope.Open(_store, AccountHash);
            for (int i = 0; i < keys1.Count; i++)
                Assert.Equal(values1[i], scope2.Get(keys1[i]));

            var (keys2, values2, proof2) = Page(oracle, oracleStorage, entries, 30, 59);
            var r2 = scope2.ApplyVerifiedPage(finalRoot, keys2[0], keys2, values2, proof2);
            Assert.True(r2.Accepted);
            Assert.False(r2.HasMore);
            scope2.CommitDirtyNodes();

            Assert.Equal(finalRoot, scope2.CurrentRootHash, ByteArrayComparer.Current);
        }

        [Fact]
        public void IntermediateFlush_CommitsIncompleteBoundaryNode_LaterPageOverwritesItCorrectly()
        {
            const int total = 30;
            var (oracle, oracleStorage, entries) = BuildOracle(total, seed: 7);
            var finalRoot = oracle.Root.GetHash();

            var scope = ResumableStorageScope.Open(_store, AccountHash, flushIntervalSlots: 4);

            var (keysA, valuesA, proofA) = Page(oracle, oracleStorage, entries, 0, 14);
            var rA = scope.ApplyVerifiedPage(finalRoot, keysA[0], keysA, valuesA, proofA);
            Assert.True(rA.Accepted);
            scope.CommitDirtyNodes();

            var (keysB, valuesB, proofB) = Page(oracle, oracleStorage, entries, 15, 29);
            var rB = scope.ApplyVerifiedPage(finalRoot, keysB[0], keysB, valuesB, proofB);
            Assert.True(rB.Accepted);
            scope.CommitDirtyNodes();

            Assert.Equal(finalRoot, scope.CurrentRootHash, ByteArrayComparer.Current);

            var reread = ResumableStorageScope.Open(_store, AccountHash);
            foreach (var (key, value) in entries)
                Assert.Equal(value, reread.Get(key));
        }

        [Fact]
        public void Restart_ReattachesByRawRootRead_ResumesWithoutRefetchingCompletedKeyspace()
        {
            const int total = 30;
            var (oracle, oracleStorage, entries) = BuildOracle(total, seed: 13);
            var finalRoot = oracle.Root.GetHash();

            var before = ResumableStorageScope.Open(_store, AccountHash);
            var (keysA, valuesA, proofA) = Page(oracle, oracleStorage, entries, 0, 14);
            var rA = before.ApplyVerifiedPage(finalRoot, keysA[0], keysA, valuesA, proofA);
            Assert.True(rA.Accepted);
            before.CommitDirtyNodes();
            var cursorAtRestart = before.Cursor;

            before = null;

            var after = ResumableStorageScope.Open(_store, AccountHash, resumeCursor: cursorAtRestart);
            Assert.Equal(cursorAtRestart, after.Cursor, ByteArrayComparer.Current);

            var (keysB, valuesB, proofB) = Page(oracle, oracleStorage, entries, 15, 29);
            var rB = after.ApplyVerifiedPage(finalRoot, keysB[0], keysB, valuesB, proofB);
            Assert.True(rB.Accepted);
            Assert.False(rB.HasMore);
            after.CommitDirtyNodes();

            Assert.Equal(finalRoot, after.CurrentRootHash, ByteArrayComparer.Current);

            var verify = ResumableStorageScope.Open(_store, AccountHash);
            foreach (var (key, value) in entries)
                Assert.Equal(value, verify.Get(key));
        }

        [Fact]
        public void Open_WithResumeCursor_ButNoPersistedRoot_ThrowsInsteadOfSilentlyStartingFresh()
        {
            var bogusCursor = Owner(0x99);

            Assert.Throws<InvalidOperationException>(() =>
                ResumableStorageScope.Open(_store, AccountHash, resumeCursor: bogusCursor));
        }
    }
}

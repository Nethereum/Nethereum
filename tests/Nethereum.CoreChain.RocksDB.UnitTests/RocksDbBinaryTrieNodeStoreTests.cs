using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbBinaryTrieNodeStoreTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;

        public RocksDbBinaryTrieNodeStoreTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_btns_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, recursive: true); } catch { }
            }
        }

        [Fact]
        public void PutNode_GetReturnsEncoded()
        {
            var store = new RocksDbBinaryTrieNodeStore(_manager);
            var hash = new byte[32]; hash[0] = 0xAA;
            var encoded = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var stem = new byte[31]; stem[0] = 0xBB;

            store.PutNode(hash, encoded, 5, BinaryTrieConstants.NodeTypeStem, stem);

            var retrieved = store.Get(hash);
            Assert.Equal(encoded, retrieved);
        }

        [Fact]
        public void PutNode_GetNodesByDepthRange()
        {
            var store = new RocksDbBinaryTrieNodeStore(_manager);

            var h1 = new byte[32]; h1[0] = 0x01;
            var h2 = new byte[32]; h2[0] = 0x02;
            var h3 = new byte[32]; h3[0] = 0x03;

            store.PutNode(h1, new byte[] { 0x10 }, 3, BinaryTrieConstants.NodeTypeInternal, null);
            store.PutNode(h2, new byte[] { 0x20 }, 7, BinaryTrieConstants.NodeTypeStem, new byte[31]);
            store.PutNode(h3, new byte[] { 0x30 }, 12, BinaryTrieConstants.NodeTypeInternal, null);

            var range = store.GetNodesByDepthRange(5, 10);
            Assert.Single(range);
            Assert.Equal(h2, range[0].Hash);
        }

        [Fact]
        public void RegisterAddressStem_GetStemNodesByAddress()
        {
            var store = new RocksDbBinaryTrieNodeStore(_manager);
            var address = new byte[20]; address[0] = 0xCC;
            var stemHash = new byte[32]; stemHash[0] = 0xDD;
            var stem = new byte[31]; stem[0] = 0xEE;

            store.PutNode(stemHash, new byte[] { 0x42 }, 2, BinaryTrieConstants.NodeTypeStem, stem);
            store.RegisterAddressStem(address, stemHash);

            var result = store.GetStemNodesByAddress(address);
            Assert.Single(result);
            Assert.Equal(stemHash, result[0].Hash);
            Assert.Equal(stem, result[0].Stem);
        }

        [Fact]
        public void DirtyTracking_PutMarksCleanClears()
        {
            var store = new RocksDbBinaryTrieNodeStore(_manager);
            var hash = new byte[32]; hash[0] = 0xFF;

            store.PutNode(hash, new byte[] { 0x01 }, 0, 0, null);
            Assert.Single(store.GetDirtyNodes());

            store.ClearDirtyTracking();
            Assert.Empty(store.GetDirtyNodes());
        }

        [Fact]
        public void Delete_RemovesNodeAndIndexes()
        {
            var store = new RocksDbBinaryTrieNodeStore(_manager);
            var hash = new byte[32]; hash[0] = 0x11;

            store.PutNode(hash, new byte[] { 0x99 }, 4, BinaryTrieConstants.NodeTypeInternal, null);
            Assert.Equal(1, store.NodeCount);

            store.Delete(hash);
            Assert.Null(store.Get(hash));
            Assert.Equal(0, store.NodeCount);
            Assert.Empty(store.GetNodesByDepthRange(0, 100));
        }

        // Both stores receive the same trie nodes via SaveToStorage. Verify they
        // hold the same node count, same depth range, and each node's encoded
        // bytes match. (ExportCheckpoint byte order may differ because InMemory
        // iterates in dictionary order while RocksDB iterates in key-sorted order.)
        [Fact]
        public void SaveToStorage_SameNodeCount_AndImportRoundTrips()
        {
            var hashProvider = new Blake3HashProvider();
            var trie = new BinaryTrie(hashProvider);

            var key1 = new byte[32]; key1[31] = 0x01;
            var key2 = new byte[32]; key2[0] = 0x80; key2[31] = 0x02;
            var val1 = new byte[32]; val1[0] = 0xAA;
            var val2 = new byte[32]; val2[0] = 0xBB;

            trie.Put(key1, val1);
            trie.Put(key2, val2);

            var memStore = new InMemoryBinaryTrieNodeStore();
            trie.SaveToStorage(memStore);

            var dbStore = new RocksDbBinaryTrieNodeStore(_manager);
            trie.SaveToStorage(dbStore);

            Assert.Equal(memStore.NodeCount, dbStore.NodeCount);

            var memNodes = memStore.GetNodesByDepthRange(0, 100);
            var dbNodes = dbStore.GetNodesByDepthRange(0, 100);
            Assert.Equal(memNodes.Count, dbNodes.Count);

            // Verify every node from InMemory is present in RocksDB with same encoded bytes
            foreach (var memNode in memNodes)
            {
                var dbEncoded = dbStore.Get(memNode.Hash);
                Assert.NotNull(dbEncoded);
                Assert.Equal(memNode.Encoded.ToHex(), dbEncoded.ToHex());
            }

            // ImportCheckpoint round-trip within RocksDB
            var checkpoint = dbStore.ExportCheckpoint(100);
            Assert.True(checkpoint.Length > 4);

            var dbPath2 = Path.Combine(Path.GetTempPath(), $"rocksdb_btns2_{Guid.NewGuid():N}");
            using var manager2 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath2 });
            var dbStore2 = new RocksDbBinaryTrieNodeStore(manager2);
            dbStore2.ImportCheckpoint(checkpoint);

            Assert.Equal(dbStore.NodeCount, dbStore2.NodeCount);
            foreach (var memNode in memNodes)
            {
                var reimported = dbStore2.Get(memNode.Hash);
                Assert.NotNull(reimported);
                Assert.Equal(memNode.Encoded.ToHex(), reimported.ToHex());
            }

            try { Directory.Delete(dbPath2, true); } catch { }
        }

        [Fact]
        public void ImportCheckpoint_RoundTrips()
        {
            var hashProvider = new Blake3HashProvider();
            var trie = new BinaryTrie(hashProvider);

            var key = new byte[32]; key[31] = 0x05;
            var val = new byte[32]; val[0] = 0xDE;
            trie.Put(key, val);

            var memStore = new InMemoryBinaryTrieNodeStore();
            trie.SaveToStorage(memStore);
            var checkpoint = memStore.ExportCheckpoint(100);

            var dbStore = new RocksDbBinaryTrieNodeStore(_manager);
            dbStore.ImportCheckpoint(checkpoint);

            var reimported = dbStore.ExportCheckpoint(100);
            Assert.Equal(checkpoint.ToHex(), reimported.ToHex());
        }

        // End-to-end: state root calculator persists trie nodes to RocksDB,
        // root matches jsign, nodes survive reset + recompute.
        [Fact]
        public async Task StateRootCalculator_WithRocksDbTrieStorage_MatchesJsignVector()
        {
            var hashProvider = new Blake3HashProvider();
            var trieStore = new RocksDbBinaryTrieNodeStore(_manager);
            var stateStore = new InMemoryStateStore();

            var address = "0x1000000000000000000000000000000000000000";
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);

            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = 1, Balance = new EvmUInt256(1000), CodeHash = codeHash
            });
            await stateStore.ClearDirtyTrackingAsync();

            var calc = new BinaryIncrementalStateRootCalculator(
                stateStore, hashProvider, trieStore);
            var root = await calc.ComputeFullStateRootAsync();

            Assert.Equal(
                "acc1f843250ebabbc9c2aa5392741656da98ffb3ec5246b9a64f79ef16048a83",
                root.ToHex());

            Assert.True(trieStore.NodeCount > 0);

            calc.Reset();
            var root2 = await calc.ComputeFullStateRootAsync();
            Assert.Equal(root.ToHex(), root2.ToHex());
        }

        // Warm-start: a NEW calculator instance loads from stored root hash
        // (no GetAllAccountsAsync), then handles an incremental balance change.
        // Cross-checked against a fresh full rebuild of the same terminal state.
        [Fact]
        public async Task WarmStart_NewCalculatorResumesFromStoredRoot()
        {
            var hashProvider = new Blake3HashProvider();
            var trieStore = new RocksDbBinaryTrieNodeStore(_manager);
            var stateStore = new InMemoryStateStore();

            var address = "0x1000000000000000000000000000000000000000";
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);

            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = 1, Balance = new EvmUInt256(1000), CodeHash = codeHash
            });
            await stateStore.ClearDirtyTrackingAsync();

            var calc1 = new BinaryIncrementalStateRootCalculator(
                stateStore, hashProvider, trieStore);
            var root1 = await calc1.ComputeStateRootAsync();
            Assert.Equal(
                "acc1f843250ebabbc9c2aa5392741656da98ffb3ec5246b9a64f79ef16048a83",
                root1.ToHex());

            // NEW calculator — simulates restart. Warm-starts from stored root.
            var calc2 = new BinaryIncrementalStateRootCalculator(
                stateStore, hashProvider, trieStore);

            var account = await stateStore.GetAccountAsync(address);
            account.Balance = new EvmUInt256(2000);
            await stateStore.SaveAccountAsync(address, account);

            var root2 = await calc2.ComputeStateRootAsync();
            Assert.NotEqual(root1.ToHex(), root2.ToHex());

            // Fresh full rebuild must match
            var calcFresh = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            var rootFresh = await calcFresh.ComputeFullStateRootAsync();
            Assert.Equal(rootFresh.ToHex(), root2.ToHex());
        }
    }
}

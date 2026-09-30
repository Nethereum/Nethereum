using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BinaryTrieNodeStoreComparisonTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _dbPath;
        private readonly RocksDbManager _rocksManager;

        public BinaryTrieNodeStoreComparisonTests(ITestOutputHelper output)
        {
            _output = output;
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_cmp_{Guid.NewGuid():N}");
            _rocksManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
        }

        public void Dispose()
        {
            _rocksManager?.Dispose();
            if (Directory.Exists(_dbPath))
                try { Directory.Delete(_dbPath, true); } catch { }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        public void AllThreeStores_SameNodeCount_SameEncodedBytes(int accountCount)
        {
            var hashProvider = new Blake3HashProvider();
            var trie = BuildTrie(hashProvider, accountCount);

            var memStore = new InMemoryBinaryTrieNodeStore();
            trie.SaveToStorage(memStore);

            using var sqliteManager = new SqliteStorageManager();
            var sqliteStore = new SqliteBinaryTrieNodeStore(sqliteManager);
            trie.SaveToStorage(sqliteStore);

            var rocksStore = new RocksDbBinaryTrieNodeStore(_rocksManager);
            trie.SaveToStorage(rocksStore);

            Assert.Equal(memStore.NodeCount, sqliteStore.NodeCount);
            Assert.Equal(memStore.NodeCount, rocksStore.NodeCount);

            var memNodes = memStore.GetNodesByDepthRange(0, 100);
            foreach (var node in memNodes)
            {
                Assert.Equal(node.Encoded.ToHex(), sqliteStore.Get(node.Hash).ToHex());
                Assert.Equal(node.Encoded.ToHex(), rocksStore.Get(node.Hash).ToHex());
            }

            var memDepth = memStore.GetNodesByDepthRange(0, 5);
            Assert.Equal(memDepth.Count, sqliteStore.GetNodesByDepthRange(0, 5).Count);
            Assert.Equal(memDepth.Count, rocksStore.GetNodesByDepthRange(0, 5).Count);

            _output.WriteLine($"[{accountCount}] nodes={memStore.NodeCount}, depth(0-5)={memDepth.Count}");
        }

        [Fact]
        public void SQLite_WarmStart_ProducesSameRootAsRocksDb()
        {
            var hashProvider = new Blake3HashProvider();
            var stateStore = new InMemoryStateStore();

            var address = "0x1000000000000000000000000000000000000000";
            stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = 1, Balance = new EvmUInt256(1000),
                CodeHash = Sha3Keccack.Current.CalculateHash(new byte[0])
            }).Wait();
            stateStore.ClearDirtyTrackingAsync().Wait();

            // RocksDB path
            var rocksStore = new RocksDbBinaryTrieNodeStore(_rocksManager);
            var rocksCalc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider, rocksStore);
            var rocksRoot = rocksCalc.ComputeStateRootAsync().Result;

            // SQLite path
            using var sqliteManager = new SqliteStorageManager();
            var sqliteStore = new SqliteBinaryTrieNodeStore(sqliteManager);
            var sqliteCalc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider, sqliteStore);
            stateStore.ClearDirtyTrackingAsync().Wait();
            var sqliteRoot = sqliteCalc.ComputeFullStateRootAsync().Result;

            Assert.Equal(rocksRoot.ToHex(), sqliteRoot.ToHex());

            // SQLite warm-start
            var sqliteCalc2 = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider, sqliteStore);
            var account = stateStore.GetAccountAsync(address).Result;
            account.Balance = new EvmUInt256(2000);
            stateStore.SaveAccountAsync(address, account).Wait();

            var warmRoot = sqliteCalc2.ComputeStateRootAsync().Result;
            Assert.NotEqual(sqliteRoot.ToHex(), warmRoot.ToHex());

            _output.WriteLine($"Initial: {rocksRoot.ToHex(true)}");
            _output.WriteLine($"After warm-start balance change: {warmRoot.ToHex(true)}");
        }

        private static BinaryTrie BuildTrie(Blake3HashProvider hashProvider, int count)
        {
            var trie = new BinaryTrie(hashProvider);
            for (int i = 0; i < count; i++)
            {
                var key = new byte[32];
                key[0] = (byte)(i >> 24);
                key[1] = (byte)(i >> 16);
                key[2] = (byte)(i >> 8);
                key[3] = (byte)i;
                var value = new byte[32];
                value[0] = (byte)(i & 0xFF);
                trie.Put(key, value);
            }
            return trie;
        }
    }
}

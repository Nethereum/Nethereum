using System;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// A-11 round-trip tests for <see cref="RocksDbBinaryTrieStorage"/>.
    /// Verifies the dedicated <c>binary_trie_nodes</c> column family is
    /// created and can store / retrieve / delete keys independently from the
    /// Patricia <c>trie_nodes</c> CF.
    /// </summary>
    public class BinaryTrieStorageTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;

        public BinaryTrieStorageTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_bt_{Guid.NewGuid():N}");
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
        public void BinaryTrie_CF_IsRegistered()
        {
            // Any Put/Get against CF_BINARY_TRIE_NODES should succeed without throwing.
            var cf = _manager.GetColumnFamily(RocksDbManager.CF_BINARY_TRIE_NODES);
            Assert.NotNull(cf);
        }

        [Fact]
        public void PutGet_RoundTrip()
        {
            var storage = new RocksDbBinaryTrieStorage(_manager);
            var key = new byte[] { 0x01, 0x02, 0x03 };
            var value = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            storage.Put(key, value);
            var read = storage.Get(key);

            Assert.Equal(value, read);
        }

        [Fact]
        public void Delete_Removes()
        {
            var storage = new RocksDbBinaryTrieStorage(_manager);
            var key = new byte[] { 0x10 };
            storage.Put(key, new byte[] { 0xFF });

            storage.Delete(key);

            Assert.Null(storage.Get(key));
        }

        [Fact]
        public void BinaryAndPatricia_TrieNodes_AreSeparateCfs()
        {
            // Same key in both CFs must NOT collide — confirms physical separation.
            var key = new byte[] { 0x42 };

            var patricia = new RocksDbTrieNodeStore(_manager);
            var binary = new RocksDbBinaryTrieStorage(_manager);

            var patriciaValue = new byte[] { 0xA1 };
            var binaryValue = new byte[] { 0xB2 };

            patricia.Put(key, patriciaValue);
            binary.Put(key, binaryValue);

            Assert.Equal(patriciaValue, patricia.Get(key));
            Assert.Equal(binaryValue, binary.Get(key));
        }

        [Fact]
        public void NullKey_IsNoOp()
        {
            var storage = new RocksDbBinaryTrieStorage(_manager);
            storage.Put(null, new byte[] { 0x01 });
            Assert.Null(storage.Get(null));
            storage.Delete(null);
        }
    }
}

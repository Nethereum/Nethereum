using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;
using Nethereum.Merkle.Patricia;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BufferedTrieStorageTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public BufferedTrieStorageTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-buftrie-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
        }

        private static byte[] Key(byte b) { var k = new byte[32]; k[0] = b; return k; }

        [Fact]
        public void Buffers_Writes_And_Reads_Own_Writes_Before_Flush()
        {
            var inner = new RocksDbTrieNodeStore(_mgr);
            var buffer = new BufferedTrieStorage(inner, _mgr);

            var k1 = Key(1);
            var k2 = Key(2);
            buffer.Put(k1, new byte[] { 10 });
            buffer.Put(k2, new byte[] { 20 });

            Assert.Equal(2, buffer.PendingCount);
            Assert.Equal(new byte[] { 10 }, buffer.Get(k1));
            Assert.True(buffer.ContainsKey(k1));
            Assert.Null(inner.Get(k1));

            buffer.Flush();

            Assert.Equal(0, buffer.PendingCount);
            Assert.Equal(new byte[] { 10 }, inner.Get(k1));
            Assert.Equal(new byte[] { 20 }, inner.Get(k2));
        }

        [Fact]
        public void Coalesces_Duplicate_Keys_And_Honors_Pending_Delete()
        {
            var inner = new RocksDbTrieNodeStore(_mgr);
            var k = Key(7);
            inner.Put(k, new byte[] { 99 });

            var buffer = new BufferedTrieStorage(inner, _mgr);
            buffer.Put(k, new byte[] { 1 });
            buffer.Put(k, new byte[] { 2 });
            Assert.Equal(1, buffer.PendingCount);
            Assert.Equal(new byte[] { 2 }, buffer.Get(k));

            buffer.Delete(k);
            Assert.Null(buffer.Get(k));
            Assert.False(buffer.ContainsKey(k));

            buffer.Flush();
            Assert.Null(inner.Get(k));
        }

        [Fact]
        public void Trie_Built_Through_Buffer_Reloads_To_Same_Root()
        {
            var inner = new RocksDbTrieNodeStore(_mgr);
            var buffer = new BufferedTrieStorage(inner, _mgr);
            var keccak = new Nethereum.Util.Sha3Keccack();

            var trie = new Nethereum.Merkle.Patricia.PatriciaTrie(buffer);
            for (int i = 0; i < 200; i++)
            {
                var key = keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
                trie.Put(key, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            }
            trie.SaveDirtyNodesToStorage();
            var root = trie.Root.GetHash();

            Assert.Null(inner.Get(root));
            buffer.Flush();

            var reloaded = Nethereum.Merkle.Patricia.PatriciaTrie.LoadFromStorage(root, (Nethereum.Merkle.Patricia.Storage.ITrieNodeStore)inner);
            Assert.Equal(root, reloaded.Root.GetHash());
        }

        [Fact]
        public async Task Calculator_Drains_Buffer_So_Nodes_Land_Durable()
        {
            var inner = new RocksDbTrieNodeStore(_mgr);
            var buffer = new BufferedTrieStorage(inner, _mgr);
            var state = new RocksDbStateStore(_mgr);
            await state.SaveAccountAsync(
                "0x0000000000000000000000000000000000000001",
                new Nethereum.Model.Account { Nonce = 1, Balance = 5 });

            var calc = new Nethereum.CoreChain.IncrementalStateRootCalculator(state, buffer);
            var root = await calc.ComputeStateRootAsync();

            Assert.NotNull(root);
            Assert.Equal(0, buffer.PendingCount);
            Assert.NotNull(inner.Get(root));
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}

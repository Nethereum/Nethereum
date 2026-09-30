using System;
using System.Diagnostics;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Storage;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BinaryTrieNodeStoreLoadTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;

        public BinaryTrieNodeStoreLoadTests(ITestOutputHelper output)
        {
            _output = output;
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_load_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
                try { Directory.Delete(_dbPath, recursive: true); } catch { }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(5000)]
        public void SaveToStorage_Benchmark(int accountCount)
        {
            var hashProvider = new Blake3HashProvider();
            var trie = new BinaryTrie(hashProvider);

            for (int i = 0; i < accountCount; i++)
            {
                var key = new byte[32];
                key[0] = (byte)(i >> 24);
                key[1] = (byte)(i >> 16);
                key[2] = (byte)(i >> 8);
                key[3] = (byte)i;
                key[31] = 0x00;

                var value = new byte[32];
                value[0] = (byte)(i & 0xFF);
                value[1] = (byte)((i >> 8) & 0xFF);
                trie.Put(key, value);
            }

            var root = trie.ComputeRoot();

            // InMemory baseline
            var memStore = new InMemoryBinaryTrieNodeStore();
            var swMem = Stopwatch.StartNew();
            trie.SaveToStorage(memStore);
            swMem.Stop();
            _output.WriteLine($"[{accountCount} accounts] InMemory SaveToStorage: {swMem.ElapsedMilliseconds}ms, nodes: {memStore.NodeCount}");

            // RocksDB
            var dbStore = new RocksDbBinaryTrieNodeStore(_manager);
            var swDb = Stopwatch.StartNew();
            trie.SaveToStorage(dbStore);
            swDb.Stop();
            _output.WriteLine($"[{accountCount} accounts] RocksDB SaveToStorage:  {swDb.ElapsedMilliseconds}ms, nodes: {dbStore.NodeCount}");

            // NodeCount
            var swCount = Stopwatch.StartNew();
            var count = dbStore.NodeCount;
            swCount.Stop();
            _output.WriteLine($"[{accountCount} accounts] RocksDB NodeCount:      {swCount.ElapsedMilliseconds}ms = {count}");

            // GetNodesByDepthRange
            var swDepth = Stopwatch.StartNew();
            var depthNodes = dbStore.GetNodesByDepthRange(0, 5);
            swDepth.Stop();
            _output.WriteLine($"[{accountCount} accounts] RocksDB DepthRange(0-5): {swDepth.ElapsedMilliseconds}ms, found: {depthNodes.Count}");

            // ExportCheckpoint
            var swExport = Stopwatch.StartNew();
            var checkpoint = dbStore.ExportCheckpoint(10);
            swExport.Stop();
            _output.WriteLine($"[{accountCount} accounts] RocksDB ExportCheckpoint(10): {swExport.ElapsedMilliseconds}ms, bytes: {checkpoint.Length}");

            // ImportCheckpoint into fresh store
            var dbPath2 = Path.Combine(Path.GetTempPath(), $"rocksdb_load2_{Guid.NewGuid():N}");
            using var manager2 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath2 });
            var dbStore2 = new RocksDbBinaryTrieNodeStore(manager2);
            var swImport = Stopwatch.StartNew();
            dbStore2.ImportCheckpoint(checkpoint);
            swImport.Stop();
            _output.WriteLine($"[{accountCount} accounts] RocksDB ImportCheckpoint:     {swImport.ElapsedMilliseconds}ms");

            try { Directory.Delete(dbPath2, true); } catch { }

            Assert.Equal(memStore.NodeCount, dbStore.NodeCount);
            _output.WriteLine($"[{accountCount} accounts] RocksDB/InMemory ratio: {(double)swDb.ElapsedMilliseconds / Math.Max(1, swMem.ElapsedMilliseconds):F1}x");
        }
    }
}

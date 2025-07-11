using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Nethereum.Merkle.Sparse;
using Nethereum.Merkle.Sparse.Storage;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public class SparseMerkleTreeStoragePerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public SparseMerkleTreeStoragePerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task InMemoryStorage_Performance_BasicOperations()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var iterations = 10000;

            // Test 1: Sequential writes
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await storage.SetLeafAsync($"key{i}", $"value{i}");
            }
            sw.Stop();
            _output.WriteLine($"Sequential writes: {iterations} items in {sw.ElapsedMilliseconds}ms ({iterations * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Test 2: Random reads
            var random = new Random(42);
            var readKeys = Enumerable.Range(0, 1000).Select(_ => $"key{random.Next(iterations)}").ToList();
            
            sw.Restart();
            foreach (var key in readKeys)
            {
                await storage.GetLeafAsync(key);
            }
            sw.Stop();
            _output.WriteLine($"Random reads: {readKeys.Count} items in {sw.ElapsedMilliseconds}ms ({readKeys.Count * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Test 3: HasLeavesInSubtree performance
            sw.Restart();
            var hasLeavesChecks = 100;
            for (int i = 0; i < hasLeavesChecks; i++)
            {
                await storage.HasLeavesInSubtreeAsync($"key{i}", 4, 32);
            }
            sw.Stop();
            _output.WriteLine($"HasLeavesInSubtree: {hasLeavesChecks} checks in {sw.ElapsedMilliseconds}ms ({hasLeavesChecks * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Test 4: Leaf count
            sw.Restart();
            var count = await storage.GetLeafCountAsync();
            sw.Stop();
            _output.WriteLine($"GetLeafCount: {sw.ElapsedMilliseconds}ms for {count} items");

            // Test 5: Cache operations
            sw.Restart();
            for (int i = 0; i < 1000; i++)
            {
                await storage.SetCachedNodeAsync($"cache{i}", new byte[] { (byte)i });
            }
            sw.Stop();
            _output.WriteLine($"Cache writes: 1000 items in {sw.ElapsedMilliseconds}ms ({1000.0 * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");
        }

        [Fact]
        public async Task DatabaseStorage_Performance_BatchOperations()
        {
            var repository = new InMemorySparseMerkleRepository<string>();
            var storage = new DatabaseSparseMerkleTreeStorage<string>(repository);

            // Test batch writes
            var batchSize = 1000;
            var batches = 10;

            var sw = Stopwatch.StartNew();
            for (int batch = 0; batch < batches; batch++)
            {
                var leaves = new Dictionary<string, string>();
                for (int i = 0; i < batchSize; i++)
                {
                    leaves[$"batch{batch}_key{i}"] = $"value{i}";
                }
                await repository.SetLeavesBatchAsync(leaves);
            }
            sw.Stop();
            _output.WriteLine($"Batch writes: {batches * batchSize} items in {batches} batches of {batchSize} - {sw.ElapsedMilliseconds}ms ({batches * batchSize * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Test batch deletes
            sw.Restart();
            var keysToDelete = Enumerable.Range(0, 1000).Select(i => $"batch0_key{i}").ToList();
            await repository.RemoveLeavesBatchAsync(keysToDelete);
            sw.Stop();
            _output.WriteLine($"Batch deletes: {keysToDelete.Count} items in {sw.ElapsedMilliseconds}ms ({keysToDelete.Count * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Test prefix queries
            sw.Restart();
            var prefixResults = await repository.GetLeafKeysWithPrefixAsync("batch1_");
            var prefixCount = prefixResults.Count();
            sw.Stop();
            _output.WriteLine($"Prefix query: Found {prefixCount} items with prefix 'batch1_' in {sw.ElapsedMilliseconds}ms");

            // Test HasAnyLeafWithKeyPrefix
            sw.Restart();
            var hasPrefix = await repository.HasAnyLeafWithKeyPrefixAsync("batch2_");
            sw.Stop();
            _output.WriteLine($"HasAnyLeafWithKeyPrefix: {sw.ElapsedMilliseconds}ms (result: {hasPrefix})");
        }

        [Fact]
        public async Task Storage_Performance_ConcurrentOperations()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var concurrency = 100;
            var operationsPerTask = 100;

            // Concurrent writes
            var sw = Stopwatch.StartNew();
            var writeTasks = Enumerable.Range(0, concurrency).Select(async taskId =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    await storage.SetLeafAsync($"task{taskId}_key{i}", $"value{i}");
                }
            });
            await Task.WhenAll(writeTasks);
            sw.Stop();
            _output.WriteLine($"Concurrent writes: {concurrency * operationsPerTask} items with {concurrency} concurrent tasks - {sw.ElapsedMilliseconds}ms ({concurrency * operationsPerTask * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Concurrent reads
            sw.Restart();
            var readTasks = Enumerable.Range(0, concurrency).Select(async taskId =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    await storage.GetLeafAsync($"task{taskId % 10}_key{i}");
                }
            });
            await Task.WhenAll(readTasks);
            sw.Stop();
            _output.WriteLine($"Concurrent reads: {concurrency * operationsPerTask} reads with {concurrency} concurrent tasks - {sw.ElapsedMilliseconds}ms ({concurrency * operationsPerTask * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Mixed operations
            sw.Restart();
            var mixedTasks = Enumerable.Range(0, concurrency).Select(async taskId =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    if (i % 3 == 0)
                        await storage.SetLeafAsync($"mixed{taskId}_key{i}", $"value{i}");
                    else if (i % 3 == 1)
                        await storage.GetLeafAsync($"task{taskId % 10}_key{i % 50}");
                    else
                        await storage.RemoveLeafAsync($"task{taskId}_key{i - 1}");
                }
            });
            await Task.WhenAll(mixedTasks);
            sw.Stop();
            _output.WriteLine($"Mixed operations: {concurrency * operationsPerTask} ops with {concurrency} concurrent tasks - {sw.ElapsedMilliseconds}ms ({concurrency * operationsPerTask * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");
        }

        [Fact]
        public async Task Storage_Performance_LargeDataset()
        {
            var storage = new InMemorySparseMerkleTreeStorage<byte[]>();
            var dataSize = 1024; // 1KB per value
            var items = 5000;

            // Generate test data
            var testData = new byte[dataSize];
            new Random(42).NextBytes(testData);

            // Write performance
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < items; i++)
            {
                var data = new byte[dataSize];
                Array.Copy(testData, data, dataSize);
                data[0] = (byte)(i % 256);
                await storage.SetLeafAsync($"large{i:D6}", data);
            }
            sw.Stop();
            var totalMB = items * dataSize / (1024.0 * 1024.0);
            _output.WriteLine($"Large data writes: {items} items of {dataSize} bytes each ({totalMB:F2} MB) in {sw.ElapsedMilliseconds}ms ({totalMB * 1000.0 / sw.ElapsedMilliseconds:F2} MB/sec)");

            // Memory usage approximation
            var leafCount = await storage.GetLeafCountAsync();
            _output.WriteLine($"Storage contains {leafCount} leaves");

            // Read performance
            sw.Restart();
            var random = new Random(42);
            for (int i = 0; i < 1000; i++)
            {
                var key = $"large{random.Next(items):D6}";
                var data = await storage.GetLeafAsync(key);
            }
            sw.Stop();
            _output.WriteLine($"Large data random reads: 1000 reads in {sw.ElapsedMilliseconds}ms ({1000.0 * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec)");

            // Clear performance
            sw.Restart();
            await storage.ClearAsync();
            sw.Stop();
            _output.WriteLine($"Clear all data: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task HasLeavesInSubtree_Performance_Analysis()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            
            // Setup: Create a tree with various depths
            var setupSw = Stopwatch.StartNew();
            var keys = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                var key = Convert.ToString(i, 16).PadLeft(8, '0');
                keys.Add(key);
                await storage.SetLeafAsync(key, $"value{i}");
            }
            setupSw.Stop();
            _output.WriteLine($"Setup: Created tree with 1000 leaves in {setupSw.ElapsedMilliseconds}ms");

            // Test at different tree levels
            var levels = new[] { 0, 4, 8, 16, 24, 32 };
            foreach (var level in levels)
            {
                var sw = Stopwatch.StartNew();
                var checks = 100;
                var found = 0;
                
                for (int i = 0; i < checks; i++)
                {
                    var nodeKey = keys[i % keys.Count];
                    if (await storage.HasLeavesInSubtreeAsync(nodeKey, level, 32))
                        found++;
                }
                sw.Stop();
                _output.WriteLine($"HasLeavesInSubtree at level {level}: {checks} checks in {sw.ElapsedMilliseconds}ms ({checks * 1000.0 / sw.ElapsedMilliseconds:F2} ops/sec), found: {found}");
            }

            // Worst case: checking non-existent subtrees
            var sw2 = Stopwatch.StartNew();
            var nonExistentChecks = 100;
            for (int i = 0; i < nonExistentChecks; i++)
            {
                var nodeKey = "FFFFFFFF"; // Unlikely to have leaves
                await storage.HasLeavesInSubtreeAsync(nodeKey, 16, 32);
            }
            sw2.Stop();
            _output.WriteLine($"HasLeavesInSubtree worst case (non-existent): {nonExistentChecks} checks in {sw2.ElapsedMilliseconds}ms ({nonExistentChecks * 1000.0 / sw2.ElapsedMilliseconds:F2} ops/sec)");
        }
    }
}
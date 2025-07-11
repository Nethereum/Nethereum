using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Merkle.Sparse;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    public class SparseMerkleTreePerformanceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider;

        public SparseMerkleTreePerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _hashProvider = new Sha3KeccackHashProvider();
        }

        [Fact]
        public async Task Performance_SmallDataset_ShouldBeFast()
        {
            // Arrange
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(8, _hashProvider, convertor, storage);
            const int itemCount = 100;

            var stopwatch = Stopwatch.StartNew();

            // Act - Insert items
            var insertStart = stopwatch.ElapsedMilliseconds;
            for (int i = 0; i < itemCount; i++)
            {
                var key = i.ToString("x2");
                var value = $"value_{i}";
                await tree.SetLeafAsync(key, value);
            }
            var insertTime = stopwatch.ElapsedMilliseconds - insertStart;

            // Act - Compute root
            var rootStart = stopwatch.ElapsedMilliseconds;
            var rootHash = await tree.GetRootHashAsync();
            var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

            stopwatch.Stop();

            // Assert and report
            var leafCount = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"Performance Results for {itemCount} items:");
            _output.WriteLine($"  Insert time: {insertTime} ms ({insertTime / (double)itemCount:F2} ms per item)");
            _output.WriteLine($"  Root computation: {rootTime} ms");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Leaf count: {leafCount}");
            _output.WriteLine($"  Root hash: {rootHash}");

            Assert.Equal(itemCount, leafCount);
            Assert.True(insertTime < 1000, $"Insert took too long: {insertTime} ms");
            Assert.True(rootTime < 100, $"Root computation took too long: {rootTime} ms");
        }

        [Fact]
        public async Task Performance_MediumDataset_ShouldScale()
        {
            // Arrange
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);
            const int itemCount = 1000;

            var stopwatch = Stopwatch.StartNew();

            // Act - Insert items with more realistic keys
            var insertStart = stopwatch.ElapsedMilliseconds;
            for (int i = 0; i < itemCount; i++)
            {
                var key = $"{i:x4}"; // 4 hex chars for depth-16 tree
                var value = $"data_block_{i}_{Guid.NewGuid()}";
                await tree.SetLeafAsync(key, value);
            }
            var insertTime = stopwatch.ElapsedMilliseconds - insertStart;

            // Act - Multiple root computations (caching test)
            var rootStart = stopwatch.ElapsedMilliseconds;
            var rootHash1 = await tree.GetRootHashAsync();
            var rootHash2 = await tree.GetRootHashAsync(); // Should be cached
            var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

            // Act - Updates test
            var updateStart = stopwatch.ElapsedMilliseconds;
            await tree.SetLeafAsync("0001", "updated_value");
            var newRootHash = await tree.GetRootHashAsync();
            var updateTime = stopwatch.ElapsedMilliseconds - updateStart;

            stopwatch.Stop();

            // Assert and report
            var leafCount = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"Performance Results for {itemCount} items:");
            _output.WriteLine($"  Insert time: {insertTime} ms ({insertTime / (double)itemCount:F2} ms per item)");
            _output.WriteLine($"  Root computation: {rootTime} ms");
            _output.WriteLine($"  Update + recompute: {updateTime} ms");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Leaf count: {leafCount}");
            _output.WriteLine($"  Original root: {rootHash1}");
            _output.WriteLine($"  Cached root: {rootHash2}");
            _output.WriteLine($"  Updated root: {newRootHash}");

            Assert.Equal(itemCount, leafCount);
            Assert.Equal(rootHash1, rootHash2); // Caching should work
            Assert.NotEqual(rootHash1, newRootHash); // Update should change root
            Assert.True(insertTime < 5000, $"Insert took too long: {insertTime} ms");
            Assert.True(updateTime < 1000, $"Update took too long: {updateTime} ms");
        }

        [Fact]
        public async Task Performance_LargeDataset_ShouldHandleThousands()
        {
            // Arrange
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(20, _hashProvider, convertor, storage);
            const int itemCount = 5000;

            var stopwatch = Stopwatch.StartNew();

            // Act - Insert items in batches
            var insertStart = stopwatch.ElapsedMilliseconds;
            var batchSize = 500;
            for (int batch = 0; batch < itemCount / batchSize; batch++)
            {
                var batchStart = stopwatch.ElapsedMilliseconds;
                
                for (int i = 0; i < batchSize; i++)
                {
                    var index = batch * batchSize + i;
                    var key = $"{index:x5}"; // 5 hex chars for depth-20 tree
                    var value = $"record_{index}_{DateTime.UtcNow.Ticks}";
                    await tree.SetLeafAsync(key, value);
                }
                
                var batchTime = stopwatch.ElapsedMilliseconds - batchStart;
                _output.WriteLine($"Batch {batch + 1}: {batchSize} items in {batchTime} ms ({batchTime / (double)batchSize:F2} ms per item)");
            }
            var insertTime = stopwatch.ElapsedMilliseconds - insertStart;

            // Act - Root computation
            var rootStart = stopwatch.ElapsedMilliseconds;
            var rootHash = await tree.GetRootHashAsync();
            var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

            // Act - Random access test
            var accessStart = stopwatch.ElapsedMilliseconds;
            var random = new Random(42); // Deterministic for testing
            var accessCount = 100;
            for (int i = 0; i < accessCount; i++)
            {
                var randomIndex = random.Next(itemCount);
                var key = $"{randomIndex:x5}";
                var leaf = await tree.GetLeafAsync(key);
                Assert.NotNull(leaf);
            }
            var accessTime = stopwatch.ElapsedMilliseconds - accessStart;

            stopwatch.Stop();

            // Assert and report
            var leafCount = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"Performance Results for {itemCount} items:");
            _output.WriteLine($"  Insert time: {insertTime} ms ({insertTime / (double)itemCount:F2} ms per item)");
            _output.WriteLine($"  Root computation: {rootTime} ms");
            _output.WriteLine($"  Random access ({accessCount} items): {accessTime} ms ({accessTime / (double)accessCount:F2} ms per access)");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Leaf count: {leafCount}");
            _output.WriteLine($"  Memory estimate: ~{EstimateMemoryUsage(leafCount)} MB");

            Assert.Equal(itemCount, leafCount);
            Assert.True(insertTime < 30000, $"Insert took too long: {insertTime} ms"); // 30 seconds max
            Assert.True(rootTime < 5000, $"Root computation took too long: {rootTime} ms"); // 5 seconds max
            Assert.True(accessTime < 1000, $"Random access took too long: {accessTime} ms");
        }

        [Fact]
        public async Task Performance_SparseDistribution_ShouldBeEfficient()
        {
            // Test the "sparse" nature - widely distributed keys should still be efficient
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(32, _hashProvider, convertor, storage);
            const int itemCount = 1000;

            var stopwatch = Stopwatch.StartNew();

            // Act - Insert sparsely distributed items
            var random = new Random(42);
            var insertStart = stopwatch.ElapsedMilliseconds;
            var usedKeys = new HashSet<string>();
            
            for (int i = 0; i < itemCount; i++)
            {
                string key;
                do
                {
                    // Generate random 8-hex-char keys (32 bits) to ensure sparse distribution
                    key = random.Next().ToString("x8");
                } while (usedKeys.Contains(key));
                
                usedKeys.Add(key);
                var value = $"sparse_data_{i}";
                await tree.SetLeafAsync(key, value);
            }
            var insertTime = stopwatch.ElapsedMilliseconds - insertStart;

            // Act - Root computation with sparse data
            var rootStart = stopwatch.ElapsedMilliseconds;
            var rootHash = await tree.GetRootHashAsync();
            var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

            // Act - Test removal efficiency
            var removeStart = stopwatch.ElapsedMilliseconds;
            var keysToRemove = usedKeys.Take(100).ToList();
            foreach (var key in keysToRemove)
            {
                await tree.SetLeafAsync(key, null); // Remove
            }
            var newRootHash = await tree.GetRootHashAsync();
            var removeTime = stopwatch.ElapsedMilliseconds - removeStart;

            stopwatch.Stop();

            // Assert and report
            var finalLeafCount = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"Sparse Distribution Performance for {itemCount} items:");
            _output.WriteLine($"  Insert time: {insertTime} ms ({insertTime / (double)itemCount:F2} ms per item)");
            _output.WriteLine($"  Root computation: {rootTime} ms");
            _output.WriteLine($"  Remove {keysToRemove.Count} items: {removeTime} ms");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Final leaf count: {finalLeafCount}");
            _output.WriteLine($"  Original root: {rootHash}");
            _output.WriteLine($"  Root after removals: {newRootHash}");

            Assert.Equal(itemCount - keysToRemove.Count, finalLeafCount);
            Assert.NotEqual(rootHash, newRootHash);
            Assert.True(insertTime < 15000, $"Sparse insert took too long: {insertTime} ms");
            Assert.True(rootTime < 3000, $"Sparse root computation took too long: {rootTime} ms");
        }

        [Fact]
        public async Task Performance_ConcurrentOperations_ShouldBeThreadSafe()
        {
            // Test concurrent operations (reads should be safe, writes are protected by storage)
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);
            const int itemCount = 1000;

            // Pre-populate the tree
            for (int i = 0; i < itemCount; i++)
            {
                await tree.SetLeafAsync($"{i:x4}", $"initial_value_{i}");
            }

            var stopwatch = Stopwatch.StartNew();

            // Act - Concurrent read operations
            var readTasks = new List<Task<string>>();
            var random = new Random(42);
            
            for (int i = 0; i < 100; i++)
            {
                var randomKey = $"{random.Next(itemCount):x4}";
                readTasks.Add(tree.GetLeafAsync(randomKey));
            }

            // Act - Concurrent root computations
            var rootTasks = new List<Task<string>>();
            for (int i = 0; i < 10; i++)
            {
                rootTasks.Add(tree.GetRootHashAsync());
            }

            // Wait for all operations
            var readResults = await Task.WhenAll(readTasks);
            var rootResults = await Task.WhenAll(rootTasks);

            stopwatch.Stop();

            // Assert and report
            _output.WriteLine($"Concurrent Operations Performance:");
            _output.WriteLine($"  {readTasks.Count} concurrent reads: {stopwatch.ElapsedMilliseconds} ms");
            _output.WriteLine($"  {rootTasks.Count} concurrent root computations completed");
            _output.WriteLine($"  All reads completed: {readResults.All(r => r != null)}");
            _output.WriteLine($"  All roots identical: {rootResults.All(r => r == rootResults[0])}");

            Assert.True(readResults.All(r => r != null));
            Assert.True(rootResults.All(r => r == rootResults[0])); // All roots should be identical
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Concurrent operations took too long");
        }

        [Fact]
        public async Task Performance_CompareDepths_ShouldShowScaling()
        {
            // Compare performance across different tree depths
            var depths = new[] { 8, 12, 16, 20 };
            var itemsPerDepth = 500;

            _output.WriteLine("Depth Comparison Performance:");
            _output.WriteLine("Depth | Items | Insert(ms) | Root(ms) | Avg Insert | Memory(MB)");
            _output.WriteLine("------|-------|-----------|----------|------------|----------");

            foreach (var depth in depths)
            {
                var storage = new InMemorySparseMerkleTreeStorage<string>();
                var convertor = new StringByteArrayConvertor();
                var tree = new SparseMerkleTree<string>(depth, _hashProvider, convertor, storage);

                var stopwatch = Stopwatch.StartNew();

                // Insert items
                var insertStart = stopwatch.ElapsedMilliseconds;
                var hexFormat = $"x{(depth + 3) / 4}"; // Calculate hex digits needed
                
                for (int i = 0; i < itemsPerDepth; i++)
                {
                    var key = i.ToString(hexFormat);
                    await tree.SetLeafAsync(key, $"value_{i}");
                }
                var insertTime = stopwatch.ElapsedMilliseconds - insertStart;

                // Compute root
                var rootStart = stopwatch.ElapsedMilliseconds;
                await tree.GetRootHashAsync();
                var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

                stopwatch.Stop();

                var leafCount = await tree.GetLeafCountAsync();
                var memoryEstimate = EstimateMemoryUsage(leafCount);
                var avgInsert = insertTime / (double)itemsPerDepth;

                _output.WriteLine($"{depth,5} | {leafCount,5} | {insertTime,9} | {rootTime,7} | {avgInsert,10:F2} | {memoryEstimate,8:F1}");

                Assert.Equal(itemsPerDepth, leafCount);
            }
        }

        private double EstimateMemoryUsage(long leafCount)
        {
            // Rough estimate: each leaf ~100 bytes (key + hash + overhead)
            // Plus cache overhead and tree structure
            var leafMemory = leafCount * 100;
            var cacheMemory = leafCount * 50; // Estimated cache overhead
            var totalBytes = leafMemory + cacheMemory;
            return totalBytes / (1024.0 * 1024.0); // Convert to MB
        }
    }
}
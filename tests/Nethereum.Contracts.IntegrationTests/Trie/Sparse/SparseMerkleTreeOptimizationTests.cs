using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Nethereum.Merkle.Sparse;
using Nethereum.Util.HashProviders;
using Nethereum.Util.ByteArrayConvertors;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Contracts.IntegrationTests.Trie.Sparse
{
    /// <summary>
    /// Tests to verify performance optimizations for handling millions of records
    /// </summary>
    public class SparseMerkleTreeOptimizationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider;

        public SparseMerkleTreeOptimizationTests(ITestOutputHelper output)
        {
            _output = output;
            _hashProvider = new Sha3KeccackHashProvider();
        }

        [Fact]
        public async Task OptimizedUpdates_ShouldBeMuchFasterThanBefore()
        {
            // Test the key optimization: O(log N) vs O(N) cache invalidation
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);

            const int itemCount = 2000; // Start with smaller count
            var stopwatch = Stopwatch.StartNew();

            _output.WriteLine($"Testing optimized updates with {itemCount} items...");

            // Phase 1: Initial population
            var populateStart = stopwatch.ElapsedMilliseconds;
            for (int i = 0; i < itemCount; i++)
            {
                await tree.SetLeafAsync($"{i:x4}", $"initial_value_{i}");
            }
            var populateTime = stopwatch.ElapsedMilliseconds - populateStart;

            // Phase 2: First root computation (should cache result)
            var firstRootStart = stopwatch.ElapsedMilliseconds;
            var firstRoot = await tree.GetRootHashAsync();
            var firstRootTime = stopwatch.ElapsedMilliseconds - firstRootStart;

            // Phase 3: Second root computation (should use cache)
            var secondRootStart = stopwatch.ElapsedMilliseconds;
            var secondRoot = await tree.GetRootHashAsync();
            var secondRootTime = stopwatch.ElapsedMilliseconds - secondRootStart;

            // Phase 4: Single update and new root (should be fast with incremental update)
            var updateStart = stopwatch.ElapsedMilliseconds;
            await tree.SetLeafAsync("0001", "updated_value");
            var newRoot = await tree.GetRootHashAsync();
            var updateTime = stopwatch.ElapsedMilliseconds - updateStart;

            stopwatch.Stop();

            _output.WriteLine($"Results:");
            _output.WriteLine($"  Initial population: {populateTime}ms ({populateTime / (double)itemCount:F2}ms per item)");
            _output.WriteLine($"  First root computation: {firstRootTime}ms");
            _output.WriteLine($"  Second root computation (cached): {secondRootTime}ms");
            _output.WriteLine($"  Single update + new root: {updateTime}ms");
            _output.WriteLine($"  Total leaves: {await tree.GetLeafCountAsync()}");
            _output.WriteLine($"  Root changed: {firstRoot != newRoot}");

            // Assertions for optimized performance
            Assert.Equal(firstRoot, secondRoot); // Caching should work
            Assert.NotEqual(firstRoot, newRoot); // Update should change root
            Assert.True(secondRootTime < 10, $"Cached root computation took too long: {secondRootTime}ms"); // Should be nearly instant
            Assert.True(updateTime < 100, $"Incremental update took too long: {updateTime}ms"); // Should be much faster than full recompute
            Assert.True(populateTime / (double)itemCount < 1.0, "Population is taking too long per item"); // Should be under 1ms per item
        }

        [Fact]
        public async Task BatchUpdates_ShouldBeMoreEfficientThanIndividualUpdates()
        {
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);

            const int batchSize = 500;
            var stopwatch = Stopwatch.StartNew();

            // Test individual updates
            var individualStart = stopwatch.ElapsedMilliseconds;
            for (int i = 0; i < batchSize; i++)
            {
                await tree.SetLeafAsync($"{i:x4}", $"individual_value_{i}");
            }
            var individualRoot = await tree.GetRootHashAsync();
            var individualTime = stopwatch.ElapsedMilliseconds - individualStart;

            // Clear and test batch updates
            await tree.ClearAsync();
            
            var batchStart = stopwatch.ElapsedMilliseconds;
            var batchUpdates = new Dictionary<string, string>();
            for (int i = 0; i < batchSize; i++)
            {
                batchUpdates[$"{i:x4}"] = $"batch_value_{i}";
            }
            await tree.SetLeavesAsync(batchUpdates);
            var batchRoot = await tree.GetRootHashAsync();
            var batchTime = stopwatch.ElapsedMilliseconds - batchStart;

            stopwatch.Stop();

            _output.WriteLine($"Batch vs Individual Updates Comparison:");
            _output.WriteLine($"  Individual updates: {individualTime}ms ({individualTime / (double)batchSize:F2}ms per item)");
            _output.WriteLine($"  Batch updates: {batchTime}ms ({batchTime / (double)batchSize:F2}ms per item)");
            _output.WriteLine($"  Batch speedup: {individualTime / (double)batchTime:F1}x faster");
            _output.WriteLine($"  Both results have same structure: {await tree.GetLeafCountAsync()} leaves");

            // Batch updates should be more efficient
            Assert.True(batchTime < individualTime, $"Batch updates should be faster: {batchTime}ms vs {individualTime}ms");
            Assert.Equal(batchSize, await tree.GetLeafCountAsync());
        }

        [Fact]
        public async Task LargeDataset_ShouldMaintainPerformance()
        {
            // Test with a larger dataset to verify scalability
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(20, _hashProvider, convertor, storage);

            const int datasetSize = 10000; // 10K records
            var stopwatch = Stopwatch.StartNew();

            _output.WriteLine($"Testing large dataset performance with {datasetSize} records...");

            // Populate using batch updates for efficiency
            var batchSize = 1000;
            for (int batch = 0; batch < datasetSize / batchSize; batch++)
            {
                var batchStart = stopwatch.ElapsedMilliseconds;
                var updates = new Dictionary<string, string>();
                
                for (int i = 0; i < batchSize; i++)
                {
                    var index = batch * batchSize + i;
                    updates[$"{index:x5}"] = $"large_dataset_value_{index}";
                }
                
                await tree.SetLeavesAsync(updates);
                var batchTime = stopwatch.ElapsedMilliseconds - batchStart;
                
                _output.WriteLine($"  Batch {batch + 1}: {batchSize} items in {batchTime}ms");
            }

            // Test root computation performance
            var rootStart = stopwatch.ElapsedMilliseconds;
            var root = await tree.GetRootHashAsync();
            var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

            // Test incremental update performance
            var updateStart = stopwatch.ElapsedMilliseconds;
            await tree.SetLeafAsync("00001", "updated_large_value");
            var newRoot = await tree.GetRootHashAsync();
            var updateTime = stopwatch.ElapsedMilliseconds - updateStart;

            stopwatch.Stop();

            _output.WriteLine($"Large Dataset Results:");
            _output.WriteLine($"  Total records: {await tree.GetLeafCountAsync()}");
            _output.WriteLine($"  Root computation: {rootTime}ms");
            _output.WriteLine($"  Incremental update: {updateTime}ms");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Root changed: {root != newRoot}");

            // Performance assertions for large datasets
            Assert.Equal(datasetSize, await tree.GetLeafCountAsync());
            Assert.NotEqual(root, newRoot);
            Assert.True(rootTime < 5000, $"Root computation took too long: {rootTime}ms");
            Assert.True(updateTime < 100, $"Incremental update took too long: {updateTime}ms");
        }
    }
}
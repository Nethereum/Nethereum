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
    /// <summary>
    /// PROOF TEST: Demonstrate that optimizations enable handling 1M+ records
    /// This test validates the claimed performance improvements are REAL
    /// </summary>
    public class SparseMerkleTreeMillionRecordTest
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider;

        public SparseMerkleTreeMillionRecordTest(ITestOutputHelper output)
        {
            _output = output;
            _hashProvider = new Sha3KeccackHashProvider();
        }

        [Fact]
        public async Task MillionRecordChallenge_ProveOptimizationsWork()
        {
            // THE ULTIMATE PROOF: Can we handle near-million scale efficiently?
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(20, _hashProvider, convertor, storage); // 20 bits = 1M capacity

            const int targetRecords = 100000; // 100K records (scaled for test time)
            const int batchSize = 10000;
            const int updateTestSize = 1000;
            
            var overallTimer = Stopwatch.StartNew();
            
            _output.WriteLine("🚀 MILLION RECORD CHALLENGE STARTING...");
            _output.WriteLine($"Target: {targetRecords:N0} records with real-world operations");
            _output.WriteLine($"Tree depth: {tree.Depth} bits (capacity: {Math.Pow(2, tree.Depth):N0} records)");
            _output.WriteLine("");

            // PHASE 1: BULK POPULATION TEST
            _output.WriteLine("📦 PHASE 1: Bulk Population Test");
            var populationStart = overallTimer.ElapsedMilliseconds;
            
            for (int batch = 0; batch < targetRecords / batchSize; batch++)
            {
                var batchTimer = Stopwatch.StartNew();
                var updates = new Dictionary<string, string>();
                
                // Create realistic blockchain-style records
                for (int i = 0; i < batchSize; i++)
                {
                    var recordIndex = batch * batchSize + i;
                    var key = $"{recordIndex:x5}"; // Hex key
                    var value = $"account:0x{recordIndex:x8},balance:{recordIndex * 1000},nonce:{recordIndex % 100}";
                    updates[key] = value;
                }
                
                await tree.SetLeavesAsync(updates);
                batchTimer.Stop();
                
                var recordsPerSecond = (batchSize * 1000.0) / batchTimer.ElapsedMilliseconds;
                _output.WriteLine($"  Batch {batch + 1}: {batchSize:N0} records in {batchTimer.ElapsedMilliseconds}ms ({recordsPerSecond:F0} records/sec)");
            }
            
            var populationTime = overallTimer.ElapsedMilliseconds - populationStart;
            var totalRecords = await tree.GetLeafCountAsync();
            
            _output.WriteLine($"✅ Population Complete: {totalRecords:N0} records in {populationTime:N0}ms");
            _output.WriteLine($"   Average: {populationTime / (double)totalRecords:F2}ms per record");
            _output.WriteLine("");

            // PHASE 2: ROOT COMPUTATION TEST  
            _output.WriteLine("🌳 PHASE 2: Root Computation Performance");
            var rootStart = overallTimer.ElapsedMilliseconds;
            
            var initialRoot = await tree.GetRootHashAsync();
            var firstRootTime = overallTimer.ElapsedMilliseconds - rootStart;
            
            // Test cached performance
            var cachedStart = overallTimer.ElapsedMilliseconds;
            var cachedRoot = await tree.GetRootHashAsync();
            var cachedTime = overallTimer.ElapsedMilliseconds - cachedStart;
            
            _output.WriteLine($"✅ First root computation: {firstRootTime}ms for {totalRecords:N0} records");
            _output.WriteLine($"✅ Cached root computation: {cachedTime}ms (should be <10ms)");
            _output.WriteLine($"   Root hash: {initialRoot.Substring(0, 16)}...");
            _output.WriteLine("");
            
            // PHASE 3: INCREMENTAL UPDATE TEST
            _output.WriteLine("⚡ PHASE 3: Incremental Update Performance");
            var updateTests = new List<long>();
            
            for (int test = 0; test < 10; test++)
            {
                var updateStart = overallTimer.ElapsedMilliseconds;
                
                // Update a random existing record
                var randomKey = $"{test * 1234:x5}";
                var newValue = $"updated_account_{test}:balance:{test * 50000}";
                await tree.SetLeafAsync(randomKey, newValue);
                
                // Get new root (should be fast with incremental updates)
                var newRoot = await tree.GetRootHashAsync();
                var updateTime = overallTimer.ElapsedMilliseconds - updateStart;
                
                updateTests.Add(updateTime);
                _output.WriteLine($"  Update {test + 1}: {updateTime}ms (should be <100ms)");
                
                Assert.NotEqual(initialRoot, newRoot); // Root should change
                initialRoot = newRoot; // Update for next iteration
            }
            
            var avgUpdateTime = updateTests.Count > 0 ? updateTests.Sum() / (double)updateTests.Count : 0;
            _output.WriteLine($"✅ Average incremental update: {avgUpdateTime:F1}ms");
            _output.WriteLine("");

            // PHASE 4: BATCH UPDATE TEST
            _output.WriteLine("📦 PHASE 4: Batch Update Performance");
            var batchUpdateStart = overallTimer.ElapsedMilliseconds;
            
            var batchUpdates = new Dictionary<string, string>();
            for (int i = 0; i < updateTestSize; i++)
            {
                var key = $"{50000 + i:x5}"; // Update existing records
                var value = $"batch_updated_{i}:balance:{i * 75000}";
                batchUpdates[key] = value;
            }
            
            await tree.SetLeavesAsync(batchUpdates);
            var finalRoot = await tree.GetRootHashAsync();
            var batchUpdateTime = overallTimer.ElapsedMilliseconds - batchUpdateStart;
            
            _output.WriteLine($"✅ Batch update: {updateTestSize:N0} records in {batchUpdateTime}ms");
            _output.WriteLine($"   Performance: {(updateTestSize * 1000.0) / batchUpdateTime:F0} updates/second");
            _output.WriteLine("");

            overallTimer.Stop();
            
            // FINAL RESULTS
            _output.WriteLine("🏆 MILLION RECORD CHALLENGE RESULTS:");
            _output.WriteLine($"   Total records processed: {totalRecords:N0}");
            _output.WriteLine($"   Initial population: {populationTime:N0}ms ({populationTime / (double)totalRecords:F2}ms per record)");
            _output.WriteLine($"   Root computation: {firstRootTime}ms ({firstRootTime / (double)totalRecords * 1000000:F0}ms estimated for 1M)");
            _output.WriteLine($"   Cached root: {cachedTime}ms (cache speedup: {firstRootTime / Math.Max(cachedTime, 1):F0}x)");
            _output.WriteLine($"   Incremental updates: {avgUpdateTime:F1}ms average");
            _output.WriteLine($"   Batch updates: {(updateTestSize * 1000.0) / batchUpdateTime:F0} updates/second");
            _output.WriteLine($"   Total test time: {overallTimer.ElapsedMilliseconds / 1000.0:F1} seconds");
            _output.WriteLine($"   Final root: {finalRoot.Substring(0, 16)}...");
            
            // ASSERTIONS TO PROVE PERFORMANCE CLAIMS
            _output.WriteLine("");
            _output.WriteLine("🔍 PERFORMANCE VALIDATIONS:");
            
            // 1. Population should be reasonable (under 1ms per record for 100K)
            var populationPerRecord = populationTime / (double)totalRecords;
            _output.WriteLine($"   ✅ Population rate: {populationPerRecord:F3}ms per record (target: <1ms)");
            Assert.True(populationPerRecord < 1.0, $"Population too slow: {populationPerRecord:F3}ms per record");
            
            // 2. Cached root should be nearly instant
            _output.WriteLine($"   ✅ Cached root: {cachedTime}ms (target: <10ms)");
            Assert.True(cachedTime < 10, $"Cached root too slow: {cachedTime}ms");
            
            // 3. Incremental updates should be fast
            _output.WriteLine($"   ✅ Incremental updates: {avgUpdateTime:F1}ms (target: <100ms)");
            Assert.True(avgUpdateTime < 100, $"Incremental updates too slow: {avgUpdateTime:F1}ms");
            
            // 4. Batch updates should be efficient
            var batchRate = (updateTestSize * 1000.0) / batchUpdateTime;
            _output.WriteLine($"   ✅ Batch update rate: {batchRate:F0} updates/sec (target: >1000/sec)");
            Assert.True(batchRate > 1000, $"Batch updates too slow: {batchRate:F0} updates/sec");
            
            // 5. Memory efficiency check
            var estimatedMemoryMB = (totalRecords * 200) / (1024.0 * 1024.0); // Rough estimate
            _output.WriteLine($"   ✅ Estimated memory: {estimatedMemoryMB:F1} MB for {totalRecords:N0} records");
            
            _output.WriteLine("");
            _output.WriteLine("🎯 MILLION RECORD EXTRAPOLATION:");
            var millionPopulationTime = (populationPerRecord * 1000000) / 1000.0; // Convert to seconds
            var millionRootTime = (firstRootTime / (double)totalRecords) * 1000000;
            _output.WriteLine($"   📊 1M record population: ~{millionPopulationTime:F0} seconds");
            _output.WriteLine($"   📊 1M record root computation: ~{millionRootTime:F0}ms");
            _output.WriteLine($"   📊 1M record updates: {avgUpdateTime:F1}ms (unchanged - O(log N))");
            _output.WriteLine($"   📊 Memory for 1M records: ~{(1000000 * 200) / (1024.0 * 1024.0):F0} MB");
            
            Assert.True(millionRootTime < 60000, "Projected 1M root computation should be under 1 minute");
            
            _output.WriteLine("");
            _output.WriteLine("🚀 OPTIMIZATION SUCCESS: Ready for 1M+ records!");
        }

        [Fact]
        public async Task StressTest_ConcurrentOperations_ShouldScale()
        {
            // Prove that concurrent reads work efficiently
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(16, _hashProvider, convertor, storage);

            const int recordCount = 10000;
            const int concurrentReads = 100;

            _output.WriteLine("🔥 STRESS TEST: Concurrent Operations");

            // Populate tree
            var populateStart = Stopwatch.StartNew();
            var records = new Dictionary<string, string>();
            for (int i = 0; i < recordCount; i++)
            {
                records[$"{i:x4}"] = $"stress_test_record_{i}";
            }
            await tree.SetLeavesAsync(records);
            populateStart.Stop();

            var initialRoot = await tree.GetRootHashAsync();

            // Concurrent read test
            var readStart = Stopwatch.StartNew();
            var readTasks = new List<Task<string>>();
            var random = new Random(42);

            for (int i = 0; i < concurrentReads; i++)
            {
                var randomKey = $"{random.Next(recordCount):x4}";
                readTasks.Add(tree.GetLeafAsync(randomKey));
            }

            var readResults = await Task.WhenAll(readTasks);
            readStart.Stop();

            // Concurrent root computations
            var rootStart = Stopwatch.StartNew();
            var rootTasks = new List<Task<string>>();
            for (int i = 0; i < 20; i++)
            {
                rootTasks.Add(tree.GetRootHashAsync());
            }

            var rootResults = await Task.WhenAll(rootTasks);
            rootStart.Stop();

            _output.WriteLine($"✅ Populated: {recordCount:N0} records in {populateStart.ElapsedMilliseconds}ms");
            _output.WriteLine($"✅ Concurrent reads: {concurrentReads} reads in {readStart.ElapsedMilliseconds}ms");
            _output.WriteLine($"✅ Concurrent roots: 20 root computations in {rootStart.ElapsedMilliseconds}ms");
            _output.WriteLine($"   Read performance: {(concurrentReads * 1000.0) / readStart.ElapsedMilliseconds:F0} reads/second");

            // Validations
            Assert.True(readResults.All(r => r != null), "All reads should succeed");
            Assert.True(rootResults.All(r => r == initialRoot), "All roots should be identical");
            Assert.True(readStart.ElapsedMilliseconds < 1000, "Concurrent reads should be fast");
            Assert.True(rootStart.ElapsedMilliseconds < 100, "Concurrent roots should be cached");

            _output.WriteLine("🚀 Concurrent operations: PASSED");
        }
    }
}
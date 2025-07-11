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
    /// Performance tests simulating real blockchain scenarios
    /// </summary>
    public class SparseMerkleTreeBlockchainPerformanceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider;

        public SparseMerkleTreeBlockchainPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _hashProvider = new Sha3KeccackHashProvider();
        }

        [Fact]
        public async Task Blockchain_AccountStateUpdates_ShouldBeEfficient()
        {
            // Simulate Ethereum account state updates using 160-bit addresses
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(160, _hashProvider, convertor, storage);
            
            const int accountCount = 2000;
            const int updateRounds = 5;

            var stopwatch = Stopwatch.StartNew();
            var addresses = new List<string>();

            // Generate realistic Ethereum addresses
            var random = new Random(42);
            for (int i = 0; i < accountCount; i++)
            {
                var address = "0x" + string.Join("", Enumerable.Range(0, 40)
                    .Select(_ => random.Next(16).ToString("x")));
                addresses.Add(address.Substring(2)); // Remove 0x prefix
            }

            _output.WriteLine($"Blockchain Account State Performance Test:");
            _output.WriteLine($"Simulating {accountCount} accounts with {updateRounds} update rounds");
            _output.WriteLine("");

            var totalInsertTime = 0L;
            var totalRootTime = 0L;

            for (int round = 0; round < updateRounds; round++)
            {
                var roundStart = stopwatch.ElapsedMilliseconds;

                // Simulate account state updates (balances, nonces, etc.)
                var insertStart = stopwatch.ElapsedMilliseconds;
                for (int i = 0; i < accountCount; i++)
                {
                    var address = addresses[i];
                    var balance = random.NextInt64(0, 1000000000000000000); // Random balance in wei
                    var nonce = random.Next(0, 1000);
                    var codeHash = random.Next().ToString("x8");
                    
                    // Encode account state (simplified)
                    var accountState = $"balance:{balance},nonce:{nonce},code:{codeHash}";
                    await tree.SetLeafAsync(address, accountState);
                }
                var insertTime = stopwatch.ElapsedMilliseconds - insertStart;
                totalInsertTime += insertTime;

                // Compute state root (like after each block)
                var rootStart = stopwatch.ElapsedMilliseconds;
                var stateRoot = await tree.GetRootHashAsync();
                var rootTime = stopwatch.ElapsedMilliseconds - rootStart;
                totalRootTime += rootTime;

                var roundTime = stopwatch.ElapsedMilliseconds - roundStart;
                
                _output.WriteLine($"Round {round + 1}: {insertTime}ms updates + {rootTime}ms root = {roundTime}ms total");
            }

            stopwatch.Stop();
            var leafCount = await tree.GetLeafCountAsync();

            _output.WriteLine("");
            _output.WriteLine($"Final Results:");
            _output.WriteLine($"  Total accounts: {leafCount}");
            _output.WriteLine($"  Total insert time: {totalInsertTime}ms ({totalInsertTime / (double)(accountCount * updateRounds):F2}ms per update)");
            _output.WriteLine($"  Total root time: {totalRootTime}ms ({totalRootTime / (double)updateRounds:F2}ms per root)");
            _output.WriteLine($"  Total execution time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Throughput: {(accountCount * updateRounds * 1000.0) / stopwatch.ElapsedMilliseconds:F0} updates/second");

            Assert.Equal(accountCount, leafCount);
            Assert.True(totalInsertTime < 60000, "Account updates took too long"); // 60 seconds max
            Assert.True(totalRootTime < 30000, "Root computations took too long"); // 30 seconds max
        }

        [Fact]
        public async Task Blockchain_ContractStorageUpdates_ShouldScale()
        {
            // Simulate smart contract storage updates using 256-bit storage keys
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(256, _hashProvider, convertor, storage);
            
            const int contractCount = 10;
            const int slotsPerContract = 500;
            const int updateCycles = 3;

            var stopwatch = Stopwatch.StartNew();
            var random = new Random(42);

            _output.WriteLine($"Smart Contract Storage Performance Test:");
            _output.WriteLine($"Simulating {contractCount} contracts × {slotsPerContract} storage slots × {updateCycles} cycles");
            _output.WriteLine("");

            var contracts = new List<string>();
            for (int i = 0; i < contractCount; i++)
            {
                contracts.Add($"contract_{i:x8}");
            }

            for (int cycle = 0; cycle < updateCycles; cycle++)
            {
                var cycleStart = stopwatch.ElapsedMilliseconds;
                var operationsInCycle = 0;

                foreach (var contractId in contracts)
                {
                    var contractStart = stopwatch.ElapsedMilliseconds;

                    // Update storage slots for this contract
                    for (int slot = 0; slot < slotsPerContract; slot++)
                    {
                        // Create storage key: keccak256(contract_address + slot)
                        var storageKey = $"{contractId}{slot:x8}".PadRight(64, '0');
                        var value = random.Next().ToString("x8").PadLeft(64, '0');
                        
                        await tree.SetLeafAsync(storageKey, value);
                        operationsInCycle++;
                    }

                    var contractTime = stopwatch.ElapsedMilliseconds - contractStart;
                    if (cycle == 0) // Only log details for first cycle
                    {
                        _output.WriteLine($"  {contractId}: {slotsPerContract} slots in {contractTime}ms");
                    }
                }

                // Compute storage root
                var rootStart = stopwatch.ElapsedMilliseconds;
                var storageRoot = await tree.GetRootHashAsync();
                var rootTime = stopwatch.ElapsedMilliseconds - rootStart;

                var cycleTime = stopwatch.ElapsedMilliseconds - cycleStart;
                _output.WriteLine($"Cycle {cycle + 1}: {operationsInCycle} operations in {cycleTime}ms (root: {rootTime}ms)");
            }

            stopwatch.Stop();
            var leafCount = await tree.GetLeafCountAsync();
            var totalOperations = contractCount * slotsPerContract * updateCycles;

            _output.WriteLine("");
            _output.WriteLine($"Contract Storage Results:");
            _output.WriteLine($"  Final storage entries: {leafCount}");
            _output.WriteLine($"  Total operations: {totalOperations}");
            _output.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Throughput: {(totalOperations * 1000.0) / stopwatch.ElapsedMilliseconds:F0} operations/second");
            _output.WriteLine($"  Memory estimate: {EstimateMemoryUsage(leafCount):F1} MB");

            Assert.Equal(contractCount * slotsPerContract, leafCount);
            Assert.True(stopwatch.ElapsedMilliseconds < 120000, "Contract storage updates took too long"); // 2 minutes max
        }

        [Fact]
        public async Task Blockchain_BlockProcessing_ShouldHandleBursts()
        {
            // Simulate processing blocks with varying transaction counts
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(256, _hashProvider, convertor, storage);
            
            var random = new Random(42);
            var blockSizes = new[] { 50, 150, 300, 500, 200, 100, 75 }; // Varying block sizes
            var totalTransactions = 0;

            _output.WriteLine($"Block Processing Performance Test:");
            _output.WriteLine($"Simulating {blockSizes.Length} blocks with varying transaction counts");
            _output.WriteLine("");

            var overallStopwatch = Stopwatch.StartNew();

            for (int blockNum = 0; blockNum < blockSizes.Length; blockNum++)
            {
                var blockStart = overallStopwatch.ElapsedMilliseconds;
                var txCount = blockSizes[blockNum];
                
                // Process transactions in this block
                for (int txIndex = 0; txIndex < txCount; txIndex++)
                {
                    // Simulate transaction effects on state
                    var fromAddress = GenerateRandomAddress(random);
                    var toAddress = GenerateRandomAddress(random);
                    var amount = random.NextInt64(1, 1000000000);

                    // Update sender balance
                    var fromBalance = random.NextInt64(amount, amount * 10);
                    await tree.SetLeafAsync(fromAddress, $"balance:{fromBalance - amount}");

                    // Update receiver balance  
                    var toBalance = random.NextInt64(0, 1000000000);
                    await tree.SetLeafAsync(toAddress, $"balance:{toBalance + amount}");

                    totalTransactions += 2; // Two state updates per transaction
                }

                // Compute state root for block
                var rootStart = overallStopwatch.ElapsedMilliseconds;
                var blockStateRoot = await tree.GetRootHashAsync();
                var rootTime = overallStopwatch.ElapsedMilliseconds - rootStart;

                var blockTime = overallStopwatch.ElapsedMilliseconds - blockStart;
                var txPerSecond = (txCount * 1000.0) / blockTime;

                _output.WriteLine($"Block {blockNum + 1}: {txCount} tx in {blockTime}ms ({txPerSecond:F0} tx/s, root: {rootTime}ms)");
            }

            overallStopwatch.Stop();
            var leafCount = await tree.GetLeafCountAsync();

            _output.WriteLine("");
            _output.WriteLine($"Block Processing Results:");
            _output.WriteLine($"  Total blocks processed: {blockSizes.Length}");
            _output.WriteLine($"  Total transactions: {blockSizes.Sum()}");
            _output.WriteLine($"  Total state updates: {totalTransactions}");
            _output.WriteLine($"  Unique accounts: {leafCount}");
            _output.WriteLine($"  Total time: {overallStopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Average throughput: {(totalTransactions * 1000.0) / overallStopwatch.ElapsedMilliseconds:F0} updates/second");

            Assert.True(leafCount > 0);
            Assert.True(overallStopwatch.ElapsedMilliseconds < 60000, "Block processing took too long"); // 1 minute max
        }

        [Fact]
        public async Task Blockchain_StateSnapshots_ShouldSupportQueries()
        {
            // Test state querying performance (important for dApps)
            var storage = new InMemorySparseMerkleTreeStorage<string>();
            var convertor = new StringByteArrayConvertor();
            var tree = new SparseMerkleTree<string>(160, _hashProvider, convertor, storage);
            
            const int accountCount = 1000;
            var random = new Random(42);

            // Setup initial state
            var accounts = new Dictionary<string, string>();
            for (int i = 0; i < accountCount; i++)
            {
                var address = GenerateRandomAddress(random);
                var balance = random.NextInt64(0, 1000000000000);
                var accountData = $"balance:{balance},nonce:{random.Next(0, 100)}";
                
                accounts[address] = accountData;
                await tree.SetLeafAsync(address, accountData);
            }

            var stateRoot = await tree.GetRootHashAsync();
            
            _output.WriteLine($"State Query Performance Test:");
            _output.WriteLine($"State with {accountCount} accounts, root: {stateRoot.Substring(0, 16)}...");
            _output.WriteLine("");

            // Test random access patterns
            var queryStopwatch = Stopwatch.StartNew();
            var queryCount = 500;
            var accountList = accounts.Keys.ToList();
            
            for (int i = 0; i < queryCount; i++)
            {
                var randomAddress = accountList[random.Next(accountList.Count)];
                var accountData = await tree.GetLeafAsync(randomAddress);
                Assert.NotNull(accountData);
                Assert.Contains("balance:", accountData);
            }
            
            queryStopwatch.Stop();

            // Test batch queries
            var batchStopwatch = Stopwatch.StartNew();
            var batchSize = 100;
            var queryTasks = new List<Task<string>>();
            
            for (int i = 0; i < batchSize; i++)
            {
                var randomAddress = accountList[random.Next(accountList.Count)];
                queryTasks.Add(tree.GetLeafAsync(randomAddress));
            }
            
            var batchResults = await Task.WhenAll(queryTasks);
            batchStopwatch.Stop();

            _output.WriteLine($"Query Performance Results:");
            _output.WriteLine($"  Sequential queries: {queryCount} in {queryStopwatch.ElapsedMilliseconds}ms ({queryStopwatch.ElapsedMilliseconds / (double)queryCount:F2}ms per query)");
            _output.WriteLine($"  Batch queries: {batchSize} in {batchStopwatch.ElapsedMilliseconds}ms ({batchStopwatch.ElapsedMilliseconds / (double)batchSize:F2}ms per query)");
            _output.WriteLine($"  Query throughput: {(queryCount * 1000.0) / queryStopwatch.ElapsedMilliseconds:F0} queries/second");

            Assert.True(batchResults.All(r => r != null));
            Assert.True(queryStopwatch.ElapsedMilliseconds < 5000, "Queries took too long");
        }

        private string GenerateRandomAddress(Random random)
        {
            return string.Join("", Enumerable.Range(0, 40)
                .Select(_ => random.Next(16).ToString("x")));
        }

        private double EstimateMemoryUsage(long leafCount)
        {
            // More accurate estimate for blockchain data
            var leafMemory = leafCount * 120; // Larger entries for account/storage data
            var cacheMemory = leafCount * 60; // Cache overhead
            var indexMemory = leafCount * 40; // Hash indexing
            var totalBytes = leafMemory + cacheMemory + indexMemory;
            return totalBytes / (1024.0 * 1024.0); // Convert to MB
        }
    }
}
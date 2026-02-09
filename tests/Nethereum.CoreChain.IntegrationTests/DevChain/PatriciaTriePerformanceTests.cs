using System.Diagnostics;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class PatriciaTriePerformanceTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly ITestOutputHelper _output;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");
        private readonly LegacyTransactionSigner _signer = new();

        public PatriciaTriePerformanceTests(DevChainNodeFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task ERC20_1000Transfers_DifferentAccounts_PerformanceTest()
        {
            const int transferCount = 1000;
            const int batchSize = 100;

            _output.WriteLine($"=== ERC20 Performance Test: {transferCount} transfers to different accounts ===\n");

            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1_000_000);
            _output.WriteLine($"Deployed ERC20 at: {contractAddress}");

            var recipientAddresses = GenerateRandomAddresses(transferCount);
            _output.WriteLine($"Generated {transferCount} unique recipient addresses\n");

            var batchTimes = new List<double>();
            var allTxTimes = new List<long>();
            var stateRoots = new HashSet<string>();

            var initialBlock = await _fixture.Node.GetLatestBlockAsync();
            stateRoots.Add(initialBlock.StateRoot.ToHex());

            var totalSw = Stopwatch.StartNew();

            for (int batch = 0; batch < transferCount / batchSize; batch++)
            {
                var batchSw = Stopwatch.StartNew();

                for (int i = 0; i < batchSize; i++)
                {
                    var txIndex = batch * batchSize + i;
                    var recipient = recipientAddresses[txIndex];

                    var txSw = Stopwatch.StartNew();

                    var transferFunction = new TransferFunction { To = recipient, Value = 100 };
                    var callData = transferFunction.GetCallData();
                    var signedTx = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
                    var result = await _fixture.Node.SendTransactionAsync(signedTx);

                    txSw.Stop();
                    allTxTimes.Add(txSw.ElapsedMilliseconds);

                    if (!result.Success)
                    {
                        _output.WriteLine($"  Tx {txIndex} FAILED: {result.RevertReason}");
                        continue;
                    }

                    var block = await _fixture.Node.GetLatestBlockAsync();
                    stateRoots.Add(block.StateRoot.ToHex());
                }

                batchSw.Stop();
                var batchAvg = batchSw.ElapsedMilliseconds / (double)batchSize;
                batchTimes.Add(batchAvg);

                _output.WriteLine($"Batch {batch + 1}/{transferCount / batchSize}: " +
                    $"{batchSw.ElapsedMilliseconds}ms total, {batchAvg:F1}ms avg/tx");
            }

            totalSw.Stop();

            _output.WriteLine($"\n=== RESULTS ===");
            _output.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Total transactions: {transferCount}");
            _output.WriteLine($"Unique state roots: {stateRoots.Count}");
            _output.WriteLine($"Overall avg: {totalSw.ElapsedMilliseconds / (double)transferCount:F1}ms/tx");

            _output.WriteLine($"\n=== BATCH PERFORMANCE (checking for degradation) ===");
            for (int i = 0; i < batchTimes.Count; i++)
            {
                var degradation = i > 0 ? (batchTimes[i] / batchTimes[0] - 1) * 100 : 0;
                _output.WriteLine($"Batch {i + 1}: {batchTimes[i]:F1}ms avg " +
                    $"({(degradation >= 0 ? "+" : "")}{degradation:F1}% vs first batch)");
            }

            var firstBatchAvg = batchTimes.First();
            var lastBatchAvg = batchTimes.Last();
            var degradationPercent = ((lastBatchAvg / firstBatchAvg) - 1) * 100;

            _output.WriteLine($"\n=== DEGRADATION ANALYSIS ===");
            _output.WriteLine($"First batch avg: {firstBatchAvg:F1}ms");
            _output.WriteLine($"Last batch avg: {lastBatchAvg:F1}ms");
            _output.WriteLine($"Degradation: {degradationPercent:F1}%");

            if (degradationPercent > 100)
            {
                _output.WriteLine($"WARNING: Performance degraded by more than 100% - possible O(n) behavior!");
            }
            else if (degradationPercent > 50)
            {
                _output.WriteLine($"NOTICE: Some degradation observed, but within acceptable range for O(log n)");
            }
            else
            {
                _output.WriteLine($"GOOD: Minimal degradation - consistent with O(log n) incremental updates");
            }

            Assert.Equal(transferCount + 1, stateRoots.Count);
            Assert.True(degradationPercent < 200,
                $"Performance degraded by {degradationPercent:F1}% which suggests O(n) instead of O(log n)");
        }

        [Fact]
        public async Task ERC20_StorageSlotDistribution_PerformanceTest()
        {
            const int transferCount = 500;

            _output.WriteLine($"=== Storage Slot Distribution Test: {transferCount} transfers ===\n");

            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1_000_000);
            _output.WriteLine($"Deployed ERC20 at: {contractAddress}");

            var recipientAddresses = GenerateRandomAddresses(transferCount);

            var times = new List<(int index, long ms, string rootHash)>();

            for (int i = 0; i < transferCount; i++)
            {
                var recipient = recipientAddresses[i];

                var sw = Stopwatch.StartNew();

                var transferFunction = new TransferFunction { To = recipient, Value = 100 };
                var callData = transferFunction.GetCallData();
                var signedTx = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);
                var result = await _fixture.Node.SendTransactionAsync(signedTx);

                sw.Stop();

                if (result.Success)
                {
                    var block = await _fixture.Node.GetLatestBlockAsync();
                    times.Add((i, sw.ElapsedMilliseconds, block.StateRoot.ToHex().Substring(0, 16)));
                }

                if ((i + 1) % 100 == 0)
                {
                    var last100 = times.Skip(Math.Max(0, times.Count - 100)).Take(100);
                    var avg = last100.Average(t => t.ms);
                    _output.WriteLine($"Progress: {i + 1}/{transferCount}, Last 100 avg: {avg:F1}ms");
                }
            }

            _output.WriteLine($"\n=== TIME DISTRIBUTION ===");

            var buckets = new[] { 0, 10, 20, 50, 100, 200, 500, 1000 };
            for (int b = 0; b < buckets.Length - 1; b++)
            {
                var count = times.Count(t => t.ms >= buckets[b] && t.ms < buckets[b + 1]);
                var pct = count * 100.0 / times.Count;
                _output.WriteLine($"  {buckets[b]}-{buckets[b + 1]}ms: {count} ({pct:F1}%)");
            }
            var over1000 = times.Count(t => t.ms >= 1000);
            _output.WriteLine($"  >1000ms: {over1000} ({over1000 * 100.0 / times.Count:F1}%)");

            var first50Avg = times.Take(50).Average(t => t.ms);
            var last50Avg = times.TakeLast(50).Average(t => t.ms);

            _output.WriteLine($"\n=== TREND ANALYSIS ===");
            _output.WriteLine($"First 50 avg: {first50Avg:F1}ms");
            _output.WriteLine($"Last 50 avg: {last50Avg:F1}ms");
            _output.WriteLine($"Ratio: {last50Avg / first50Avg:F2}x");

            var uniqueRoots = times.Select(t => t.rootHash).Distinct().Count();
            _output.WriteLine($"\nUnique state root prefixes: {uniqueRoots}");

            Assert.True(last50Avg / first50Avg < 3.0,
                $"Performance ratio {last50Avg / first50Avg:F2}x is too high, expected < 3x for O(log n)");
        }

        [Fact]
        public async Task MultipleContracts_CrossContractStorage_PerformanceTest()
        {
            const int contractCount = 10;
            const int transfersPerContract = 50;

            _output.WriteLine($"=== Multi-Contract Test: {contractCount} contracts, {transfersPerContract} transfers each ===\n");

            var contracts = new List<string>();
            for (int i = 0; i < contractCount; i++)
            {
                var addr = await _fixture.DeployERC20Async(OneToken * 100_000);
                contracts.Add(addr);
                _output.WriteLine($"Deployed contract {i + 1}: {addr}");
            }

            var recipients = GenerateRandomAddresses(transfersPerContract);
            var contractTimes = new Dictionary<int, List<long>>();

            _output.WriteLine($"\nRunning transfers...");

            for (int round = 0; round < transfersPerContract; round++)
            {
                var recipient = recipients[round];

                for (int c = 0; c < contractCount; c++)
                {
                    var sw = Stopwatch.StartNew();

                    var transferFunction = new TransferFunction { To = recipient, Value = 10 };
                    var callData = transferFunction.GetCallData();
                    var signedTx = _fixture.CreateSignedTransaction(contracts[c], BigInteger.Zero, callData);
                    var result = await _fixture.Node.SendTransactionAsync(signedTx);

                    sw.Stop();

                    if (result.Success)
                    {
                        if (!contractTimes.ContainsKey(c))
                            contractTimes[c] = new List<long>();
                        contractTimes[c].Add(sw.ElapsedMilliseconds);
                    }
                }

                if ((round + 1) % 10 == 0)
                {
                    _output.WriteLine($"Round {round + 1}/{transfersPerContract} complete");
                }
            }

            _output.WriteLine($"\n=== PER-CONTRACT PERFORMANCE ===");
            foreach (var kvp in contractTimes.OrderBy(k => k.Key))
            {
                var avg = kvp.Value.Average();
                var max = kvp.Value.Max();
                _output.WriteLine($"Contract {kvp.Key + 1}: avg={avg:F1}ms, max={max}ms, count={kvp.Value.Count}");
            }

            var allTimes = contractTimes.Values.SelectMany(t => t).ToList();
            _output.WriteLine($"\n=== OVERALL ===");
            _output.WriteLine($"Total transactions: {allTimes.Count}");
            _output.WriteLine($"Overall avg: {allTimes.Average():F1}ms");
            _output.WriteLine($"Overall max: {allTimes.Max()}ms");

            var first100 = allTimes.Take(100).Average();
            var last100 = allTimes.TakeLast(100).Average();
            _output.WriteLine($"First 100 avg: {first100:F1}ms");
            _output.WriteLine($"Last 100 avg: {last100:F1}ms");
            _output.WriteLine($"Degradation ratio: {last100 / first100:F2}x");

            Assert.True(last100 / first100 < 3.0, "Performance degraded too much");
        }

        private static List<string> GenerateRandomAddresses(int count)
        {
            var addresses = new List<string>();
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                var bytes = new byte[20];
                random.NextBytes(bytes);
                addresses.Add("0x" + bytes.ToHex());
            }

            return addresses;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EEST.ConformanceRunner
{
    public class ThroughputBenchmarkTests
    {
        private readonly ITestOutputHelper _output;
        private readonly EestBlockchainTestsRlpDriver _driver = EestBlockchainTestsRlpDriver.Instance;

        public ThroughputBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private const int BatchSize = 750;
        private const int FullCorpusEstimate = 99_000;

        [Fact]
        public async Task MeasureRepresentativeBatch_SingleThreaded()
        {
            Assert.True(FixtureCatalog.CacheIsPresent, $"cache missing at {FixtureCatalog.BlockchainTestsRoot}");

            var cases = SampleCases(BatchSize);
            Assert.True(cases.Count > 0, "no fixture cases available to sample");

            var timings = new List<double>(cases.Count);
            int pass = 0, fail = 0;
            var sw = new Stopwatch();
            var kindCounts = new Dictionary<string, int>();
            var kindByForkCounts = new Dictionary<string, int>();
            var samples = new Dictionary<string, string>();

            foreach (var (file, test) in cases)
            {
                sw.Restart();
                ConformanceCaseResult result;
                try
                {
                    result = await _driver.RunAsync(test);
                }
                catch (Exception ex)
                {
                    result = ConformanceCaseResult.Fail("harnessException", ex.Message);
                }
                sw.Stop();

                timings.Add(sw.Elapsed.TotalMilliseconds);
                if (result.Success) pass++;
                else
                {
                    fail++;
                    kindCounts.TryGetValue(result.Kind, out var c);
                    kindCounts[result.Kind] = c + 1;
                    var forkKey = $"{result.Kind} / {test.Network}";
                    kindByForkCounts.TryGetValue(forkKey, out var fc);
                    kindByForkCounts[forkKey] = fc + 1;
                    if (!samples.ContainsKey(result.Kind) && samples.Count < 15)
                        samples[result.Kind] = $"{test.Name} [{test.Network}]: {result.Detail}";
                }
            }

            _output.WriteLine("=== Failure kind histogram ===");
            foreach (var kv in kindCounts.OrderByDescending(k => k.Value))
                _output.WriteLine($"  {kv.Key}: {kv.Value}");
            _output.WriteLine("=== Failure kind x fork ===");
            foreach (var kv in kindByForkCounts.OrderByDescending(k => k.Value))
                _output.WriteLine($"  {kv.Key}: {kv.Value}");
            _output.WriteLine("=== Sample failures ===");
            foreach (var kv in samples)
                _output.WriteLine($"  [{kv.Key}] {kv.Value}");

            timings.Sort();
            var totalMs = timings.Sum();
            var meanMs = totalMs / timings.Count;
            var p50 = timings[timings.Count / 2];
            var p95 = timings[(int)(timings.Count * 0.95)];
            var totalFixtures = FixtureCatalog.CountAllCases();

            _output.WriteLine("=== EEST Conformance Runner — Throughput (single-threaded) ===");
            _output.WriteLine($"Sampled: {timings.Count} fixtures (pass={pass}, fail={fail})");
            _output.WriteLine($"Per-fixture ms: mean={meanMs:F2} p50={p50:F2} p95={p95:F2} min={timings[0]:F2} max={timings[^1]:F2}");
            _output.WriteLine($"Batch wall time: {totalMs / 1000.0:F1}s for {timings.Count} fixtures");
            _output.WriteLine($"Discovered corpus (blockchain_tests, this cache): {totalFixtures} named cases");
            _output.WriteLine($"Extrapolated single-threaded wall time @ discovered corpus ({totalFixtures}): {meanMs * totalFixtures / 1000.0:F1}s (~{meanMs * totalFixtures / 60000.0:F1} min)");
            _output.WriteLine($"Extrapolated single-threaded wall time @ full-EEST estimate ({FullCorpusEstimate}): {meanMs * FullCorpusEstimate / 1000.0:F1}s (~{meanMs * FullCorpusEstimate / 60000.0:F1} min)");

            Directory.CreateDirectory(HiveResultsSink.ResultsDir);
            File.WriteAllText(
                Path.Combine(HiveResultsSink.ResultsDir, "throughput-single-threaded.txt"),
                $"sampled={timings.Count} pass={pass} fail={fail} meanMs={meanMs:F3} p50Ms={p50:F3} p95Ms={p95:F3} " +
                $"discoveredCorpus={totalFixtures} extrapolatedDiscoveredMin={meanMs * totalFixtures / 60000.0:F2} " +
                $"extrapolatedFullEestMin={meanMs * FullCorpusEstimate / 60000.0:F2}\n");
        }

        private static List<(string File, BlockchainTestLoader.BlockchainTest Test)> SampleCases(int max)
        {
            var files = FixtureCatalog.AllFixtureFiles;
            var result = new List<(string, BlockchainTestLoader.BlockchainTest)>(max);
            var rnd = new Random(1234567);
            var shuffled = files.OrderBy(_ => rnd.Next()).ToList();

            foreach (var file in shuffled)
            {
                if (result.Count >= max) break;

                List<BlockchainTestLoader.BlockchainTest> tests;
                try
                {
                    tests = BlockchainTestLoader.LoadFromFile(file);
                }
                catch
                {
                    continue;
                }

                foreach (var test in tests)
                {
                    if (result.Count >= max) break;
                    result.Add((file, test));
                }
            }

            return result;
        }
    }
}

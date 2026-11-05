using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.EEST.Conformance;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EestEngineConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestEngineConformance";
    }

    [Collection(EestEngineConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    public class EestBlockchainTestsEngineConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly ITestOutputHelper _output;

        public EestBlockchainTestsEngineConformanceTests(HiveResultsSink sink, ITestOutputHelper output)
        {
            _sink = sink;
            _output = output;
            _sink.EnsureSuite(EestBlockchainTestsEngineDriver.Instance);
        }

        [Fact]
        public Task AllEngineFixtures() =>
            EngineCorpusRun.RunAllAsync(Corpora.BlockchainEngine, "EestStable", _sink, _output);
    }

    public static class EngineCorpusRun
    {
        public static async Task RunAllAsync(ICorpusSource source, string settingsKey, HiveResultsSink sink, ITestOutputHelper output)
        {
            Assert.True(source.IsPresent,
                $"blockchain_tests_engine fixture cache not found at '{source.Root}'. Set ExternalPaths:{settingsKey} (see external/README.md).");

            var cases = Corpora.Selected(source).Select(c => (c.File, c.Name)).ToList();
            if (cases.Count == 0 && !string.IsNullOrEmpty(ConformanceSettings.Case)) return;
            Assert.True(cases.Count > 0,
                $"blockchain_tests_engine cache at '{source.Root}' contains zero cases - never silently skip.");

            EnginePayloadFileCache.Prime(cases);
            var failuresByKind = new ConcurrentDictionary<string, int>();

            await Parallel.ForEachAsync(
                cases,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (item, ct) => await RunOneAsync(item.File, item.Name, sink, failuresByKind));

            var (passed, failed, total) = sink.Counts;
            output.WriteLine($"consume-engine: {passed}/{total} passed, {failed} failed");
            foreach (var kv in failuresByKind.OrderByDescending(k => k.Value))
                output.WriteLine($"   [{kv.Key}] x{kv.Value}");

            Assert.True(failed == 0,
                $"consume-engine: {failed} of {total} cases failed ({passed} passed) - failure kinds: " +
                string.Join(", ", failuresByKind.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));
        }

        private static async Task RunOneAsync(string file, string testName, HiveResultsSink sink, ConcurrentDictionary<string, int> failuresByKind)
        {
            var start = DateTimeOffset.UtcNow;
            var caseId = $"{Corpora.RelativePath(file)}::{testName}";

            ConformanceCaseResult result;
            try
            {
                var tests = await FixtureFileCache.LoadAsync(file);
                var test = tests.FirstOrDefault(t => t.Name == testName);
                if (test == null)
                {
                    result = ConformanceCaseResult.Fail("missingCase", $"test '{testName}' not found in {file} on re-parse");
                }
                else
                {
                    var payloads = await EnginePayloadFileCache.GetEntriesAsync(file, testName);
                    result = await EestBlockchainTestsEngineDriver.Instance.RunAsync(test, payloads);
                }
            }
            catch (Exception ex)
            {
                result = ConformanceCaseResult.Fail("harnessException", $"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                EnginePayloadFileCache.Release(file);
            }

            var end = DateTimeOffset.UtcNow;
            var details = result.Success ? "" : $"[{result.Kind}] {result.Detail}";
            sink.Record(caseId, testName, result.Success, details, start, end);
            if (!result.Success)
                failuresByKind.AddOrUpdate(result.Kind, 1, (_, n) => n + 1);
        }
    }
}

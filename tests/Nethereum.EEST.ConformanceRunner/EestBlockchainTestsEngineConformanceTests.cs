using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public class EestBlockchainTestsEngineConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly ITestOutputHelper _output;
        private readonly EestBlockchainTestsEngineDriver _driver = EestBlockchainTestsEngineDriver.Instance;

        public EestBlockchainTestsEngineConformanceTests(HiveResultsSink sink, ITestOutputHelper output)
        {
            _sink = sink;
            _output = output;
            _sink.EnsureSuite(_driver);
        }

        [Fact]
        public async Task AllEngineFixtures()
        {
            Assert.True(FixtureCatalog.BlockchainTestsEngineCacheIsPresent,
                $"EEST blockchain_tests_engine fixture cache not found at " +
                $"'{FixtureCatalog.BlockchainTestsEngineRoot}'. See external/README.md / run-acceptance.sh.");

            var cases = FixtureCatalog.EngineCaseList();
            Assert.True(cases.Count > 0,
                $"blockchain_tests_engine cache at '{FixtureCatalog.BlockchainTestsEngineRoot}' contains zero cases - never silently skip.");

            EnginePayloadFileCache.Prime(cases);
            var failuresByKind = new ConcurrentDictionary<string, int>();

            await Parallel.ForEachAsync(
                cases,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (item, ct) => await RunOneAsync(item.File, item.Name, failuresByKind));

            var (passed, failed, total) = _sink.Counts;
            _output.WriteLine($"consume-engine: {passed}/{total} passed, {failed} failed");
            foreach (var kv in failuresByKind.OrderByDescending(k => k.Value))
                _output.WriteLine($"   [{kv.Key}] x{kv.Value}");

            Assert.True(failed == 0,
                $"consume-engine: {failed} of {total} cases failed ({passed} passed) - failure kinds: " +
                string.Join(", ", failuresByKind.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));
        }

        private async Task RunOneAsync(string file, string testName, ConcurrentDictionary<string, int> failuresByKind)
        {
            var start = DateTimeOffset.UtcNow;
            var caseId = $"{RelativePath(file)}::{testName}";

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
                    result = await _driver.RunAsync(test, payloads);
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
            _sink.Record(caseId, testName, result.Success, details, start, end);
            if (!result.Success)
                failuresByKind.AddOrUpdate(result.Kind, 1, (_, n) => n + 1);
        }

        private static string RelativePath(string file)
        {
            var root = FixtureCatalog.RepoRoot;
            if (root == null) return file;
            return Path.GetRelativePath(root, file).Replace('\\', '/');
        }
    }
}

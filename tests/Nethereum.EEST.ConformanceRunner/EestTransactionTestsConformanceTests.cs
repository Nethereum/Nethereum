using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EestTransactionTestsCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestTransactionTests";
    }

    [Collection(EestTransactionTestsCollection.Name)]
    public class EestTransactionTestsConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly EestTransactionTestsDriver _driver = EestTransactionTestsDriver.Instance;

        private static readonly ConcurrentDictionary<string, System.Collections.Generic.List<TransactionTestLoader.TransactionTest>> Cache = new();

        public EestTransactionTestsConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(_driver);
        }

        public static TheoryData<string, string> Cases() => FixtureCatalog.TransactionCases();

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Fixture(string file, string testName)
        {
            if (file == FixtureCatalog.MissingCacheSentinel)
            {
                Assert.Fail(
                    $"EEST transaction_tests fixture cache not found at '{testName}'. Follow " +
                    "external/README.md to download execution-spec-tests fixtures before running this gate.");
                return;
            }

            if (file == FixtureCatalog.NoFixturesSentinel)
            {
                Assert.Fail(
                    $"EEST transaction_tests cache at '{testName}' exists but contains zero *.json " +
                    "files - a category discovering 0 files must fail loudly, never be skipped.");
                return;
            }

            var start = DateTimeOffset.UtcNow;
            var caseId = $"{RelativePath(file)}::{testName}";

            var tests = Cache.GetOrAdd(file, TransactionTestLoader.LoadFromFile);

            TransactionTestLoader.TransactionTest test = null;
            foreach (var candidate in tests)
            {
                if (candidate.Name == testName) { test = candidate; break; }
            }

            if (test == null)
            {
                var end0 = DateTimeOffset.UtcNow;
                _sink.Record(caseId, testName, false, $"test '{testName}' not found in {file} on re-parse", start, end0);
                Assert.Fail($"Test '{testName}' not found in {file} on re-parse.");
                return;
            }

            ConformanceCaseResult result;
            try
            {
                result = await _driver.RunAsync(test);
            }
            catch (Exception ex)
            {
                result = ConformanceCaseResult.Fail("harnessException", $"{ex.GetType().Name}: {ex.Message}");
            }

            var end = DateTimeOffset.UtcNow;
            var details = result.Success ? "" : $"[{result.Kind}] {result.Detail}";
            _sink.Record(caseId, testName, result.Success, details, start, end);

            Assert.True(result.Success, details);
        }

        private static string RelativePath(string file)
        {
            var root = FixtureCatalog.RepoRoot;
            if (root == null) return file;
            return Path.GetRelativePath(root, file).Replace('\\', '/');
        }
    }
}

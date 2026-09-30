using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EestStateTestsConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestStateTestsConformance";
    }

    [Collection(EestStateTestsConformanceCollection.Name)]
    public class EestStateTestsConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly EestStateTestsDriver _driver = EestStateTestsDriver.Instance;

        public EestStateTestsConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(_driver);
        }

        public static TheoryData<string, string, string, int, int, int> Cases() => FixtureCatalog.AllStateTestCases();

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Fixture(string file, string testName, string fork, int dataIndex, int gasIndex, int valueIndex)
        {
            if (file == FixtureCatalog.MissingCacheSentinel)
            {
                Assert.Fail(
                    $"EEST state_tests cache not found at '{testName}'. Follow external/README.md to " +
                    "download execution-spec-tests fixtures before running this gate.");
                return;
            }

            if (file == FixtureCatalog.NoFixturesSentinel)
            {
                Assert.Fail(
                    $"EEST state_tests cache at '{testName}' exists but contains zero state_tests " +
                    "*.json files - a category discovering 0 files must fail loudly, never be skipped.");
                return;
            }

            var start = DateTimeOffset.UtcNow;
            var caseName = $"{testName}[{fork}:d{dataIndex},g{gasIndex},v{valueIndex}]";
            var caseId = $"{RelativePath(file)}::{caseName}";

            var tests = await StateTestFileCache.LoadAsync(file);

            if (!tests.TryGetValue(testName, out var test) || test == null)
            {
                var end0 = DateTimeOffset.UtcNow;
                _sink.Record(caseId, caseName, false, $"test '{testName}' not found in {file} on re-parse", start, end0);
                Assert.Fail($"Test '{testName}' not found in {file} on re-parse.");
                return;
            }

            ConformanceCaseResult result;
            try
            {
                result = await _driver.RunAsync(test, fork, dataIndex, gasIndex, valueIndex);
            }
            catch (Exception ex)
            {
                result = ConformanceCaseResult.Fail("harnessException", $"{ex.GetType().Name}: {ex.Message}");
            }

            var end = DateTimeOffset.UtcNow;
            var details = result.Success ? "" : $"[{result.Kind}] {result.Detail}";
            _sink.Record(caseId, caseName, result.Success, details, start, end);

            Assert.True(result.Success, details);
        }

        private static string RelativePath(string file)
        {
            var root = FixtureCatalog.RepoRoot;
            if (root == null) return file;
            return Path.GetRelativePath(root, file).Replace('\\', '/');
        }
    }

    public class StateTestFixtureCacheSanityTests
    {
        [Fact]
        public void StateTestsCacheIsPresent()
        {
            Assert.True(FixtureCatalog.StateTestsCacheIsPresent,
                $"EEST state_tests cache missing at {FixtureCatalog.StateTestsRoot}. See external/README.md.");
        }

        [Fact]
        public void StateTestsCacheHasAtLeastOneFile()
        {
            Assert.True(FixtureCatalog.StateTestsCacheIsPresent, $"cache missing at {FixtureCatalog.StateTestsRoot}");
            Assert.NotEmpty(FixtureCatalog.AllStateTestFiles);
        }

        [Fact]
        public void EveryStateTestTopLevelCategoryHasAtLeastOneFile()
        {
            Assert.True(FixtureCatalog.StateTestsCacheIsPresent, $"cache missing at {FixtureCatalog.StateTestsRoot}");

            foreach (var category in FixtureCatalog.StateTestTopLevelCategories())
            {
                var count = Directory.GetFiles(category, "*.json", SearchOption.AllDirectories).Length;
                Assert.True(count > 0, $"category '{category}' discovered 0 fixture files - never silently skip.");
            }
        }
    }
}

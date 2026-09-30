using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EestConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestConformance";
    }

    [Collection(EestConformanceCollection.Name)]
    public class EestConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly EestBlockchainTestsRlpDriver _driver = EestBlockchainTestsRlpDriver.Instance;

        public EestConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(_driver);
        }

        public static TheoryData<string, string> Cases() => FixtureCatalog.AllCases();

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Fixture(string file, string testName)
        {
            if (file == FixtureCatalog.MissingCacheSentinel)
            {
                Assert.Fail(
                    $"EEST fixture cache not found at '{testName}'. Follow external/README.md to " +
                    "download execution-spec-tests fixtures before running this gate.");
                return;
            }

            if (file == FixtureCatalog.NoFixturesSentinel)
            {
                Assert.Fail(
                    $"EEST fixture cache at '{testName}' exists but contains zero blockchain_tests " +
                    "*.json files - a category discovering 0 files must fail loudly, never be skipped.");
                return;
            }

            var start = DateTimeOffset.UtcNow;
            var caseId = $"{RelativePath(file)}::{testName}";

            var tests = await FixtureFileCache.LoadAsync(file);

            BlockchainTestLoader.BlockchainTest? test = null;
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

    public class FixtureCacheSanityTests
    {
        [Fact]
        public void FixtureCacheIsPresent()
        {
            Assert.True(FixtureCatalog.CacheIsPresent,
                $"EEST fixture cache missing at {FixtureCatalog.BlockchainTestsRoot}. See external/README.md.");
        }

        [Fact]
        public void FixtureCacheHasAtLeastOneFile()
        {
            Assert.True(FixtureCatalog.CacheIsPresent, $"cache missing at {FixtureCatalog.BlockchainTestsRoot}");
            Assert.NotEmpty(FixtureCatalog.AllFixtureFiles);
        }

        [Fact]
        public void EveryTopLevelCategoryHasAtLeastOneFile()
        {
            Assert.True(FixtureCatalog.CacheIsPresent, $"cache missing at {FixtureCatalog.BlockchainTestsRoot}");

            foreach (var category in FixtureCatalog.TopLevelCategories())
            {
                var count = Directory.GetFiles(category, "*.json", SearchOption.AllDirectories).Length;
                Assert.True(count > 0, $"category '{category}' discovered 0 fixture files - never silently skip.");
            }
        }
    }
}

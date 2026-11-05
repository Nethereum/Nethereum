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
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "eest-blockchain")]
    public class EestConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly EestBlockchainTestsRlpDriver _driver = EestBlockchainTestsRlpDriver.Instance;

        public EestConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(_driver);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.Blockchain);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.Blockchain, "EEST blockchain_tests cache", "EestStable", caseId, _sink);
    }

    [Trait("Requires", "Externals")]
    public class FixtureCacheSanityTests
    {
        [Fact]
        public void FixtureCacheIsPresent()
        {
            Assert.True(Corpora.Blockchain.IsPresent,
                $"EEST fixture cache missing at {Corpora.Blockchain.Root}. Set ExternalPaths:EestStable (see external/README.md).");
        }

        [Fact]
        public void FixtureCacheHasAtLeastOneFile()
        {
            Assert.True(Corpora.Blockchain.IsPresent, $"cache missing at {Corpora.Blockchain.Root}");
            Assert.NotEmpty(Corpora.Blockchain.Files);
        }

        [Fact]
        public void EveryTopLevelCategoryHasAtLeastOneFile()
        {
            Assert.True(Corpora.Blockchain.IsPresent, $"cache missing at {Corpora.Blockchain.Root}");

            foreach (var category in Directory.GetDirectories(Corpora.Blockchain.Root))
            {
                var count = Directory.GetFiles(category, "*.json", SearchOption.AllDirectories).Length;
                Assert.True(count > 0, $"category '{category}' discovered 0 fixture files - never silently skip.");
            }
        }
    }
}

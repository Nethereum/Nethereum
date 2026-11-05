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
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "eest-state")]
    public class EestStateTestsConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly EestStateTestsDriver _driver = EestStateTestsDriver.Instance;

        public EestStateTestsConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(_driver);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.State);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            StateCaseTheory.RunAsync(Corpora.State, "EEST state_tests cache", "EestStable", caseId, _sink);
    }

    [Trait("Requires", "Externals")]
    public class StateTestFixtureCacheSanityTests
    {
        [Fact]
        public void StateTestsCacheIsPresent()
        {
            Assert.True(Corpora.State.IsPresent,
                $"EEST state_tests cache missing at {Corpora.State.Root}. Set ExternalPaths:EestStable (see external/README.md).");
        }

        [Fact]
        public void StateTestsCacheHasAtLeastOneFile()
        {
            Assert.True(Corpora.State.IsPresent, $"cache missing at {Corpora.State.Root}");
            Assert.NotEmpty(Corpora.State.Files);
        }

        [Fact]
        public void EveryStateTestTopLevelCategoryHasAtLeastOneFile()
        {
            Assert.True(Corpora.State.IsPresent, $"cache missing at {Corpora.State.Root}");

            foreach (var category in Directory.GetDirectories(Corpora.State.Root))
            {
                var count = Directory.GetFiles(category, "*.json", SearchOption.AllDirectories).Length;
                Assert.True(count > 0, $"category '{category}' discovered 0 fixture files - never silently skip.");
            }
        }
    }
}

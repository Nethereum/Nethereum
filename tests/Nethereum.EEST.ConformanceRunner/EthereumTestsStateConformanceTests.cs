using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EthereumTestsStateConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EthereumTestsStateConformance";
    }

    [Collection(EthereumTestsStateConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "ethereum-tests")]
    public class EthereumTestsStateConformanceTests
    {
        public const string SuiteId = "ethereum-tests-state-tests";
        public const string SuiteName = "Nethereum ethereum/tests GeneralStateTests - single-transaction state transition";

        private readonly HiveResultsSink _sink;

        public EthereumTestsStateConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.EthereumTestsState);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            StateCaseTheory.RunAsync(Corpora.EthereumTestsState, "ethereum/tests GeneralStateTests", "EthereumTests", caseId, _sink);
    }
}

using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EthereumTestsBlockchainConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EthereumTestsBlockchainConformance";
    }

    [Collection(EthereumTestsBlockchainConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "ethereum-tests")]
    public class EthereumTestsBlockchainConformanceTests
    {
        private readonly HiveResultsSink _sink;

        public EthereumTestsBlockchainConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(EestBlockchainTestsRlpDriver.Instance);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.EthereumTestsBlockchain);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.EthereumTestsBlockchain, "ethereum/tests BlockchainTests", "EthereumTests", caseId, _sink);
    }
}

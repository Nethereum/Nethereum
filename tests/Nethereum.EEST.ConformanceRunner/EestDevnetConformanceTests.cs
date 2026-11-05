using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EestAmsterdamBlockchainConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestAmsterdamBlockchainConformance";
    }

    [Collection(EestAmsterdamBlockchainConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    public class EestAmsterdamBlockchainConformanceTests
    {
        public const string SuiteId = "eest-amsterdam-blockchain-tests-rlp";
        public const string SuiteName = "Nethereum EEST glamsterdam devnet blockchain_tests - consume-rlp (full node, RLP import)";

        private readonly HiveResultsSink _sink;

        public EestAmsterdamBlockchainConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.AmsterdamBlockchain);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.AmsterdamBlockchain, "EEST Amsterdam blockchain_tests", "EestAmsterdam", caseId, _sink);
    }

    [CollectionDefinition(Name)]
    public sealed class EestAmsterdamEngineConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestAmsterdamEngineConformance";
    }

    [Collection(EestAmsterdamEngineConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    public class EestAmsterdamEngineConformanceTests
    {
        public const string SuiteId = "eest-amsterdam-blockchain-tests-engine";
        public const string SuiteName = "Nethereum EEST glamsterdam devnet blockchain_tests_engine - consume-engine";

        private readonly HiveResultsSink _sink;
        private readonly ITestOutputHelper _output;

        public EestAmsterdamEngineConformanceTests(HiveResultsSink sink, ITestOutputHelper output)
        {
            _sink = sink;
            _output = output;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        [Fact]
        public Task AllEngineFixtures() =>
            EngineCorpusRun.RunAllAsync(Corpora.AmsterdamBlockchainEngine, "EestAmsterdam", _sink, _output);
    }

    [CollectionDefinition(Name)]
    public sealed class EestFusakaDevnet5BlockchainConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestFusakaDevnet5BlockchainConformance";
    }

    [Collection(EestFusakaDevnet5BlockchainConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    public class EestFusakaDevnet5BlockchainConformanceTests
    {
        public const string SuiteId = "eest-fusaka-devnet-5-blockchain-tests-rlp";
        public const string SuiteName = "Nethereum EEST fusaka-devnet-5 blockchain_tests (EIP-7918) - consume-rlp";

        private readonly HiveResultsSink _sink;

        public EestFusakaDevnet5BlockchainConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.FusakaDevnet5Blockchain);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.FusakaDevnet5Blockchain, "EEST fusaka-devnet-5 blockchain_tests", "EestFusakaDevnet5", caseId, _sink);
    }
}

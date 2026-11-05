using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class LegacyBlockchainConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "LegacyBlockchainConformance";
    }

    [Collection(LegacyBlockchainConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    public class LegacyBlockchainConformanceTests
    {
        private readonly HiveResultsSink _sink;

        public LegacyBlockchainConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(EestBlockchainTestsRlpDriver.Instance);
        }

        public static TheoryData<string> ConstantinopleInvalid() => Corpora.TheoryCases(Corpora.LegacyConstantinopleInvalidBlocks);

        public static TheoryData<string> ConstantinopleValid() => Corpora.TheoryCases(Corpora.LegacyConstantinopleValidBlocks);

        public static TheoryData<string> CancunInvalid() => Corpora.TheoryCases(Corpora.LegacyCancunInvalidBlocks);

        public static TheoryData<string> CancunValid() => Corpora.TheoryCases(Corpora.LegacyCancunValidBlocks);

        [Theory]
        [Trait("Corpus", "legacy-constantinople")]
        [MemberData(nameof(ConstantinopleInvalid))]
        public Task ConstantinopleInvalidBlocks(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.LegacyConstantinopleInvalidBlocks, "legacytests Constantinople BlockchainTests/InvalidBlocks", "LegacyTests", caseId, _sink);

        [Theory]
        [Trait("Corpus", "legacy-constantinople")]
        [MemberData(nameof(ConstantinopleValid))]
        public Task ConstantinopleValidBlocks(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.LegacyConstantinopleValidBlocks, "legacytests Constantinople BlockchainTests/ValidBlocks", "LegacyTests", caseId, _sink);

        [Theory]
        [Trait("Corpus", "legacy-cancun")]
        [MemberData(nameof(CancunInvalid))]
        public Task CancunInvalidBlocks(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.LegacyCancunInvalidBlocks, "legacytests Cancun BlockchainTests/InvalidBlocks", "LegacyTests", caseId, _sink);

        [Theory]
        [Trait("Corpus", "legacy-cancun")]
        [MemberData(nameof(CancunValid))]
        public Task CancunValidBlocks(string caseId) =>
            BlockchainCaseTheory.RunAsync(Corpora.LegacyCancunValidBlocks, "legacytests Cancun BlockchainTests/ValidBlocks", "LegacyTests", caseId, _sink);
    }
}

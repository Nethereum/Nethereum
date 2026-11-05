using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class LegacyCancunStateConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "LegacyCancunStateConformance";
    }

    [Collection(LegacyCancunStateConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "legacy-cancun")]
    public class LegacyCancunStateConformanceTests
    {
        public const string SuiteId = "legacy-cancun-state-tests";
        public const string SuiteName = "Nethereum ethereum/legacytests Cancun GeneralStateTests - Istanbul to Cancun";

        private readonly HiveResultsSink _sink;

        public LegacyCancunStateConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.LegacyCancunState);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            StateCaseTheory.RunAsync(Corpora.LegacyCancunState, "legacytests Cancun GeneralStateTests", "LegacyTests", caseId, _sink);
    }

    [CollectionDefinition(Name)]
    public sealed class LegacyConstantinopleStateConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "LegacyConstantinopleStateConformance";
    }

    [Collection(LegacyConstantinopleStateConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "legacy-constantinople")]
    public class LegacyConstantinopleStateConformanceTests
    {
        public const string SuiteId = "legacy-constantinople-state-tests";
        public const string SuiteName = "Nethereum ethereum/legacytests Constantinople GeneralStateTests - Frontier to ConstantinopleFix";

        private readonly HiveResultsSink _sink;

        public LegacyConstantinopleStateConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.LegacyConstantinopleState);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            StateCaseTheory.RunAsync(Corpora.LegacyConstantinopleState, "legacytests Constantinople GeneralStateTests", "LegacyTests", caseId, _sink);
    }
}

using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class EestAmsterdamStateConformanceCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "EestAmsterdamStateConformance";
    }

    [Collection(EestAmsterdamStateConformanceCollection.Name)]
    [Trait("Requires", "Externals")]
    public class EestAmsterdamStateConformanceTests
    {
        public const string SuiteId = "eest-amsterdam-state-tests";
        public const string SuiteName = "Nethereum EEST glamsterdam devnet state_tests - single-transaction state transition";

        private readonly HiveResultsSink _sink;

        public EestAmsterdamStateConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(SuiteId, SuiteName);
        }

        public static TheoryData<string> Cases() => Corpora.TheoryCases(Corpora.AmsterdamState);

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Fixture(string caseId) =>
            StateCaseTheory.RunAsync(Corpora.AmsterdamState, "EEST Amsterdam state_tests", "EestAmsterdam", caseId, _sink);
    }
}

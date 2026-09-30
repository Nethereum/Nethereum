using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class FrontierHomesteadHostBlockchainTests
    {
        private readonly HostBlockchainTestRunner _runner;

        public FrontierHomesteadHostBlockchainTests(ITestOutputHelper output)
        {
            _runner = new HostBlockchainTestRunner(output);
        }

        [Fact]
        [Trait("Category", "FrontierHomesteadHostBlockchainTests")]
        public async Task Frontier_validation_test_transaction()
        {
            await _runner.RunCategoryAsync(FrontierValidationFixtureCorpus.Path, "Frontier");
        }

        [Fact]
        [Trait("Category", "FrontierHomesteadHostBlockchainTests")]
        public async Task Homestead_validation_test_transaction()
        {
            await _runner.RunCategoryAsync(FrontierValidationFixtureCorpus.Path, "Homestead");
        }
    }
}

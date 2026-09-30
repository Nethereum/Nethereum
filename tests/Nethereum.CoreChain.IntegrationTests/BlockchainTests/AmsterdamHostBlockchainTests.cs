using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class AmsterdamHostBlockchainTests
    {
        private readonly HostBlockchainTestRunner _runner;

        public AmsterdamHostBlockchainTests(ITestOutputHelper output)
        {
            _runner = new HostBlockchainTestRunner(output);
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip2780_reduce_intrinsic_tx_gas()
        {
            var path = AmsterdamFixtureCorpus.Category("eip2780_reduce_intrinsic_tx_gas");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7708_eth_transfer_logs()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7708_eth_transfer_logs");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7778_block_gas_accounting_without_refunds()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7778_block_gas_accounting_without_refunds");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7843_slotnum()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7843_slotnum");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7928_block_level_access_lists()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7928_block_level_access_lists");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7954_increase_max_contract_size()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7954_increase_max_contract_size");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7976_increase_calldata_floor_cost()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7976_increase_calldata_floor_cost");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7981_increase_access_list_cost()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7981_increase_access_list_cost");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip7997_deterministic_factory_predeploy()
        {
            var path = AmsterdamFixtureCorpus.Category("eip7997_deterministic_factory_predeploy");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip8024_dupn_swapn_exchange()
        {
            var path = AmsterdamFixtureCorpus.Category("eip8024_dupn_swapn_exchange");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip8037_state_creation_gas_cost_increase()
        {
            var path = AmsterdamFixtureCorpus.Category("eip8037_state_creation_gas_cost_increase");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip8038_state_access_gas_cost_increase()
        {
            var path = AmsterdamFixtureCorpus.Category("eip8038_state_access_gas_cost_increase");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip8246_selfdestruct_no_burn()
        {
            var path = AmsterdamFixtureCorpus.Category("eip8246_selfdestruct_no_burn");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Amsterdam_eip8282_builder_execution_requests()
        {
            var path = AmsterdamFixtureCorpus.Category("eip8282_builder_execution_requests");
            await _runner.RunCategoryAsync(path, "Amsterdam");
        }
    }
}

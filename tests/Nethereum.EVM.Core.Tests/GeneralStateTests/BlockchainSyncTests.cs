using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BlockchainSyncTests
    {
        private readonly ITestOutputHelper _output;
        private readonly BlockchainSyncTestRunner _runner;
        private readonly string _testVectorsPath;
        private readonly string _amsterdamTestVectorsPath;

        public BlockchainSyncTests(ITestOutputHelper output)
        {
            _output = output;
            _runner = new BlockchainSyncTestRunner(output);
            _testVectorsPath = GetBlockchainTestsPath();
            _amsterdamTestVectorsPath = GetAmsterdamBlockchainTestsPath();
        }


        [Fact]
        public void ValidBlocks_bcExample()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcExample");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcValidBlockTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcValidBlockTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcStateTests()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcStateTests");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcEIP1559()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcEIP1559");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcBlockGasLimitTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcBlockGasLimitTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcGasPricerTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcGasPricerTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcWalletTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcWalletTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcEIP1153()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcEIP1153-transientStorage");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcEIP3675()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcEIP3675");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcExploitTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcExploitTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcForkStressTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcForkStressTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcRandomBlockhashTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcRandomBlockhashTest");
            _runner.RunCategory(path);
        }


        [Fact]
        public void Prague_bcExample()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcExample");
            _runner.RunCategory(path, "Prague");
        }

        [Fact]
        public void Prague_bcValidBlockTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcValidBlockTest");
            _runner.RunCategory(path, "Prague");
        }

        [Fact]
        public void Prague_bcStateTests()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcStateTests");
            _runner.RunCategory(path, "Prague");
        }


        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bc4895_withdrawals()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bc4895-withdrawals");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcBlockGasLimitTest()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcBlockGasLimitTest");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcEIP1559()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcEIP1559");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcEIP3675()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcEIP3675");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcInvalidHeaderTest()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcInvalidHeaderTest");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcMultiChainTest()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcMultiChainTest");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcStateTests()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcStateTests");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcUncleHeaderValidity()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcUncleHeaderValidity");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcUncleSpecialTests()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcUncleSpecialTests");
            _runner.RunCategory(path);
        }

        [Fact]
        [Trait("Category", "InvalidBlocks")]
        public void InvalidBlocks_bcUncleTest()
        {
            var path = Path.Combine(_testVectorsPath, "InvalidBlocks", "bcUncleTest");
            _runner.RunCategory(path);
        }


        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip2780_reduce_intrinsic_tx_gas()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip2780_reduce_intrinsic_tx_gas");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7708_eth_transfer_logs()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7708_eth_transfer_logs");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7778_block_gas_accounting_without_refunds()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7778_block_gas_accounting_without_refunds");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7843_slotnum()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7843_slotnum");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7928_block_level_access_lists()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7928_block_level_access_lists");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7954_increase_max_contract_size()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7954_increase_max_contract_size");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7976_increase_calldata_floor_cost()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7976_increase_calldata_floor_cost");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7981_increase_access_list_cost()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7981_increase_access_list_cost");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip7997_deterministic_factory_predeploy()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip7997_deterministic_factory_predeploy");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip8024_dupn_swapn_exchange()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip8024_dupn_swapn_exchange");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip8037_state_creation_gas_cost_increase()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip8037_state_creation_gas_cost_increase");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip8038_state_access_gas_cost_increase()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip8038_state_access_gas_cost_increase");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip8246_selfdestruct_no_burn()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip8246_selfdestruct_no_burn");
            _runner.RunCategory(path, "Amsterdam");
        }

        [Fact]
        [Trait("Category", "AmsterdamBlockchainTests")]
        public void Amsterdam_eip8282_builder_execution_requests()
        {
            var path = Path.Combine(_amsterdamTestVectorsPath ?? "", "eip8282_builder_execution_requests");
            _runner.RunCategory(path, "Amsterdam");
        }

        private static string GetBlockchainTestsPath()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var path = Path.Combine(dir.FullName, "external", "ethereum-tests", "BlockchainTests");
                    if (Directory.Exists(path)) return path;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private static string GetAmsterdamBlockchainTestsPath()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var path = Path.Combine(dir.FullName, "external", "execution-spec-tests", "fixtures", "blockchain_tests", "amsterdam");
                    if (Directory.Exists(path)) return path;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}

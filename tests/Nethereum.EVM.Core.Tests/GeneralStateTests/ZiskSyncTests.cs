using Xunit;
using Xunit.Abstractions;
using Nethereum.Testing;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class ZiskSyncTests
    {
        private readonly ITestOutputHelper _output;
        private readonly ZiskSyncStateTestRunner _runner;

        public ZiskSyncTests(ITestOutputHelper output)
        {
            _output = output;
            _runner = new ZiskSyncStateTestRunner(output);
        }


        [Fact]
        public void DirectSync_stExample()
            => _runner.RunCategoryDirect("stExample");

        [Fact]
        public void DirectSync_stArgsZeroOneBalance()
            => _runner.RunCategoryDirect("stArgsZeroOneBalance");

        [Fact]
        public void DirectSync_stSStoreTest()
            => _runner.RunCategoryDirect("stSStoreTest");

        [Fact]
        public void DirectSync_stPreCompiledContracts()
            => _runner.RunCategoryDirect("stPreCompiledContracts");

        [Fact]
        public void DirectSync_stShift()
            => _runner.RunCategoryDirect("stShift");

        [Fact]
        public void DirectSync_stMemoryTest()
            => _runner.RunCategoryDirect("stMemoryTest");

        [Fact]
        public void DirectSync_stCallCodes()
            => _runner.RunCategoryDirect("stCallCodes");

        [Fact]
        public void DirectSync_stEIP150Specific()
            => _runner.RunCategoryDirect("stEIP150Specific");


        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stExample()
            => _runner.RunCategoryZiskEmu("stExample", maxTests: 5);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stArgsZeroOneBalance()
            => _runner.RunCategoryZiskEmu("stArgsZeroOneBalance", maxTests: 3);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stSStoreTest()
            => _runner.RunCategoryZiskEmu("stSStoreTest", maxTests: 3);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stPreCompiledContracts()
            => _runner.RunCategoryZiskEmu("stPreCompiledContracts", maxTests: 100);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stPreCompiledContracts2()
            => _runner.RunCategoryZiskEmu("stPreCompiledContracts2", maxTests: 100);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stShift()
            => _runner.RunCategoryZiskEmu("stShift", maxTests: 3);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stCallCodes()
            => _runner.RunCategoryZiskEmu("stCallCodes", maxTests: 3);


        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip2537_Bls12_381()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/prague/eip2537_bls_12_381_precompiles"),
                maxTests: 50, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip4844_KzgPointEvaluation()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/cancun/eip4844_blobs"),
                maxTests: 10, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Osaka_Eip7951_P256Verify()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_osaka/osaka/eip7951_p256verify_precompiles"),
                maxTests: 20, hardfork: "Osaka");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip196_Bn254_AddMul_InvalidInputs()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/byzantium/eip196_ec_add_mul"),
                maxTests: 50, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip197_Bn254_Pairing_InvalidInputs()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/byzantium/eip197_ec_pairing"),
                maxTests: 50, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip152_Blake2f_InvalidInputs()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/istanbul/eip152_blake2"),
                maxTests: 50, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip4844_PointEvaluation_InvalidInputs()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/cancun/eip4844_blobs/point_evaluation_precompile"),
                maxTests: 50, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Prague_Eip198_ModExp_Bounds()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_prague/byzantium/eip198_modexp_precompile"),
                maxTests: 50, hardfork: "Prague");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_Osaka_Eip7823_ModExp_UpperBounds()
            => _runner.RunCategoryZiskEmu(
                GetExecutionSpecTestPath("state_tests/for_osaka/osaka/eip7823_modexp_upper_bounds"),
                maxTests: 50, hardfork: "Osaka");

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stBadOpcode_UndefinedBytesHaltWithoutCrashing()
            => _runner.RunCategoryZiskEmu("stBadOpcode", maxTests: 50);

        [Fact]
        [Trait("Requires", "Externals")]
        [Trait("Category", "Zisk")]
        public void ZiskEmu_stStackTests_OverflowAndUnderflowHaltWithoutCrashing()
            => _runner.RunCategoryZiskEmu("stStackTests", maxTests: 50);

        private static string GetExecutionSpecTestPath(string relative)
        {
            return System.IO.Path.Combine(TestEnvironment.RequireExternalPath("EestStable"), relative);
        }


        [Fact]
        public void WitnessSync_stExample()
            => _runner.RunCategoryWitness("stExample");

        [Fact]
        public void WitnessSync_stArgsZeroOneBalance()
            => _runner.RunCategoryWitness("stArgsZeroOneBalance");

        [Fact]
        public void WitnessSync_stSStoreTest()
            => _runner.RunCategoryWitness("stSStoreTest");

        [Fact]
        public void WitnessSync_stPreCompiledContracts()
            => _runner.RunCategoryWitness("stPreCompiledContracts");

        [Fact]
        public void WitnessSync_stShift()
            => _runner.RunCategoryWitness("stShift");

        [Fact]
        public void WitnessSync_stMemoryTest()
            => _runner.RunCategoryWitness("stMemoryTest");

        [Fact]
        public void WitnessSync_stCallCodes()
            => _runner.RunCategoryWitness("stCallCodes");

        [Fact]
        public void WitnessSync_stEIP150Specific()
            => _runner.RunCategoryWitness("stEIP150Specific");

        [Fact]
        public void GenerateWitnesses_stExample()
        {
            var outputDir = GetWitnessOutputDir();
            var count = _runner.GenerateWitnesses("stExample", outputDir);
            Assert.True(count > 0, "No witnesses generated");
        }

        [Fact]
        public void GenerateWitnesses_stSStoreTest()
        {
            var outputDir = GetWitnessOutputDir();
            var count = _runner.GenerateWitnesses("stSStoreTest", outputDir, maxTests: 10);
            Assert.True(count > 0);
        }

        [Fact]
        public void GenerateWitnesses_stCallCodes()
        {
            var outputDir = GetWitnessOutputDir();
            var count = _runner.GenerateWitnesses("stCallCodes", outputDir, maxTests: 10);
            Assert.True(count > 0);
        }

        [Fact]
        public void GenerateWitnesses_stCreateTest()
        {
            var outputDir = GetWitnessOutputDir();
            var count = _runner.GenerateWitnesses("stCreateTest", outputDir, maxTests: 10);
            Assert.True(count > 0);
        }

        [Fact]
        public void GenerateWitnesses_stPreCompiledContracts()
        {
            var outputDir = GetWitnessOutputDir();
            var count = _runner.GenerateWitnesses("stPreCompiledContracts", outputDir, maxTests: 10);
            Assert.True(count > 0);
        }

        private static string GetWitnessOutputDir()
        {
            return System.IO.Path.Combine(TestEnvironment.ExternalPath("ZiskOutput"), "witnesses");
        }
    }
}

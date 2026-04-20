using Xunit;
using Xunit.Abstractions;

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

        // === Direct Sync: JSON → state → sync Execute (no witness) ===
        // Isolates sync EVM correctness from witness format

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

        // === Zisk Emulator: JSON → WitnessData → Serialize → ziskemu via WSL (selected tests) ===
        // Validates full zkVM path — slow, requires ELF binary + WSL

        [Fact]
        public void ZiskEmu_stExample()
            => _runner.RunCategoryZiskEmu("stExample", maxTests: 5);

        [Fact]
        public void ZiskEmu_stArgsZeroOneBalance()
            => _runner.RunCategoryZiskEmu("stArgsZeroOneBalance", maxTests: 3);

        [Fact]
        public void ZiskEmu_stSStoreTest()
            => _runner.RunCategoryZiskEmu("stSStoreTest", maxTests: 3);

        [Fact]
        public void ZiskEmu_stPreCompiledContracts()
            => _runner.RunCategoryZiskEmu("stPreCompiledContracts", maxTests: 100);

        [Fact]
        public void ZiskEmu_stPreCompiledContracts2()
            => _runner.RunCategoryZiskEmu("stPreCompiledContracts2", maxTests: 100);

        [Fact]
        public void ZiskEmu_stShift()
            => _runner.RunCategoryZiskEmu("stShift", maxTests: 3);

        [Fact]
        public void ZiskEmu_stCallCodes()
            => _runner.RunCategoryZiskEmu("stCallCodes", maxTests: 3);

        // === Witness Sync: JSON → WitnessData → Serialize → Deserialize → sync Execute ===
        // Validates witness roundtrip AND sync EVM — same pipeline as Zisk binary

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
        // === Witness Generation: JSON → WitnessData → Serialize → write .bin files ===
        // Generates v3 BinaryBlockWitness files for Zisk emulation/proving

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
            var projectRoot = FindProjectRoot(System.IO.Directory.GetCurrentDirectory());
            return System.IO.Path.Combine(projectRoot, "scripts", "zisk-output", "witnesses");
        }

        private static string FindProjectRoot(string dir)
        {
            while (dir != null)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dir, "Nethereum.slnx")) ||
                    System.IO.File.Exists(System.IO.Path.Combine(dir, "Nethereum.sln")))
                    return dir;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}

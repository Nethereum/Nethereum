using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class ZiskQuickTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ZiskStateTestRunner _runner;

        public ZiskQuickTest(ITestOutputHelper output)
        {
            _output = output;
            _runner = new ZiskStateTestRunner(output);
        }

        // === Native witness roundtrip tests (fast — Windows only) ===
        // Validates: JSON → WitnessData → BinaryWitness → Deserialize → Execute

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stExample()
            => await _runner.RunCategoryNativeAsync("stExample");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stArgsZeroOneBalance()
            => await _runner.RunCategoryNativeAsync("stArgsZeroOneBalance");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stSStoreTest()
            => await _runner.RunCategoryNativeAsync("stSStoreTest");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stPreCompiledContracts()
            => await _runner.RunCategoryNativeAsync("stPreCompiledContracts");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stShift()
            => await _runner.RunCategoryNativeAsync("stShift");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stMemoryTest()
            => await _runner.RunCategoryNativeAsync("stMemoryTest");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stCallCodes()
            => await _runner.RunCategoryNativeAsync("stCallCodes");

        [Fact]
        public async System.Threading.Tasks.Task WitnessNative_stEIP150Specific()
            => await _runner.RunCategoryNativeAsync("stEIP150Specific");

        // === Zisk emulator tests (slow — WSL, selected tests per category) ===
        // Validates: full zkVM execution path

        [Fact]
        public async System.Threading.Tasks.Task Zisk_stExample()
            => await _runner.RunCategoryZiskAsync("stExample", maxTests: 5);

        [Fact]
        public async System.Threading.Tasks.Task Zisk_stSStoreTest()
            => await _runner.RunCategoryZiskAsync("stSStoreTest", maxTests: 3);

        [Fact]
        public async System.Threading.Tasks.Task Zisk_stPreCompiledContracts()
            => await _runner.RunCategoryZiskAsync("stPreCompiledContracts", maxTests: 3);

        [Fact]
        public async System.Threading.Tasks.Task Zisk_stShift()
            => await _runner.RunCategoryZiskAsync("stShift", maxTests: 3);

        [Fact]
        public async System.Threading.Tasks.Task Zisk_stCallCodes()
            => await _runner.RunCategoryZiskAsync("stCallCodes", maxTests: 3);
    }
}

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Drives the full async + recording pipeline against execution-spec state tests.
    /// Each test:
    ///   pre-state (InMemoryStateReader) → WitnessRecordingStateReader (decorator) →
    ///   async TransactionExecutor → recorder.GetWitnessAccounts() → BlockExecutor.ExecuteAsync
    ///   → post-state root must equal reference hash from the test vector.
    /// </summary>
    public class RecordingAsyncStateTests
    {
        private readonly RecordingAsyncStateTestRunner _runner;

        public RecordingAsyncStateTests(ITestOutputHelper output)
        {
            _runner = new RecordingAsyncStateTestRunner(output);
        }

        [Fact]
        public Task RecordingAsync_stExample() => _runner.RunCategoryAsync("stExample");

        [Fact]
        public Task RecordingAsync_stArgsZeroOneBalance() => _runner.RunCategoryAsync("stArgsZeroOneBalance");

        [Fact]
        public Task RecordingAsync_balanceNonConst_only() => _runner.RunSingleTestAsync("stArgsZeroOneBalance", "balanceNonConst");

        [Fact]
        public Task RecordingAsync_precompsEIP2929Cancun_13_only() => _runner.RunSingleTestAsync("stPreCompiledContracts", "precompsEIP2929Cancun");

        [Fact]
        public Task RecordingAsync_stSStoreTest() => _runner.RunCategoryAsync("stSStoreTest");

        [Fact]
        public Task RecordingAsync_stShift() => _runner.RunCategoryAsync("stShift");

        [Fact]
        public Task RecordingAsync_stCallCodes() => _runner.RunCategoryAsync("stCallCodes");

        [Fact]
        public Task RecordingAsync_stEIP150Specific() => _runner.RunCategoryAsync("stEIP150Specific");

        [Fact]
        public Task RecordingAsync_stPreCompiledContracts() => _runner.RunCategoryAsync("stPreCompiledContracts");

        [Fact]
        public Task RecordingAsync_stMemoryTest() => _runner.RunCategoryAsync("stMemoryTest");

        [Fact]
        public Task RecordingAsync_Cancun_Transient() => _runner.RunCategoryAsync("Cancun/stEIP1153-transientStorage", "Cancun");

        [Fact]
        public Task RecordingAsync_Cancun_MCOPY() => _runner.RunCategoryAsync("Cancun/stEIP5656-MCOPY", "Cancun");

        [Fact]
        public Task RecordingAsync_Cancun_Blob() => _runner.RunCategoryAsync("Cancun/stEIP4844-blobtransactions", "Cancun");
    }
}

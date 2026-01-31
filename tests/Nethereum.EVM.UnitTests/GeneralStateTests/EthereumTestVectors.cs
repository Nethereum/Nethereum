using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class EthereumTestVectors
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testVectorsPath;

        public EthereumTestVectors(ITestOutputHelper output)
        {
            _output = output;
            _testVectorsPath = GetTestVectorsPath();
        }

        private static string GetTestVectorsPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var testDir = Path.Combine(currentDir, "Tests", "GeneralStateTests");

            if (Directory.Exists(testDir))
                return testDir;

            var projectRoot = FindProjectRoot(currentDir);
            if (projectRoot != null)
            {
                testDir = Path.Combine(projectRoot, "tests", "Nethereum.EVM.UnitTests", "Tests", "GeneralStateTests");
                if (Directory.Exists(testDir))
                    return testDir;
            }

            return null;
        }

        private static string FindProjectRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        [Fact(Skip = "Run manually - executes all categories")]
        public async Task RunAllGeneralStateTests()
        {
            if (_testVectorsPath == null || !Directory.Exists(_testVectorsPath))
            {
                _output.WriteLine("GeneralStateTests not found. Download instructions:");
                _output.WriteLine("");
                _output.WriteLine("1. Clone the ethereum/tests repository:");
                _output.WriteLine("   git clone https://github.com/ethereum/tests.git --depth 1");
                _output.WriteLine("");
                _output.WriteLine("2. Copy GeneralStateTests folder to:");
                _output.WriteLine("   tests/Nethereum.EVM.UnitTests/Tests/GeneralStateTests/");
                _output.WriteLine("");
                _output.WriteLine("3. Remove the [Skip] attribute and run this test");
                Assert.True(false, "Test vectors not found");
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var categories = Directory.GetDirectories(_testVectorsPath);

            var totalPassed = 0;
            var totalFailed = 0;
            var totalSkipped = 0;
            var failedTests = new System.Collections.Generic.List<string>();

            foreach (var category in categories)
            {
                var categoryName = Path.GetFileName(category);
                var testFiles = Directory.GetFiles(category, "*.json", SearchOption.AllDirectories);

                _output.WriteLine($"Category: {categoryName} ({testFiles.Length} files)");

                foreach (var testFile in testFiles)
                {
                    var result = await runner.RunTestAsync(testFile);

                    totalPassed += result.PassedCount;
                    totalFailed += result.FailedCount;
                    totalSkipped += result.SkippedCount;

                    foreach (var r in result.Results.Where(x => !x.Passed && !x.Skipped))
                    {
                        failedTests.Add($"{categoryName}/{Path.GetFileNameWithoutExtension(testFile)}/{r.TestName}[{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {r.Message}");
                    }
                }
            }

            _output.WriteLine("");
            _output.WriteLine("=== SUMMARY ===");
            _output.WriteLine($"Passed:  {totalPassed}");
            _output.WriteLine($"Failed:  {totalFailed}");
            _output.WriteLine($"Skipped: {totalSkipped}");

            if (failedTests.Any())
            {
                _output.WriteLine("");
                _output.WriteLine("=== FAILED TESTS ===");
                foreach (var f in failedTests.Take(50))
                {
                    _output.WriteLine(f);
                }
                if (failedTests.Count > 50)
                    _output.WriteLine($"... and {failedTests.Count - 50} more");
            }

            Assert.Equal(0, totalFailed);
        }

        private async Task RunCategoryAsync(string categoryName) => await RunCategoryAsync(categoryName, "Prague");

        private async Task RunCategoryAsync(string categoryName, string hardfork)
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var categoryPath = Path.Combine(_testVectorsPath, categoryName);
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category {categoryName} not found at {categoryPath}");
                Assert.True(false, $"Category {categoryName} not found");
                return;
            }

            _output.WriteLine($"Running {categoryName} with hardfork: {hardfork} using TransactionExecutor");
            var runner = new GeneralStateTestRunner(_output, hardfork);
            var gethRunner = new GethEvmRunner();
            var comparer = new TraceComparer();
            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);

            var totalPassed = 0;
            var totalFailed = 0;
            var totalSkipped = 0;
            var failedTests = new System.Collections.Generic.List<string>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                _output.WriteLine($"Running: {fileName}");

                var result = await runner.RunTestWithExecutorAsync(testFile);

                totalPassed += result.PassedCount;
                totalFailed += result.FailedCount;
                totalSkipped += result.SkippedCount;

                foreach (var r in result.Results.Where(x => !x.Passed && !x.Skipped))
                {
                    failedTests.Add($"{fileName}/{r.TestName}[{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {r.Message}");
                    _output.WriteLine($"  FAILED: {fileName} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}]");
                    if (r.AccountDiffs != null)
                    {
                        foreach (var diff in r.AccountDiffs)
                            _output.WriteLine($"    {diff}");
                    }

                    try
                    {
                        _output.WriteLine($"  Running trace comparison with Geth...");

                        var gethResult = await gethRunner.RunStateTestAsync(testFile,
                            r.DataIndex, r.GasIndex, r.ValueIndex, hardfork);

                        if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
                        {
                            _output.WriteLine($"    Geth trace failed: {gethResult.Error ?? "No steps"}");
                            continue;
                        }

                        var nethResult = await runner.RunTestWithTraceAsync(testFile);
                        var nethSingleResult = nethResult.Results.FirstOrDefault(x =>
                            x.DataIndex == r.DataIndex &&
                            x.GasIndex == r.GasIndex &&
                            x.ValueIndex == r.ValueIndex);

                        if (nethSingleResult?.Traces == null || nethSingleResult.Traces.Count == 0)
                        {
                            _output.WriteLine($"    Nethereum trace failed: No traces captured");
                            continue;
                        }

                        var nethSteps = comparer.NormalizeNethTrace(nethSingleResult.Traces);
                        var comparison = comparer.Compare(gethResult.Steps, nethSteps);

                        if (comparison.HasDivergence)
                        {
                            _output.WriteLine($"  TRACE DIVERGENCE at step {comparison.FirstDivergenceStep}:");
                            _output.WriteLine($"    Type: {comparison.DivergenceReason}");

                            var divergeStep = comparison.Steps[comparison.FirstDivergenceStep - 1];
                            _output.WriteLine($"    Geth:  PC={divergeStep.GethPC} Op={divergeStep.GethOp} Gas={divergeStep.GethGas} Cost={divergeStep.GethCost} Depth={divergeStep.GethDepth}");
                            _output.WriteLine($"    Neth:  PC={divergeStep.NethPC} Op={divergeStep.NethOp} Gas={divergeStep.NethGas} Cost={divergeStep.NethCost} Depth={divergeStep.NethDepth}");

                            _output.WriteLine($"  Context (steps {Math.Max(1, comparison.FirstDivergenceStep - 5)} to {Math.Min(comparison.Steps.Count, comparison.FirstDivergenceStep + 5)}):");
                            int start = Math.Max(0, comparison.FirstDivergenceStep - 6);
                            int end = Math.Min(comparison.Steps.Count, comparison.FirstDivergenceStep + 5);
                            for (int i = start; i < end; i++)
                            {
                                var s = comparison.Steps[i];
                                var marker = s.Step == comparison.FirstDivergenceStep ? ">>>" : "   ";
                                _output.WriteLine($"    {marker} Step {s.Step}: PC={s.GethPC}/{s.NethPC} Op={s.GethOp} Gas={s.GethGas}/{s.NethGas} Cost={s.GethCost}/{s.NethCost} {s.DivergenceType}");
                            }
                        }
                        else
                        {
                            _output.WriteLine($"    Traces MATCH ({gethResult.Steps.Count} steps) - divergence is in final state calculation");
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"    Trace comparison error: {ex.Message}");
                    }
                }
            }

            _output.WriteLine("");
            _output.WriteLine($"=== {categoryName} SUMMARY ===");
            _output.WriteLine($"Passed:  {totalPassed}");
            _output.WriteLine($"Failed:  {totalFailed}");
            _output.WriteLine($"Skipped: {totalSkipped}");

            if (failedTests.Any())
            {
                _output.WriteLine("");
                _output.WriteLine("=== FAILED TESTS ===");
                foreach (var f in failedTests)
                {
                    _output.WriteLine(f);
                }
            }

            Assert.Equal(0, totalFailed);
        }

        // ============================================================
        // Debug Test - Single test with snapshot debug output
        // ============================================================

        [Fact]
        public async Task DebugCreate2CollisionSelfdestructedOOG()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCreate2", "create2collisionSelfdestructedOOG.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, "Test file not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestAsync(testFile);

            var failed = 0;
            foreach (var r in result.Results)
            {
                _output.WriteLine($"Test: {r.TestName} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}] - {(r.Passed ? "PASSED" : r.Skipped ? "SKIPPED" : "FAILED")}");
                if (!r.Passed && !r.Skipped)
                {
                    failed++;
                    if (r.AccountDiffs != null)
                    {
                        foreach (var diff in r.AccountDiffs)
                            _output.WriteLine($"  {diff}");
                    }
                }
            }
            Assert.Equal(0, failed);
        }

        [Fact]
        public async Task DebugCreate2RefundEF()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCreateTest", "CREATE2_RefundEF.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, "Test file not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestAsync(testFile);

            foreach (var r in result.Results)
            {
                _output.WriteLine($"Test: {r.TestName} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}] - {(r.Passed ? "PASSED" : r.Skipped ? "SKIPPED" : "FAILED")}");
                if (!r.Passed && !r.Skipped && r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"  {diff}");
                }
            }
        }

        [Fact]
        public async Task DebugCreate2CollisionCode()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCreate2", "create2collisionCode.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, "Test file not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestAsync(testFile);

            foreach (var r in result.Results)
            {
                _output.WriteLine($"Test: {r.TestName} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}] - {(r.Passed ? "PASSED" : r.Skipped ? "SKIPPED" : "FAILED")}");
                if (!r.Passed && !r.Skipped && r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"  {diff}");
                }
            }
        }

        /// <summary>
        /// Tests the TransactionExecutor against a simple category (stChainId)
        /// to verify the new implementation produces the same results as the original.
        /// </summary>
        [Fact]
        public async Task TransactionExecutor_stChainId_MatchesOriginal()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var categoryPath = Path.Combine(_testVectorsPath, "stChainId");
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category stChainId not found at {categoryPath}");
                Assert.True(false, "Category stChainId not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);

            var mismatches = new System.Collections.Generic.List<string>();
            var executorPassed = 0;
            var executorFailed = 0;

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);
                _output.WriteLine($"Testing: {fileName}");

                var originalResult = await runner.RunTestAsync(testFile);
                var executorResult = await runner.RunTestWithExecutorAsync(testFile);

                for (int i = 0; i < originalResult.Results.Count && i < executorResult.Results.Count; i++)
                {
                    var orig = originalResult.Results[i];
                    var exec = executorResult.Results[i];

                    if (exec.Passed) executorPassed++;
                    else if (!exec.Skipped) executorFailed++;

                    if (orig.Passed != exec.Passed && !orig.Skipped && !exec.Skipped)
                    {
                        mismatches.Add($"{fileName}[{orig.DataIndex}]: Original={orig.Passed}, Executor={exec.Passed}");
                        _output.WriteLine($"  MISMATCH [{orig.DataIndex}]: Original={orig.Passed}, Executor={exec.Passed}");
                        if (exec.Message != null)
                            _output.WriteLine($"    Executor message: {exec.Message}");
                    }
                    else if (orig.ExpectedStateRoot != exec.ExpectedStateRoot || orig.ActualStateRoot != exec.ActualStateRoot)
                    {
                        if (orig.ActualStateRoot != exec.ActualStateRoot)
                        {
                            mismatches.Add($"{fileName}[{orig.DataIndex}]: StateRoot mismatch - Original={orig.ActualStateRoot}, Executor={exec.ActualStateRoot}");
                        }
                    }
                }
            }

            _output.WriteLine($"\n=== TransactionExecutor Results ===");
            _output.WriteLine($"Passed: {executorPassed}");
            _output.WriteLine($"Failed: {executorFailed}");
            _output.WriteLine($"Mismatches with original: {mismatches.Count}");

            if (mismatches.Any())
            {
                _output.WriteLine($"\n=== MISMATCHES ===");
                foreach (var m in mismatches)
                    _output.WriteLine(m);
            }

            Assert.Empty(mismatches);
        }

        /// <summary>
        /// Tests the TransactionExecutor against stExample category.
        /// </summary>
        [Fact]
        public async Task TransactionExecutor_stExample_MatchesOriginal()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var categoryPath = Path.Combine(_testVectorsPath, "stExample");
            if (!Directory.Exists(categoryPath))
            {
                _output.WriteLine($"Category stExample not found at {categoryPath}");
                Assert.True(false, "Category stExample not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var testFiles = Directory.GetFiles(categoryPath, "*.json", SearchOption.AllDirectories);

            var mismatches = new System.Collections.Generic.List<string>();

            foreach (var testFile in testFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(testFile);

                var originalResult = await runner.RunTestAsync(testFile);
                var executorResult = await runner.RunTestWithExecutorAsync(testFile);

                for (int i = 0; i < originalResult.Results.Count && i < executorResult.Results.Count; i++)
                {
                    var orig = originalResult.Results[i];
                    var exec = executorResult.Results[i];

                    if (orig.Passed != exec.Passed && !orig.Skipped && !exec.Skipped)
                    {
                        mismatches.Add($"{fileName}[{orig.DataIndex}]: Original={orig.Passed}, Executor={exec.Passed} - {exec.Message}");
                    }
                }
            }

            _output.WriteLine($"Mismatches: {mismatches.Count}");
            foreach (var m in mismatches)
                _output.WriteLine(m);

            Assert.Empty(mismatches);
        }

        [Fact]
        public async Task DebugCodeLookup_Create2CallPrecompiles()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCreate2", "create2callPrecompiles.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, "Test file not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);

            foreach (var r in result.Results)
            {
                _output.WriteLine($"Test: {r.TestName} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}] - {(r.Passed ? "PASSED" : r.Skipped ? "SKIPPED" : "FAILED")}");
                if (!r.Passed && !r.Skipped && r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs.Take(10))
                        _output.WriteLine($"  {diff}");
                }
            }
        }

        [Fact]
        public async Task TestCallCodeRecursive()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcallcode_ABCB_RECURSIVE.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, "Test file not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestAsync(testFile);

            foreach (var r in result.Results)
            {
                _output.WriteLine($"Test: {r.TestName} [{r.DataIndex},{r.GasIndex},{r.ValueIndex}] - {(r.Passed ? "PASSED" : r.Skipped ? "SKIPPED" : "FAILED")}");
                if (!r.Passed && !r.Skipped)
                {
                    _output.WriteLine($"  Message: {r.Message}");
                    if (r.AccountDiffs != null)
                    {
                        foreach (var diff in r.AccountDiffs.Take(5))
                            _output.WriteLine($"  {diff}");
                    }
                }
            }
        }

        // ============================================================
        // Basic/Example Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stExample()
        {
            await RunCategoryAsync("stExample");
        }

        [Fact]
        public async Task RunSpecificCategory_stArgsZeroOneBalance()
        {
            await RunCategoryAsync("stArgsZeroOneBalance");
        }

        // ============================================================
        // EIP-Specific Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stEIP150Specific()
        {
            await RunCategoryAsync("stEIP150Specific");
        }

        [Fact]
        public async Task RunSpecificCategory_stEIP150singleCodeGasPrices()
        {
            await RunCategoryAsync("stEIP150singleCodeGasPrices");
        }

        [Fact]
        public async Task RunSpecificCategory_stEIP158Specific()
        {
            await RunCategoryAsync("stEIP158Specific");
        }

        [Fact]
        public async Task RunSpecificCategory_stEIP1559()
        {
            await RunCategoryAsync("stEIP1559");
        }

        [Fact]
        public async Task RunSpecificCategory_stEIP2930()
        {
            await RunCategoryAsync("stEIP2930");
        }

        [Fact]
        public async Task RunSpecificCategory_stEIP3607()
        {
            await RunCategoryAsync("stEIP3607");
        }

        // ============================================================
        // CREATE/CREATE2 Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stCreate2()
        {
            await RunCategoryAsync("stCreate2");
        }

        [Fact]
        public async Task RunSpecificCategory_stCreateTest()
        {
            await RunCategoryAsync("stCreateTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stRecursiveCreate()
        {
            await RunCategoryAsync("stRecursiveCreate");
        }

        [Fact]
        public async Task RunSpecificCategory_stInitCodeTest()
        {
            await RunCategoryAsync("stInitCodeTest");
        }

        // ============================================================
        // CALL/DELEGATECALL/STATICCALL Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stCallCodes()
        {
            await RunCategoryAsync("stCallCodes");
        }

        [Fact]
        public async Task RunSpecificCategory_stCallCreateCallCodeTest()
        {
            await RunCategoryAsync("stCallCreateCallCodeTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stCallDelegateCodesCallCodeHomestead()
        {
            await RunCategoryAsync("stCallDelegateCodesCallCodeHomestead");
        }

        [Fact]
        public async Task RunSpecificCategory_stCallDelegateCodesHomestead()
        {
            await RunCategoryAsync("stCallDelegateCodesHomestead");
        }

        [Fact]
        public async Task RunSpecificCategory_stDelegatecallTestHomestead()
        {
            await RunCategoryAsync("stDelegatecallTestHomestead");
        }

        [Fact]
        public async Task RunSpecificCategory_stStaticCall()
        {
            await RunCategoryAsync("stStaticCall");
        }

        [Fact]
        public async Task RunSpecificCategory_stStaticFlagEnabled()
        {
            await RunCategoryAsync("stStaticFlagEnabled");
        }

        [Fact]
        public async Task RunSpecificCategory_stNonZeroCallsTest()
        {
            await RunCategoryAsync("stNonZeroCallsTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stZeroCallsTest()
        {
            await RunCategoryAsync("stZeroCallsTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stZeroCallsRevert()
        {
            await RunCategoryAsync("stZeroCallsRevert");
        }

        // ============================================================
        // Memory Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stMemoryTest()
        {
            await RunCategoryAsync("stMemoryTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stMemExpandingEIP150Calls()
        {
            await RunCategoryAsync("stMemExpandingEIP150Calls");
        }

        [Fact]
        public async Task RunSpecificCategory_stMemoryStressTest()
        {
            await RunCategoryAsync("stMemoryStressTest");
        }

        // ============================================================
        // Storage Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stSLoadTest()
        {
            await RunCategoryAsync("stSLoadTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stSStoreTest()
        {
            await RunCategoryAsync("stSStoreTest");
        }

        // ============================================================
        // Precompiled Contracts Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stPreCompiledContracts()
        {
            await RunCategoryAsync("stPreCompiledContracts");
        }

        [Fact]
        public async Task RunSpecificCategory_stPreCompiledContracts2()
        {
            await RunCategoryAsync("stPreCompiledContracts2");
        }

        [Fact]
        public async Task Debug_CALLCODEEcrecoverV_prefixedf0()
        {
            var testFile = Path.Combine(_testVectorsPath, "stPreCompiledContracts2", "CALLCODEEcrecoverV_prefixedf0.json");
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestAsync(testFile);

            _output.WriteLine($"Passed: {result.PassedCount}, Failed: {result.FailedCount}");
            foreach (var r in result.Results.Where(x => !x.Passed))
            {
                _output.WriteLine($"[{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {r.Message}");
            }
        }

        [Fact]
        public async Task Debug_buffer_185()
        {
            var testFile = Path.Combine(_testVectorsPath, "stMemoryTest", "buffer.json");

            var nethRunner = new GeneralStateTestRunner(_output, "Prague");
            var nethResult = await nethRunner.RunTestWithTraceAsync(testFile, specificDataIndex: 185);

            _output.WriteLine($"=== NETHEREUM RESULT ===");
            _output.WriteLine($"Passed: {nethResult.PassedCount}, Failed: {nethResult.FailedCount}");
            foreach (var r in nethResult.Results)
            {
                _output.WriteLine($"[{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {(r.Passed ? "PASSED" : "FAILED")} {r.Message}");
            }

            _output.WriteLine($"\n=== GETH TRACE COMPARISON ===");
            var gethRunner = new GethEvmRunner();
            var gethResult = await gethRunner.RunStateTestAsync(testFile, dataIndex: 185, gasIndex: 0, valueIndex: 0);

            if (gethResult.Success && gethResult.Steps != null)
            {
                _output.WriteLine($"Geth steps: {gethResult.Steps.Count}");
                var nethSingleResult = nethResult.Results.FirstOrDefault(r => r.DataIndex == 185);
                var nethTraces = nethSingleResult?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();
                var comparer = new TraceComparer();
                var nethSteps = comparer.NormalizeNethTrace(nethTraces);

                _output.WriteLine($"Neth steps: {nethSteps.Count}");

                int firstDivergence = -1;
                for (int i = 0; i < Math.Min(gethResult.Steps.Count, nethSteps.Count); i++)
                {
                    var g = gethResult.Steps[i];
                    var n = nethSteps[i];
                    if (g.GasCost != n.GasCost)
                    {
                        firstDivergence = i;
                        _output.WriteLine($"\n=== FIRST DIVERGENCE at step {i} ===");
                        _output.WriteLine($"Geth: PC={g.PC} Op={g.Op} Gas={g.Gas} Cost={g.GasCost}");
                        _output.WriteLine($"Neth: PC={n.PC} Op={n.Op} Gas={n.Gas} Cost={n.GasCost}");

                        _output.WriteLine($"\n=== Context around divergence ===");
                        for (int j = Math.Max(0, i - 3); j <= Math.Min(gethResult.Steps.Count - 1, i + 3); j++)
                        {
                            var gStep = gethResult.Steps[j];
                            var nStep = j < nethSteps.Count ? nethSteps[j] : null;
                            var marker = j == i ? ">>> " : "    ";
                            _output.WriteLine($"{marker}[{j}] Geth: PC={gStep.PC} Op={gStep.Op} Gas={gStep.Gas} Cost={gStep.GasCost}");
                            if (nStep != null)
                                _output.WriteLine($"{marker}[{j}] Neth: PC={nStep.PC} Op={nStep.Op} Gas={nStep.Gas} Cost={nStep.GasCost}");
                        }
                        break;
                    }
                }

                if (firstDivergence < 0)
                    _output.WriteLine("No gas cost divergence found in overlapping steps");

                _output.WriteLine($"\n=== Geth stack at divergence ===");
                if (firstDivergence >= 0 && firstDivergence < gethResult.Steps.Count)
                {
                    var gStep = gethResult.Steps[firstDivergence];
                    if (gStep.Stack != null)
                    {
                        _output.WriteLine($"Stack (top to bottom):");
                        for (int si = 0; si < Math.Min(10, gStep.Stack.Count); si++)
                            _output.WriteLine($"  [{si}]: {gStep.Stack[si]}");
                    }
                }

                _output.WriteLine($"\n=== Looking for address-related ops before divergence ===");
                for (int i = 0; i < firstDivergence && i < gethResult.Steps.Count; i++)
                {
                    var gStep = gethResult.Steps[i];
                    if (gStep.Op.StartsWith("EXTCODE") || gStep.Op == "BALANCE" ||
                        gStep.Op == "CALL" || gStep.Op == "CALLCODE" ||
                        gStep.Op == "DELEGATECALL" || gStep.Op == "STATICCALL" ||
                        gStep.Op == "SELFDESTRUCT" || gStep.Op == "CREATE" || gStep.Op == "CREATE2")
                    {
                        _output.WriteLine($"[{i}] PC={gStep.PC} Op={gStep.Op} Cost={gStep.GasCost}");
                        if (gStep.Stack != null && gStep.Stack.Count > 1)
                            _output.WriteLine($"      Stack[1] (address): {gStep.Stack[1]}");
                    }
                }

                _output.WriteLine($"\n=== All steps with cost > 100 (potential cold access) ===");
                for (int i = 0; i < firstDivergence && i < gethResult.Steps.Count; i++)
                {
                    var gStep = gethResult.Steps[i];
                    if (gStep.GasCost > 100)
                    {
                        _output.WriteLine($"[{i}] PC={gStep.PC} Op={gStep.Op} Cost={gStep.GasCost}");
                    }
                }
            }
            else
            {
                _output.WriteLine($"Geth run failed: {gethResult.Error}");
            }
        }

        [Fact]
        public async Task RunSpecificCategory_stZeroKnowledge()
        {
            await RunCategoryAsync("stZeroKnowledge");
        }

        [Fact]
        public async Task RunSpecificCategory_stZeroKnowledge2()
        {
            await RunCategoryAsync("stZeroKnowledge2");
        }

        // ============================================================
        // Code/ExtCode Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stCodeCopyTest()
        {
            await RunCategoryAsync("stCodeCopyTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stCodeSizeLimit()
        {
            await RunCategoryAsync("stCodeSizeLimit");
        }

        [Fact]
        public async Task RunSpecificCategory_stExtCodeHash()
        {
            await RunCategoryAsync("stExtCodeHash");
        }

        // ============================================================
        // Stack/Flow Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stStackTests()
        {
            await RunCategoryAsync("stStackTests");
        }

        [Fact]
        public async Task RunSpecificCategory_stShift()
        {
            await RunCategoryAsync("stShift");
        }

        // ============================================================
        // Log Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stLogTests()
        {
            await RunCategoryAsync("stLogTests");
        }

        // ============================================================
        // Return/Revert Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stReturnDataTest()
        {
            await RunCategoryAsync("stReturnDataTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stRevertTest()
        {
            await RunCategoryAsync("stRevertTest");
        }

        [Fact]
        public async Task RunSingleTest_LoopCallsDepthThenRevert()
        {
            await RunSingleTestFileAsync("stRevertTest", "LoopCallsDepthThenRevert");
        }

        [Fact]
        public async Task RunSingleTest_callcall_00()
        {
            await RunSingleTestFileAsync("stCallCodes", "callcall_00");
        }

#if ENABLE_OLD_RECURSIVE_ARCHITECTURE
        [Fact]
        public async Task RunSingleTest_callcall_00_OldArchitecture()
        {
            await RunSingleTestFileAsyncWithOldArchitecture("stCallCodes", "callcall_00");
        }
#endif

        [Fact]
        public async Task RunSingleTest_callcallcall_ABCB_RECURSIVE()
        {
            await RunSingleTestFileAsync("stCallCodes", "callcallcall_ABCB_RECURSIVE");
        }

#if ENABLE_OLD_RECURSIVE_ARCHITECTURE
        [Fact]
        public async Task RunSingleTest_callcallcall_ABCB_RECURSIVE_OldArchitecture()
        {
            await RunSingleTestFileAsyncWithOldArchitecture("stCallCodes", "callcallcall_ABCB_RECURSIVE");
        }
#endif

        [Fact]
        public async Task RunSingleTest_callcodecallcodecallcode_ABCB_RECURSIVE()
        {
            await RunSingleTestFileAsync("stCallCodes", "callcodecallcodecallcode_ABCB_RECURSIVE");
        }

        [Fact]
        public async Task RunSingleTest_callcodecallcode_11_SuicideEnd()
        {
            await RunSingleTestFileAsync("stCallCodes", "callcodecallcode_11_SuicideEnd");
        }

        [Fact]
        public async Task RunSingleTest_callcodeEmptycontract()
        {
            await RunSingleTestFileAsync("stCallCodes", "callcodeEmptycontract");
        }

        [Fact]
        public async Task RunSingleTest_create2noCash()
        {
            await RunSingleTestFileAsync("stCreate2", "create2noCash");
        }

        [Fact]
        public async Task RunSingleTest_CREATE2_Bounds()
        {
            await RunSingleTestFileAsync("stCreate2", "CREATE2_Bounds");
        }

        [Fact]
        public async Task RunSingleTest_createFailResult()
        {
            await RunSingleTestFileAsync("stCreateTest", "createFailResult");
        }

        [Fact]
        public async Task RunSingleTest_randomStatetest85()
        {
            await RunSingleTestFileAsync("stRandom", "randomStatetest85");
        }

        [Fact]
        public async Task RunSingleTest_randomStatetest572()
        {
            await RunSingleTestFileAsync("stRandom2", "randomStatetest572");
        }

        [Fact]
        public async Task RunSingleTest_randomStatetest626()
        {
            await RunSingleTestFileAsync("stRandom2", "randomStatetest626");
        }

        [Fact]
        public async Task RunSingleTest_ecpairing_empty_data()
        {
            await RunSingleTestFileAsync("stZeroKnowledge", "ecpairing_empty_data");
        }

        [Fact]
        public async Task RunSingleTest_ecpairing_two_point_match_2()
        {
            await RunSingleTestFileAsync("stZeroKnowledge", "ecpairing_two_point_match_2");
        }

        private async Task RunSingleTestFileAsync(string categoryName, string testFileName, bool validateTraces = true)
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, categoryName, $"{testFileName}.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, $"Test file not found: {testFile}");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            _output.WriteLine($"Running: {testFileName}");

            var result = await runner.RunTestAsync(testFile);

            _output.WriteLine("");
            _output.WriteLine($"=== {testFileName} SUMMARY ===");
            _output.WriteLine($"Passed:  {result.PassedCount}");
            _output.WriteLine($"Failed:  {result.FailedCount}");
            _output.WriteLine($"Skipped: {result.SkippedCount}");

            foreach (var r in result.Results.Where(x => !x.Passed && !x.Skipped))
            {
                _output.WriteLine($"  FAILED: [{r.DataIndex},{r.GasIndex},{r.ValueIndex}]");
                if (r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"    {diff}");
                }
            }

            if (result.FailedCount > 0 && validateTraces)
            {
                _output.WriteLine("");
                _output.WriteLine("=== TRACE VALIDATION (comparing with geth) ===");
                try
                {
                    var (gethResult, nethResult, validation) = await runner.RunAndValidateFullAsync(testFile, new TraceValidationOptions
                    {
                        ValidateGasCost = true,
                        ValidateGasRemaining = true,
                        ContinueOnMismatch = true
                    });

                    _output.WriteLine($"Geth success: {gethResult.Success}, exit code: {gethResult.ExitCode}");
                    if (!string.IsNullOrEmpty(gethResult.Error))
                        _output.WriteLine($"Geth error: {gethResult.Error}");
                    if (!string.IsNullOrEmpty(gethResult.RawError) && gethResult.RawError.Length < 2000)
                        _output.WriteLine($"Geth stderr: {gethResult.RawError}");
                    else if (!string.IsNullOrEmpty(gethResult.RawError))
                        _output.WriteLine($"Geth stderr (first 500): {gethResult.RawError.Substring(0, 500)}...");

                    _output.WriteLine($"Geth steps: {validation.TotalGethSteps}");
                    _output.WriteLine($"Neth steps: {validation.TotalNethSteps}");
                    _output.WriteLine($"Matched: {validation.MatchedSteps}");
                    _output.WriteLine($"Valid: {validation.IsValid}");
                    if (validation.Mismatches?.Count > 0)
                    {
                        _output.WriteLine($"Total mismatches: {validation.Mismatches.Count}");
                        foreach (var m in validation.Mismatches.Take(20))
                        {
                            _output.WriteLine($"  [{m.StepIndex}] {m.Field}: Geth={m.GethValue}, Neth={m.NethValue}");
                        }
                        if (validation.Mismatches.Count > 20)
                            _output.WriteLine($"  ... and {validation.Mismatches.Count - 20} more");
                    }

                    if (!validation.IsValid && validation.FirstMismatch != null)
                    {
                        _output.WriteLine($"\nFIRST MISMATCH at step {validation.FirstMismatch.StepIndex}:");
                        _output.WriteLine($"  Field: {validation.FirstMismatch.Field}");
                        _output.WriteLine($"  Geth:  {validation.FirstMismatch.GethValue}");
                        _output.WriteLine($"  Neth:  {validation.FirstMismatch.NethValue}");

                        if (validation.FirstMismatch.GethStep != null)
                        {
                            var g = validation.FirstMismatch.GethStep;
                            _output.WriteLine($"\n  Geth context: PC={g.PC} Op={g.Op} Gas={g.Gas} Cost={g.GasCost} Depth={g.Depth}");
                        }
                        if (validation.FirstMismatch.NethStep != null)
                        {
                            var n = validation.FirstMismatch.NethStep;
                            _output.WriteLine($"  Neth context: PC={n.PC} Op={n.Op} Gas={n.Gas} Cost={n.GasCost} Depth={n.Depth}");
                        }

                        // Show surrounding steps for context
                        var mismatchIdx = validation.FirstMismatch.StepIndex;
                        _output.WriteLine($"\n  Previous 10 GETH steps:");
                        for (int i = Math.Max(0, mismatchIdx - 10); i < mismatchIdx && i < gethResult.Steps.Count; i++)
                        {
                            var s = gethResult.Steps[i];
                            _output.WriteLine($"    [{i}] D={s.Depth} PC={s.PC} Op={s.Op,-12} Gas={s.Gas} Cost={s.GasCost}");
                        }
                        if (mismatchIdx < gethResult.Steps.Count)
                        {
                            var s = gethResult.Steps[mismatchIdx];
                            _output.WriteLine($"  > [{mismatchIdx}] D={s.Depth} PC={s.PC} Op={s.Op,-12} Gas={s.Gas} Cost={s.GasCost} <<< MISMATCH");
                        }

                        var comparer = new TraceComparer();
                        var nethSteps = comparer.NormalizeNethTrace(nethResult.Results.FirstOrDefault(r => !r.Skipped)?.Traces);
                        if (nethSteps != null)
                        {
                            _output.WriteLine($"\n  Previous 10 NETH steps:");
                            for (int i = Math.Max(0, mismatchIdx - 10); i < mismatchIdx && i < nethSteps.Count; i++)
                            {
                                var s = nethSteps[i];
                                _output.WriteLine($"    [{i}] D={s.Depth} PC={s.PC} Op={s.Op,-12} Gas={s.Gas} Cost={s.GasCost}");
                            }
                            if (mismatchIdx < nethSteps.Count)
                            {
                                var s = nethSteps[mismatchIdx];
                                _output.WriteLine($"  > [{mismatchIdx}] D={s.Depth} PC={s.PC} Op={s.Op,-12} Gas={s.Gas} Cost={s.GasCost} <<< MISMATCH");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Trace validation failed: {ex.Message}");
                }
            }

            Assert.Equal(0, result.FailedCount);
        }

#if ENABLE_OLD_RECURSIVE_ARCHITECTURE
        private async Task RunSingleTestFileAsyncWithOldArchitecture(string categoryName, string testFileName)
        {
            if (_testVectorsPath == null || !Directory.Exists(_testVectorsPath))
            {
                _output.WriteLine("Test vectors not found");
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, categoryName, $"{testFileName}.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, $"Test file not found: {testFile}");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague", useOldRecursiveArchitecture: true);
            _output.WriteLine($"Running (OLD ARCHITECTURE): {testFileName}");

            var result = await runner.RunTestAsync(testFile);

            _output.WriteLine("");
            _output.WriteLine($"=== {testFileName} (OLD ARCH) SUMMARY ===");
            _output.WriteLine($"Passed:  {result.PassedCount}");
            _output.WriteLine($"Failed:  {result.FailedCount}");
            _output.WriteLine($"Skipped: {result.SkippedCount}");

            foreach (var r in result.Results.Where(x => !x.Passed && !x.Skipped))
            {
                _output.WriteLine($"  FAILED: [{r.DataIndex},{r.GasIndex},{r.ValueIndex}]");
                if (r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"    {diff}");
                }
            }

            Assert.Equal(0, result.FailedCount);
        }
#endif

        // ============================================================
        // Refund Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stRefundTest()
        {
            await RunCategoryAsync("stRefundTest");
        }

        // ============================================================
        // Balance/SelfBalance Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stSelfBalance()
        {
            await RunCategoryAsync("stSelfBalance");
        }

        // ============================================================
        // ChainId Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stChainId()
        {
            await RunCategoryAsync("stChainId");
        }

        // ============================================================
        // Transaction Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stTransactionTest()
        {
            await RunCategoryAsync("stTransactionTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stTransitionTest()
        {
            await RunCategoryAsync("stTransitionTest");
        }

        // ============================================================
        // System Operations Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stSystemOperationsTest()
        {
            await RunCategoryAsync("stSystemOperationsTest");
        }

        // ============================================================
        // Homestead Specific Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stHomesteadSpecific()
        {
            await RunCategoryAsync("stHomesteadSpecific");
        }

        // ============================================================
        // Special/Edge Case Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stSpecialTest()
        {
            await RunCategoryAsync("stSpecialTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stBadOpcode()
        {
            await RunCategoryAsync("stBadOpcode");
        }

        [Fact]
        public async Task RunSpecificCategory_stBugs()
        {
            await RunCategoryAsync("stBugs");
        }

        [Fact]
        public async Task RunSpecificCategory_stAttackTest()
        {
            await RunCategoryAsync("stAttackTest");
        }

        // ============================================================
        // Solidity Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stSolidityTest()
        {
            await RunCategoryAsync("stSolidityTest");
        }

        // ============================================================
        // Wallet Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stWalletTest()
        {
            await RunCategoryAsync("stWalletTest");
        }

        // ============================================================
        // Random/Fuzz Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stRandom()
        {
            await RunCategoryAsync("stRandom");
        }

        [Fact]
        public async Task RunSpecificCategory_stRandom2()
        {
            await RunCategoryAsync("stRandom2");
        }

        // ============================================================
        // Performance/Stress Tests (Skipped by default)
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stQuadraticComplexityTest()
        {
            await RunCategoryAsync("stQuadraticComplexityTest");
        }

        [Fact]
        public async Task RunSpecificCategory_stTimeConsuming()
        {
            await RunCategoryAsync("stTimeConsuming");
        }

        // ============================================================
        // Expect Section Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_stExpectSection()
        {
            await RunCategoryAsync("stExpectSection");
        }

        // ============================================================
        // VM Tests (included in GeneralStateTests)
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_VMTests()
        {
            await RunCategoryAsync("VMTests");
        }

        // ============================================================
        // Multi-Hardfork Theory Tests (Cancun + Prague)
        // ============================================================

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stChainId_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stChainId", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stExample_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stExample", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stSLoadTest_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stSLoadTest", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stSStoreTest_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stSStoreTest", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stCallCodes_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stCallCodes", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stCreateTest_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stCreateTest", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stCreate2_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stCreate2", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stMemoryTest_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stMemoryTest", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stPreCompiledContracts_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stPreCompiledContracts", hardfork);
        }

        [Theory]
        [InlineData("Cancun")]
        [InlineData("Prague")]
        public async Task RunCategory_stTransactionTest_MultiFork(string hardfork)
        {
            await RunCategoryAsync("stTransactionTest", hardfork);
        }

        // ============================================================
        // Hardfork-Specific Tests
        // ============================================================

        [Fact]
        public async Task RunSpecificCategory_Cancun()
        {
            await RunCategoryAsync("Cancun");
        }

        [Fact]
        public async Task RunSpecificCategory_Prague()
        {
            await RunCategoryAsync("Prague");
        }

        [Fact]
        public async Task RunSpecificCategory_Shanghai()
        {
            await RunCategoryAsync("Shanghai");
        }

        [Fact]
        public async Task RunSingleFile_RevertOpcode()
        {
            await RunSingleFileAsync("stRevertTest", "RevertOpcode.json");
        }

        [Fact]
        public async Task RunSingleFile_LoopCallsDepthThenRevert()
        {
            await RunSingleFileAsync("stRevertTest", "LoopCallsDepthThenRevert.json");
        }

        [Fact]
        public async Task RunSingleFile_LoopCallsThenRevert()
        {
            await RunSingleFileAsync("stRevertTest", "LoopCallsThenRevert.json");
        }

        [Fact]
        public async Task RunSingleFile_RevertInCreateInInit_Paris()
        {
            await RunSingleFileAsync("stRevertTest", "RevertInCreateInInit_Paris.json");
        }

        [Fact]
        public async Task RunSingleFile_RevertOpcodeCreate()
        {
            await RunSingleFileAsync("stRevertTest", "RevertOpcodeCreate.json");
        }

        [Fact]
        public async Task RunSingleFile_RevertOpcodeMultipleSubCalls()
        {
            await RunSingleFileAsync("stRevertTest", "RevertOpcodeMultipleSubCalls.json");
        }

        [Fact]
        public async Task DebugSingleFile_FailedTx()
        {
            await RunSingleFileAsync("stSpecialTest", "failed_tx_xcf416c53_Paris.json");
        }

        [Fact]
        public async Task CompareTraces_FailedTx()
        {
            await CompareTracesAsync("stSpecialTest", "failed_tx_xcf416c53_Paris");
        }

        [Fact]
        public async Task DebugSingleFile_PythonRevertTest()
        {
            await RunSingleFileAsync("stRevertTest", "PythonRevertTestTue201814-1430.json");
        }

        [Fact]
        public async Task CompareTraces_PythonRevertTest()
        {
            await CompareTracesAsync("stRevertTest", "PythonRevertTestTue201814-1430");
        }

        [Fact]
        public async Task DebugSingleFile_CallcodeCost()
        {
            await RunSingleFileAsync("stStaticCall", "static_callcodecallcodecall_1102.json");
        }

        [Fact]
        public async Task CompareTraces_CallcodeCost()
        {
            await CompareTracesAsync("stStaticCall", "static_callcodecallcodecall_1102");
        }

        [Fact]
        public async Task DebugSingleFile_StaticcallToPrecompile()
        {
            await RunSingleFileAsync("stStaticCall", "StaticcallToPrecompileFromTransaction.json");
        }

        [Fact]
        public async Task CompareTraces_StaticcallToPrecompile()
        {
            await CompareTracesAsync("stStaticCall", "StaticcallToPrecompileFromTransaction");
        }

        [Fact]
        public async Task DebugSingleFile_CallIdentityNonzeroValue()
        {
            await RunSingleFileAsync("stStaticCall", "static_CallIdentity_1_nonzeroValue.json");
        }

        [Fact]
        public async Task CompareTraces_CallIdentityNonzeroValue()
        {
            await CompareTracesAsync("stStaticCall", "static_CallIdentity_1_nonzeroValue");
        }

        [Fact]
        public async Task DebugSingleFile_ReturnBounds()
        {
            await RunSingleFileAsync("stStaticCall", "static_RETURN_Bounds.json");
        }

        [Fact]
        public async Task CompareTraces_ReturnBounds()
        {
            await CompareTracesAsync("stStaticCall", "static_RETURN_Bounds");
        }

        [Fact]
        public async Task DebugSingleFile_CreateSuicideDuringInit()
        {
            await RunSingleFileAsync("stStaticCall", "static_CREATE_ContractSuicideDuringInit.json");
        }

        [Fact]
        public async Task CompareTraces_CreateSuicideDuringInit()
        {
            await CompareTracesAsync("stStaticCall", "static_CREATE_ContractSuicideDuringInit");
        }

        [Fact]
        public async Task DebugSingleFile_StaticCheckOpcodes()
        {
            await RunSingleFileAsync("stStaticCall", "static_CheckOpcodes2.json");
        }

        [Fact]
        public async Task CompareTraces_StaticCheckOpcodes()
        {
            await CompareTracesAsync("stStaticCall", "static_CheckOpcodes2");
        }

        private async Task RunSingleFileAsync(string categoryName, string fileName)
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, categoryName, fileName);
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file {testFile} not found");
                Assert.True(false, $"Test file {fileName} not found");
                return;
            }

            var runner = new GeneralStateTestRunner(_output, "Prague", TimeSpan.FromSeconds(60));
            var result = await runner.RunTestAsync(testFile);

            _output.WriteLine($"File: {fileName}");
            _output.WriteLine($"  Passed: {result.PassedCount}");
            _output.WriteLine($"  Failed: {result.FailedCount}");
            _output.WriteLine($"  Skipped: {result.SkippedCount}");

            foreach (var r in result.Results.Where(x => !x.Passed && !x.Skipped))
            {
                _output.WriteLine($"  FAILED: {r.TestName}[{r.DataIndex},{r.GasIndex},{r.ValueIndex}]: {r.Message}");
                if (r.AccountDiffs != null)
                {
                    foreach (var diff in r.AccountDiffs)
                        _output.WriteLine($"    {diff}");
                }
            }

            Assert.Equal(0, result.FailedCount);
        }

        // ============================================================
        // Trace Comparison Utility - Compare geth and Nethereum traces
        // ============================================================

        [Fact]
        public async Task CompareTraces_callcallcall_ABCB_RECURSIVE()
        {
            await CompareTracesAsync("stCallCodes", "callcallcall_ABCB_RECURSIVE");
        }

        [Fact]
        public async Task CompareTraces_ShowCALLGasAllocation()
        {
            await CompareCALLGasAsync("stCallCodes", "callcallcall_ABCB_RECURSIVE");
        }

        [Fact]
        public async Task CompareTraces_StepByStepGas()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found");
                return;
            }

            _output.WriteLine("=== STEP-BY-STEP GAS COMPARISON ===\n");

            var gethTraceLines = await RunGethTraceAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            var gethIdx = 0;
            var nethIdx = 0;
            long gethCumulativeGas = 0;
            long nethCumulativeGas = 0;
            var gasDiffCount = 0;
            var firstDiffStep = -1;
            var lastDiffStep = -1;
            long totalGasDiff = 0;

            _output.WriteLine("Showing steps where gas cost differs (same op/pc/depth):\n");
            _output.WriteLine("Step | Op | Depth | Geth Gas | Neth Gas | Diff | Cumulative Diff");
            _output.WriteLine("-----|-----|-------|----------|----------|------|----------------");

            while (gethIdx < gethTraceLines.Count && nethIdx < nethTraces.Count)
            {
                var gethLine = gethTraceLines[gethIdx];
                if (!gethLine.StartsWith("{") || !gethLine.Contains("\"opName\""))
                {
                    gethIdx++;
                    continue;
                }

                var gethOp = ParseGethField(gethLine, "opName").Trim('"');
                var gethPc = ParseGethField(gethLine, "pc");
                var gethGasCost = long.Parse(ParseGethHexField(gethLine, "gasCost"));
                var gethDepth = ParseGethField(gethLine, "depth");

                var nethTrace = nethTraces[nethIdx];
                var nethOp = nethTrace.Instruction?.Instruction?.ToString() ?? "?";
                var nethPc = nethTrace.Instruction?.Step.ToString() ?? "?";
                var nethGasCost = (long)nethTrace.GasCost;
                var nethDepth = nethTrace.Depth.ToString();

                // Check if ops match
                var opMatch = gethOp.ToUpper() == nethOp.ToUpper();
                var pcMatch = gethPc == nethPc;
                var depthMatch = gethDepth == nethDepth;

                if (!opMatch || !pcMatch || !depthMatch)
                {
                    // Traces diverged - stop comparison
                    _output.WriteLine($"\n--- Traces diverged at step {gethIdx} ---");
                    _output.WriteLine($"GETH: op={gethOp} pc={gethPc} depth={gethDepth}");
                    _output.WriteLine($"NETH: op={nethOp} pc={nethPc} depth={nethDepth}");
                    break;
                }

                // Track gas
                gethCumulativeGas += gethGasCost;
                nethCumulativeGas += nethGasCost;

                // Check for gas cost difference (skip CALL/CREATE which include allocation)
                var isCallOp = gethOp.Contains("CALL") || gethOp.Contains("CREATE");
                if (!isCallOp && gethGasCost != nethGasCost)
                {
                    var diff = nethGasCost - gethGasCost;
                    totalGasDiff += diff;
                    gasDiffCount++;
                    if (firstDiffStep < 0) firstDiffStep = gethIdx;
                    lastDiffStep = gethIdx;

                    // Show first 30 and last 10 differences
                    if (gasDiffCount <= 30 || nethIdx > nethTraces.Count - 100)
                    {
                        _output.WriteLine($"{gethIdx,4} | {gethOp,-4} | {gethDepth,5} | {gethGasCost,8} | {nethGasCost,8} | {diff,+4} | {totalGasDiff,+15}");
                    }
                    else if (gasDiffCount == 31)
                    {
                        _output.WriteLine("... (skipping middle differences) ...");
                    }
                }

                gethIdx++;
                nethIdx++;
            }

            _output.WriteLine($"\n=== SUMMARY ===");
            _output.WriteLine($"Steps compared: {nethIdx}");
            _output.WriteLine($"Steps with gas diff: {gasDiffCount}");
            _output.WriteLine($"First diff at step: {firstDiffStep}");
            _output.WriteLine($"Last diff at step: {lastDiffStep}");
            _output.WriteLine($"Geth cumulative gas: {gethCumulativeGas}");
            _output.WriteLine($"Neth cumulative gas: {nethCumulativeGas}");
            _output.WriteLine($"Total gas difference: {totalGasDiff} (Neth - Geth)");
            _output.WriteLine($"Average diff per differing step: {(gasDiffCount > 0 ? totalGasDiff / gasDiffCount : 0)}");

            // Now compare CALL operations specifically
            _output.WriteLine("\n=== CALL GAS REMAINING COMPARISON ===");
            _output.WriteLine("Comparing 'gas' field (remaining gas before CALL):\n");
            _output.WriteLine("Depth | Geth GasBefore | Neth GasBefore | Diff");
            _output.WriteLine("------|----------------|----------------|------");

            gethIdx = 0;
            nethIdx = 0;
            var callCompareCount = 0;
            long totalCallGasDiff = 0;

            while (gethIdx < gethTraceLines.Count && nethIdx < nethTraces.Count && callCompareCount < 280)
            {
                var gethLine = gethTraceLines[gethIdx];
                if (!gethLine.StartsWith("{") || !gethLine.Contains("\"opName\""))
                {
                    gethIdx++;
                    continue;
                }

                var gethOp = ParseGethField(gethLine, "opName").Trim('"');
                var gethDepth = ParseGethField(gethLine, "depth");

                var nethTrace = nethTraces[nethIdx];
                var nethOp = nethTrace.Instruction?.Instruction?.ToString() ?? "?";
                var nethDepth = nethTrace.Depth.ToString();

                if (gethOp.ToUpper() != nethOp.ToUpper() || gethDepth != nethDepth)
                {
                    break; // diverged
                }

                if (gethOp == "CALL" || gethOp == "CALLCODE")
                {
                    callCompareCount++;
                    var gethGasBefore = long.Parse(ParseGethHexField(gethLine, "gas"));

                    // For Nethereum, we need to look at the trace's remaining gas
                    // The trace doesn't directly have "gas before" - we'd need to calculate it
                    // For now, let's show what we have
                    var diff = 0L; // We can't easily get Neth gas before from trace

                    if (callCompareCount <= 10 || callCompareCount > 260)
                    {
                        _output.WriteLine($"{gethDepth,5} | {gethGasBefore,14} | (see trace) | {diff}");
                    }
                    else if (callCompareCount == 11)
                    {
                        _output.WriteLine("... (skipping middle) ...");
                    }
                }

                gethIdx++;
                nethIdx++;
            }
            _output.WriteLine($"\nTotal CALL operations compared: {callCompareCount}");
        }

        [Fact]
        public async Task CompareTraces_FinalGasUsed()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found");
                return;
            }

            _output.WriteLine("=== COMPARING FINAL GAS USED ===\n");

            // Run geth and capture full output including summary
            var startInfo = new ProcessStartInfo
            {
                FileName = gethEvmPath,
                Arguments = $"--json statetest \"{testFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var gethOutput = new System.Text.StringBuilder();
            var gethError = new System.Text.StringBuilder();
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) gethOutput.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) gethError.AppendLine(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
            }

            _output.WriteLine("=== GETH OUTPUT (stdout) ===");
            _output.WriteLine(gethOutput.ToString());

            // Parse geth gas from output (look for "gasUsed" in the summary)
            var gethOutputStr = gethOutput.ToString();
            long gethGasUsed = 0;
            foreach (var line in gethOutputStr.Split('\n'))
            {
                if (line.Contains("\"gasUsed\"") && line.Contains("0x"))
                {
                    var gasMatch = System.Text.RegularExpressions.Regex.Match(line, "\"gasUsed\"\\s*:\\s*\"?(0x[0-9a-fA-F]+)\"?");
                    if (gasMatch.Success)
                    {
                        gethGasUsed = Convert.ToInt64(gasMatch.Groups[1].Value, 16);
                        _output.WriteLine($"Geth gasUsed: {gethGasUsed} (0x{gethGasUsed:X})");
                    }
                }
            }

            // Run Nethereum
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestAsync(testFile);
            var testResult = result.Results.FirstOrDefault();

            _output.WriteLine($"\n=== NETHEREUM ===");
            _output.WriteLine($"Test passed: {testResult?.Passed}");
            if (!string.IsNullOrEmpty(testResult?.Message))
                _output.WriteLine($"Message: {testResult.Message}");

            // The difference would be in the balance comparison
            // Let's also read the expected post state to see what gas geth expects
            var json = File.ReadAllText(testFile);
            var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, GeneralStateTest>>(json);
            var test = tests.Values.First();
            var postResults = test.Post["Prague"];
            foreach (var post in postResults)
            {
                _output.WriteLine($"\nExpected post state hash: {post.Hash}");
            }

            if (gethGasUsed > 0)
            {
                _output.WriteLine($"\n=== GAS ANALYSIS ===");
                _output.WriteLine($"Geth total gas used: {gethGasUsed}");
                // The 7993 gas difference × 10 gasPrice = 79930 balance diff
                var expectedDiff = 7993;
                _output.WriteLine($"Expected Nethereum excess: ~{expectedDiff} gas");
                _output.WriteLine($"Implied Nethereum gas: ~{gethGasUsed + expectedDiff}");
            }
        }

        [Fact]
        public async Task Debug_RECURSIVE_HighDepthCalls()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, $"Test file not found: {testFile}");
                return;
            }

            _output.WriteLine("Running Nethereum trace...");
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();
            _output.WriteLine($"Nethereum trace: {nethTraces.Count} lines");
        }

        [Fact]
        public async Task Debug_RECURSIVE_WithBoostedGas()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, $"Test file not found: {testFile}");
                return;
            }

            // Read and modify the test JSON to boost gas
            var json = File.ReadAllText(testFile);
            var tests = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, GeneralStateTest>>(json);
            var test = tests.Values.First();

            _output.WriteLine("=== Original gas limits ===");
            for (int i = 0; i < test.Transaction.GasLimit.Count; i++)
            {
                _output.WriteLine($"  GasLimit[{i}] = {test.Transaction.GasLimit[i]}");
            }

            // Boost gas by 10x
            var boostedGas = new System.Collections.Generic.List<string>();
            foreach (var gasStr in test.Transaction.GasLimit)
            {
                var gas = gasStr.HexToBigInteger(false);
                var boosted = gas * 10;
                boostedGas.Add("0x" + boosted.ToString("X"));
            }
            test.Transaction.GasLimit = boostedGas;

            _output.WriteLine("\n=== Boosted gas limits (10x) ===");
            for (int i = 0; i < test.Transaction.GasLimit.Count; i++)
            {
                _output.WriteLine($"  GasLimit[{i}] = {test.Transaction.GasLimit[i]}");
            }

            // Save modified test to temp file
            var tempFile = Path.Combine(Path.GetTempPath(), "boosted_recursive_test.json");
            var modifiedJson = Newtonsoft.Json.JsonConvert.SerializeObject(tests, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(tempFile, modifiedJson);

            try
            {
                _output.WriteLine("\n=== Running with boosted gas ===");
                var runner = new GeneralStateTestRunner(_output, "Prague");
                var result = await runner.RunTestWithTraceAsync(tempFile);
                var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

                // Find max depth reached
                int maxDepth = 0;
                foreach (var trace in nethTraces)
                {
                    if (trace.Depth > maxDepth) maxDepth = trace.Depth;
                }

                _output.WriteLine($"\nNethereum trace count: {nethTraces.Count}");
                _output.WriteLine($"Max depth reached: {maxDepth}");

                // Now compare with geth using boosted gas
                _output.WriteLine("\n=== Running geth with boosted gas ===");
                var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
                var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");
                if (File.Exists(gethEvmPath))
                {
                    var gethTraceLines = await RunGethTraceAsync(gethEvmPath, tempFile);
                    _output.WriteLine($"Geth trace count: {gethTraceLines.Count}");

                    // Find max depth in geth trace
                    int gethMaxDepth = 0;
                    foreach (var line in gethTraceLines)
                    {
                        if (line.Contains("\"depth\""))
                        {
                            var depthStr = ParseGethField(line, "depth");
                            if (int.TryParse(depthStr, out int depth) && depth > gethMaxDepth)
                                gethMaxDepth = depth;
                        }
                    }
                    _output.WriteLine($"Geth max depth: {gethMaxDepth}");
                    _output.WriteLine($"Nethereum max depth: {maxDepth} (0-indexed, equivalent to geth {maxDepth + 1})");
                    _output.WriteLine($"Depth difference: {gethMaxDepth - (maxDepth + 1)}");
                }
                else
                {
                    _output.WriteLine($"Geth not found at: {gethEvmPath}");
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task Debug_CALL_MemoryParameters()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found");
                return;
            }

            _output.WriteLine("=== CALL MEMORY PARAMETERS DEBUG ===\n");

            // Run geth with stack trace to see actual stack values at CALL
            var gethTraceLines = await RunGethTraceAsync(gethEvmPath, testFile);

            _output.WriteLine("=== GETH CALL STACK VALUES ===");
            _output.WriteLine("Looking for CALL operations with stack contents...\n");

            int callCount = 0;
            foreach (var line in gethTraceLines)
            {
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;
                var op = ParseGethField(line, "opName").Trim('"');
                if (op != "CALL" && op != "CALLCODE") continue;

                callCount++;
                if (callCount > 10) break; // Only show first 10

                var depth = ParseGethField(line, "depth");
                var gas = ParseGethHexField(line, "gas");
                var gasCost = ParseGethHexField(line, "gasCost");

                // Try to parse stack if available
                var stackStart = line.IndexOf("\"stack\":[");
                if (stackStart > 0)
                {
                    var stackEnd = line.IndexOf("]", stackStart);
                    if (stackEnd > stackStart)
                    {
                        var stackStr = line.Substring(stackStart + 9, stackEnd - stackStart - 9);
                        var stackItems = stackStr.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim().Trim('"')).ToList();

                        _output.WriteLine($"CALL #{callCount} depth={depth} gas={gas} gasCost={gasCost}");
                        if (stackItems.Count >= 7)
                        {
                            // Stack is in reverse order: top at index 0
                            _output.WriteLine($"  Stack top-7: {string.Join(", ", stackItems.Take(7))}");
                            // For CALL: gas(0), to(1), value(2), inOff(3), inSz(4), outOff(5), outSz(6)
                            try
                            {
                                var inOff = Convert.ToInt64(stackItems[3], 16);
                                var inSz = Convert.ToInt64(stackItems[4], 16);
                                var outOff = Convert.ToInt64(stackItems[5], 16);
                                var outSz = Convert.ToInt64(stackItems[6], 16);
                                var inEnd = inSz > 0 ? inOff + inSz : 0;
                                var outEnd = outSz > 0 ? outOff + outSz : 0;
                                var maxMem = Math.Max(inEnd, outEnd);
                                _output.WriteLine($"  Memory: inOff={inOff} inSz={inSz} outOff={outOff} outSz={outSz}");
                                _output.WriteLine($"  Calculated: inEnd={inEnd} outEnd={outEnd} maxMem={maxMem}");
                            }
                            catch { }
                        }
                        _output.WriteLine("");
                    }
                }
                else
                {
                    _output.WriteLine($"CALL #{callCount} depth={depth} gas={gas} gasCost={gasCost} (no stack)");
                }
            }

            // Now run Nethereum
            _output.WriteLine("\n=== NETHEREUM CALL MEMORY PARAMETERS ===\n");

            var runner = new GeneralStateTestRunner(_output, "Prague");
            await runner.RunTestAsync(testFile);
        }

        [Fact]
        public async Task CompareTraces_CumulativeGasAtEachCALL()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found");
                return;
            }

            _output.WriteLine("=== CUMULATIVE GAS AT EACH CALL LEVEL ===");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            // Parse geth trace and find CALL operations with their gas values
            var gethCALLs = new System.Collections.Generic.List<(int depth, long gasBefore, long gasCost, int traceIndex)>();
            int gethTraceIndex = 0;
            foreach (var line in gethTraceLines)
            {
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;
                var op = ParseGethField(line, "opName").Trim('"');
                if (op == "CALL" || op == "CALLCODE")
                {
                    var depth = int.Parse(ParseGethField(line, "depth"));
                    var gasBefore = long.Parse(ParseGethHexField(line, "gas"));
                    var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));
                    gethCALLs.Add((depth, gasBefore, gasCost, gethTraceIndex));
                }
                gethTraceIndex++;
            }

            // Parse Nethereum trace and find CALL operations
            // Track cumulative gas cost to calculate "gas before" for each CALL
            var nethCALLs = new System.Collections.Generic.List<(int depth, long cumulativeGas, long gasCost, int traceIndex)>();
            long nethCumulativeGas = 0;
            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                var op = trace.Instruction?.Instruction?.ToString() ?? "";

                if (op == "CALL" || op == "CALLCODE")
                {
                    var depth = trace.Depth + 1; // Nethereum depth is 0-indexed
                    var gasCost = (long)trace.GasCost;
                    nethCALLs.Add((depth, nethCumulativeGas, gasCost, i));
                }
                nethCumulativeGas += (long)trace.GasCost;
            }

            _output.WriteLine($"Geth CALLs: {gethCALLs.Count}");
            _output.WriteLine($"Nethereum CALLs: {nethCALLs.Count}");
            _output.WriteLine("");

            // Compare base CALL costs (not including allocated gas)
            // Geth gasCost = base_cost + allocated_gas
            // We can estimate base_cost from pattern: cold=2600+memory, warm=100+memory
            _output.WriteLine("=== CALL BASE COST COMPARISON ===");
            _output.WriteLine("Geth gasCost includes allocated gas. Neth gasCost is just the base cost.");
            _output.WriteLine("");
            _output.WriteLine("CALL# | Depth | Geth GasBefore | Geth TotalCost | Neth BaseCost | Est Geth Base");
            _output.WriteLine("------|-------|----------------|----------------|---------------|---------------");

            var maxCompare = Math.Min(gethCALLs.Count, nethCALLs.Count);
            long totalBaseCostDiff = 0;

            for (int i = 0; i < maxCompare; i++)
            {
                var geth = gethCALLs[i];
                var neth = nethCALLs[i];

                // Estimate geth base cost:
                // For first few calls (cold), base ~= 2600 + some memory
                // For later calls (warm), base ~= 100 + some memory
                // The allocated gas = gasCost - base
                long estimatedGethBase = i < 3 ? 2606L : 106L;  // Common values seen

                var baseCostDiff = neth.gasCost - estimatedGethBase;
                totalBaseCostDiff += baseCostDiff;

                if (i < 10 || i >= maxCompare - 10)
                {
                    _output.WriteLine($"{i + 1,5} | {geth.depth,5} | {geth.gasBefore,14} | {geth.gasCost,14} | {neth.gasCost,13} | {estimatedGethBase,13}");
                }
                else if (i == 10)
                {
                    _output.WriteLine("... (skipping middle) ...");
                }
            }

            _output.WriteLine("");
            _output.WriteLine("=== ANALYSIS ===");
            _output.WriteLine($"Total CALLs compared: {maxCompare}");
            _output.WriteLine("");

            // Track cumulative gas usage at matching points
            _output.WriteLine("=== CUMULATIVE GAS USAGE ===");
            _output.WriteLine("Tracking cumulative gas at each CALL to find where discrepancy grows.");
            _output.WriteLine("");

            // For geth, calculate cumulative gas from gasBefore differences
            // Initial gas - gasBefore at call N = cumulative gas used by call N
            if (gethCALLs.Count > 0)
            {
                var initialGas = gethCALLs[0].gasBefore; // Actually, we need to find the very first gas
                _output.WriteLine($"First CALL geth gas_before: {gethCALLs[0].gasBefore}");
                _output.WriteLine($"First CALL neth cumulative: {nethCALLs[0].cumulativeGas}");
                _output.WriteLine("");

                _output.WriteLine("CALL# | Geth Gas Used (inferred) | Neth Gas Used | Diff");
                _output.WriteLine("------|--------------------------|---------------|------");

                for (int i = 0; i < maxCompare; i++)
                {
                    var geth = gethCALLs[i];
                    var neth = nethCALLs[i];

                    // Geth gas used up to this point = first call's gasBefore - current gasBefore
                    var gethGasUsed = gethCALLs[0].gasBefore - geth.gasBefore;
                    var nethGasUsed = neth.cumulativeGas;
                    var diff = nethGasUsed - gethGasUsed;

                    if (i < 10 || i >= maxCompare - 10)
                    {
                        _output.WriteLine($"{i + 1,5} | {gethGasUsed,24} | {nethGasUsed,13} | {diff,+5}");
                    }
                    else if (i == 10)
                    {
                        _output.WriteLine("... (skipping middle) ...");
                    }
                }

                // Final comparison
                if (maxCompare > 0)
                {
                    var lastGeth = gethCALLs[maxCompare - 1];
                    var lastNeth = nethCALLs[maxCompare - 1];
                    var finalGethUsed = gethCALLs[0].gasBefore - lastGeth.gasBefore;
                    var finalNethUsed = lastNeth.cumulativeGas;
                    var finalDiff = finalNethUsed - finalGethUsed;

                    _output.WriteLine("");
                    _output.WriteLine($"At last matching CALL ({maxCompare}):");
                    _output.WriteLine($"  Geth cumulative gas: {finalGethUsed}");
                    _output.WriteLine($"  Neth cumulative gas: {finalNethUsed}");
                    _output.WriteLine($"  Difference: {finalDiff} (Neth - Geth)");
                    _output.WriteLine($"  Per-call average: {(double)finalDiff / maxCompare:F2} gas");
                }
            }

            // Now analyze gas allocation at each depth level
            _output.WriteLine("");
            _output.WriteLine("=== GAS ALLOCATION COMPARISON (63/64 Rule) ===");
            _output.WriteLine("Comparing gas allocated to subcalls.");
            _output.WriteLine("");
            _output.WriteLine("For geth: gasBefore at depth D+1 ≈ allocated from depth D");
            _output.WriteLine("We extract: geth_allocated = next_call.gasBefore");
            _output.WriteLine("          : geth_base_cost = gasCost - geth_allocated");
            _output.WriteLine("");
            _output.WriteLine("Depth | Geth GasBefore | Next GasBefore | Geth Allocated | Geth BaseCost | Neth BaseCost");
            _output.WriteLine("------|----------------|----------------|----------------|---------------|---------------");

            // Group geth CALLs by depth to understand the call chain
            var gethByDepth = gethCALLs.GroupBy(c => c.depth).ToDictionary(g => g.Key, g => g.ToList());
            var nethByDepth = nethCALLs.GroupBy(c => c.depth).ToDictionary(g => g.Key, g => g.ToList());

            for (int d = 1; d <= Math.Min(20, Math.Min(gethByDepth.Keys.Max(), nethByDepth.Keys.Max())); d++)
            {
                if (!gethByDepth.ContainsKey(d) || !gethByDepth.ContainsKey(d + 1)) continue;
                if (!nethByDepth.ContainsKey(d)) continue;

                var gethThis = gethByDepth[d].First();
                var gethNext = gethByDepth[d + 1].First();
                var nethThis = nethByDepth[d].First();

                var gethAllocated = gethNext.gasBefore; // Gas given to subcall
                var gethBaseCost = gethThis.gasCost - gethAllocated;

                _output.WriteLine($"{d,5} | {gethThis.gasBefore,14} | {gethNext.gasBefore,14} | {gethAllocated,14} | {gethBaseCost,13} | {nethThis.gasCost,13}");
            }

            // Check if there's a pattern in the gas allocation differences
            _output.WriteLine("");
            _output.WriteLine("=== DETAILED DEPTH ANALYSIS ===");
            _output.WriteLine("Checking if the 63/64 calculation produces different results.");
            _output.WriteLine("");

            for (int d = 3; d <= Math.Min(10, nethByDepth.Keys.Max()); d++)
            {
                if (!gethByDepth.ContainsKey(d) || !nethByDepth.ContainsKey(d)) continue;

                var gethCall = gethByDepth[d].First();
                var nethCall = nethByDepth[d].First();

                // Simulate what Nethereum's allocation would be
                // After CALL opcode cost is paid, remaining = gasBefore - baseCost
                // Then allocate = remaining - remaining/64
                var baseCost = d <= 2 ? 2606L : 106L;
                var gethRemaining = gethCall.gasBefore - baseCost;
                var simulated64th = gethRemaining / 64;
                var simulatedAlloc = gethRemaining - simulated64th;

                var nextGeth = gethByDepth.ContainsKey(d + 1) ? gethByDepth[d + 1].First().gasBefore : 0;

                _output.WriteLine($"Depth {d}: gasBefore={gethCall.gasBefore}, baseCost={baseCost}");
                _output.WriteLine($"  After base: remaining={gethRemaining}");
                _output.WriteLine($"  63/64 rule: {gethRemaining} - {gethRemaining}/64 = {gethRemaining} - {simulated64th} = {simulatedAlloc}");
                _output.WriteLine($"  Geth actual next level: {nextGeth}");
                _output.WriteLine($"  Difference: {nextGeth - simulatedAlloc}");
                _output.WriteLine("");
            }
        }

        private async Task CompareCALLGasAsync(string categoryName, string testFileName)
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, categoryName, $"{testFileName}.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            _output.WriteLine("Comparing CALL gas allocation between geth and Nethereum...\n");

            var gethTraceLines = await RunGethTraceAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            _output.WriteLine("=== CALL Operations - Geth Gas Flow ===");
            _output.WriteLine("Geth shows 'gas before' at CALL, and 'gasCost' includes allocated gas.");
            _output.WriteLine("Next depth shows subcall starting gas.\n");

            var gethIdx = 0;
            var callCount = 0;
            var maxCalls = 280;  // Show enough to see the problem area (~depth 267)

            long lastCallGasBefore = 0;
            long lastCallGasCost = 0;

            while (gethIdx < gethTraceLines.Count && callCount < maxCalls)
            {
                var gethLine = gethTraceLines[gethIdx];

                if (!gethLine.StartsWith("{") || !gethLine.Contains("\"opName\""))
                {
                    gethIdx++;
                    continue;
                }

                var gethOp = ParseGethField(gethLine, "opName").Trim('"');
                var gethDepth = int.Parse(ParseGethField(gethLine, "depth"));
                var gethGas = long.Parse(ParseGethHexField(gethLine, "gas"));
                var gethGasCost = long.Parse(ParseGethHexField(gethLine, "gasCost"));

                if (gethOp == "CALL" || gethOp == "CALLCODE")
                {
                    callCount++;
                    if (callCount > maxCalls - 20 || callCount <= 5)  // Show first 5 and last 20
                    {
                        var baseCost = gethDepth <= 2 ? 2606L : 106L;  // Cold vs warm access
                        var gasAllocated = gethGasCost - baseCost;
                        var gasRetained = gethGas - gethGasCost;
                        _output.WriteLine($"CALL #{callCount}: depth={gethDepth}, gas_before={gethGas}, gasCost={gethGasCost} (base={baseCost}, alloc={gasAllocated}), retained={gasRetained}");
                    }
                    else if (callCount == 6)
                    {
                        _output.WriteLine("  ... skipping middle calls ...");
                    }
                    lastCallGasBefore = gethGas;
                    lastCallGasCost = gethGasCost;
                }

                gethIdx++;
            }

            _output.WriteLine($"\nTotal CALLs in geth trace: {callCount}");
            _output.WriteLine($"Nethereum traces: {nethTraces.Count}");
            _output.WriteLine($"Last geth CALL: gas_before={lastCallGasBefore}, gasCost={lastCallGasCost}");
        }

        private async Task CompareTracesAsync(string categoryName, string testFileName)
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, categoryName, $"{testFileName}.json");
            if (!File.Exists(testFile))
            {
                _output.WriteLine($"Test file not found: {testFile}");
                Assert.True(false, $"Test file not found: {testFile}");
                return;
            }

            // Find geth evm.exe
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");
            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found at: {gethEvmPath}");
                _output.WriteLine("Run: geth tools should be in geth-tools folder");
                Assert.True(false, "geth evm.exe not found");
                return;
            }

            // Get geth trace
            _output.WriteLine("Running geth trace...");
            var gethTraceLines = await RunGethTraceAsync(gethEvmPath, testFile);
            _output.WriteLine($"Geth trace: {gethTraceLines.Count} lines");

            // Get Nethereum trace
            _output.WriteLine("Running Nethereum trace...");
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();
            _output.WriteLine($"Nethereum trace: {nethTraces.Count} lines");

            // Compare traces
            _output.WriteLine("");
            _output.WriteLine("=== TRACE COMPARISON ===");
            _output.WriteLine("Note: Geth depth starts at 1, Nethereum at 0 (offset by 1)");
            _output.WriteLine("");

            var gethIdx = 0;
            var nethIdx = 0;
            var diffCount = 0;
            var maxDiffs = 50;
            var matchCount = 0;

            // Calculate total gas from each trace
            long gethTotalGas = 0;
            long nethTotalGas = 0;

            while (gethIdx < gethTraceLines.Count && nethIdx < nethTraces.Count)
            {
                var gethLine = gethTraceLines[gethIdx];
                var nethTrace = nethTraces[nethIdx];

                // Parse geth line
                if (!gethLine.StartsWith("{")) { gethIdx++; continue; }
                if (!gethLine.Contains("\"opName\"")) { gethIdx++; continue; }

                var gethOp = ParseGethField(gethLine, "opName").Trim('"');
                var gethPc = ParseGethField(gethLine, "pc");
                var gethGas = ParseGethHexField(gethLine, "gas");
                var gethGasCost = ParseGethHexField(gethLine, "gasCost");
                var gethDepth = ParseGethField(gethLine, "depth");

                var nethOp = nethTrace.Instruction?.Instruction?.ToString() ?? "?";
                var nethPc = nethTrace.Instruction?.Step.ToString() ?? "?";
                var nethDepth = nethTrace.Depth.ToString(); // Adjust for depth offset
                var nethGasCost = nethTrace.GasCost.ToString();

                // Track gas totals (excluding CALL/CREATE gas allocation which geth includes)
                if (!gethOp.Contains("CALL") && !gethOp.Contains("CREATE"))
                {
                    if (long.TryParse(gethGasCost, out var gGas)) gethTotalGas += gGas;
                    nethTotalGas += (long)nethTrace.GasCost;
                }

                // Compare (now with depth offset adjusted)
                var pcMatch = gethPc == nethPc;
                var depthMatch = gethDepth == nethDepth;
                var opMatch = gethOp.ToUpper() == nethOp.ToUpper();

                // For CALL/CALLCODE/etc, geth includes allocated gas in gasCost, we don't
                var isCallOp = gethOp.Contains("CALL") || gethOp.Contains("CREATE");
                var gasCostMatch = isCallOp || gethGasCost == nethGasCost;

                if (!pcMatch || !depthMatch || !opMatch || !gasCostMatch)
                {
                    if (diffCount < maxDiffs)
                    {
                        diffCount++;
                        _output.WriteLine($"DIFF #{diffCount} at step geth={gethIdx} neth={nethIdx}:");
                        _output.WriteLine($"  GETH:  pc={gethPc} depth={gethDepth} op={gethOp} gas={gethGas} gasCost={gethGasCost}");
                        _output.WriteLine($"  NETH:  pc={nethPc} depth={nethDepth} op={nethOp} gasCost={nethGasCost}");
                        if (!opMatch) _output.WriteLine($"  -> OPCODE MISMATCH!");
                        if (!pcMatch) _output.WriteLine($"  -> PC MISMATCH!");
                        if (!depthMatch) _output.WriteLine($"  -> DEPTH MISMATCH!");
                        if (!gasCostMatch) _output.WriteLine($"  -> GAS COST MISMATCH! diff={long.Parse(gethGasCost) - (long)nethTrace.GasCost}");
                    }
                    else
                    {
                        diffCount++;
                    }
                }
                else
                {
                    matchCount++;
                }

                gethIdx++;
                nethIdx++;
            }

            _output.WriteLine("");
            _output.WriteLine("=== SUMMARY ===");
            _output.WriteLine($"Total matches: {matchCount}");
            _output.WriteLine($"Total differences: {diffCount}");
            _output.WriteLine($"Geth trace lines: {gethTraceLines.Count}");
            _output.WriteLine($"Nethereum traces: {nethTraces.Count}");
            _output.WriteLine($"Geth trace lines processed: {gethIdx}");
            _output.WriteLine($"Nethereum traces processed: {nethIdx}");
            _output.WriteLine($"Geth total gas (excl CALL alloc): {gethTotalGas}");
            _output.WriteLine($"Nethereum total gas (excl CALL alloc): {nethTotalGas}");
            _output.WriteLine($"Gas difference: {gethTotalGas - nethTotalGas}");

            if (gethTraceLines.Count != nethTraces.Count)
            {
                _output.WriteLine("");
                _output.WriteLine($"WARNING: Trace count mismatch! Geth={gethTraceLines.Count}, Neth={nethTraces.Count}");
                _output.WriteLine("This indicates different execution paths or trace collection issues.");
            }
        }

        private async Task<System.Collections.Generic.List<string>> RunGethTraceAsync(string gethEvmPath, string testFile)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = gethEvmPath,
                Arguments = $"--json statetest \"{testFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var lines = new System.Collections.Generic.List<string>();
            var errorLines = new System.Collections.Generic.List<string>();

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                var stdoutTask = Task.Run(async () =>
                {
                    string line;
                    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                    {
                        lines.Add(line);
                    }
                });

                var stderrTask = Task.Run(async () =>
                {
                    string line;
                    while ((line = await process.StandardError.ReadLineAsync()) != null)
                    {
                        errorLines.Add(line);
                    }
                });

                await Task.WhenAll(stdoutTask, stderrTask);
                await process.WaitForExitAsync();
            }

            // geth outputs trace to stderr and summary to stdout
            // Return stderr (trace) as primary output
            if (errorLines.Count > 0)
            {
                return errorLines;
            }
            return lines;
        }

        private string ParseGethField(string json, string field)
        {
            var pattern = $"\"{field}\":";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return "?";
            var start = idx + pattern.Length;
            var end = json.IndexOfAny(new[] { ',', '}' }, start);
            if (end < 0) return "?";
            return json.Substring(start, end - start).Trim();
        }

        private string ParseGethHexField(string json, string field)
        {
            var hexValue = ParseGethField(json, field).Trim('"');
            if (hexValue.StartsWith("0x"))
            {
                try
                {
                    var value = Convert.ToInt64(hexValue, 16);
                    return value.ToString();
                }
                catch
                {
                    return hexValue;
                }
            }
            return hexValue;
        }

        private async Task<System.Collections.Generic.List<string>> RunGethTraceWithMemoryAsync(string gethEvmPath, string testFile)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = gethEvmPath,
                Arguments = $"--json --nomemory=false statetest \"{testFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var lines = new System.Collections.Generic.List<string>();
            var errorLines = new System.Collections.Generic.List<string>();

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                var stdoutTask = Task.Run(async () =>
                {
                    string line;
                    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                    {
                        lines.Add(line);
                    }
                });

                var stderrTask = Task.Run(async () =>
                {
                    string line;
                    while ((line = await process.StandardError.ReadLineAsync()) != null)
                    {
                        errorLines.Add(line);
                    }
                });

                await Task.WhenAll(stdoutTask, stderrTask);
                await process.WaitForExitAsync();
            }

            if (errorLines.Count > 0)
            {
                return errorLines;
            }
            return lines;
        }

        private int ParseGethMemorySize(string json)
        {
            var pattern = "\"memory\":";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return 0;

            var arrayStart = json.IndexOf('[', idx);
            if (arrayStart < 0) return 0;

            var arrayEnd = json.IndexOf(']', arrayStart);
            if (arrayEnd < 0) return 0;

            var arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1).Trim();
            if (string.IsNullOrEmpty(arrayContent)) return 0;

            var elements = arrayContent.Split(',');
            int totalBytes = 0;
            foreach (var elem in elements)
            {
                var hex = elem.Trim().Trim('"');
                if (hex.StartsWith("0x"))
                    totalBytes += (hex.Length - 2) / 2;
            }
            return totalBytes;
        }

        [Fact]
        public async Task CompareTraces_FullOpcodeWithMemory()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found at: {gethEvmPath}");
                return;
            }

            _output.WriteLine("=== FULL OPCODE COMPARISON WITH MEMORY SIZE ===");
            _output.WriteLine("Running geth with --nomemory=false to include memory in trace...");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            _output.WriteLine($"Geth trace lines: {gethTraceLines.Count}");

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();
            _output.WriteLine($"Nethereum traces: {nethTraces.Count}");
            _output.WriteLine("");

            _output.WriteLine("Step | Op        | Depth | PC   | GasCost | MemSize | Source");
            _output.WriteLine("-----|-----------|-------|------|---------|---------|--------");

            int gethIdx = 0;
            int nethIdx = 0;
            int stepNum = 0;
            int mismatchCount = 0;
            int maxSteps = 200;

            while ((gethIdx < gethTraceLines.Count || nethIdx < nethTraces.Count) && stepNum < maxSteps)
            {
                string gethOp = "", nethOp = "";
                string gethPc = "", nethPc = "";
                string gethDepth = "", nethDepth = "";
                string gethGasCost = "", nethGasCost = "";
                int gethMemSize = 0, nethMemSize = 0;
                bool gethValid = false, nethValid = false;

                while (gethIdx < gethTraceLines.Count)
                {
                    var line = gethTraceLines[gethIdx];
                    if (line.StartsWith("{") && line.Contains("\"opName\""))
                    {
                        gethOp = ParseGethField(line, "opName").Trim('"');
                        gethPc = ParseGethField(line, "pc");
                        gethDepth = ParseGethField(line, "depth");
                        gethGasCost = ParseGethHexField(line, "gasCost");
                        gethMemSize = ParseGethMemorySize(line);
                        gethValid = true;
                        gethIdx++;
                        break;
                    }
                    gethIdx++;
                }

                if (nethIdx < nethTraces.Count)
                {
                    var trace = nethTraces[nethIdx];
                    nethOp = trace.Instruction?.Instruction?.ToString() ?? "?";
                    nethPc = trace.Instruction?.Step.ToString() ?? "?";
                    nethDepth = trace.Depth.ToString();
                    nethGasCost = trace.GasCost.ToString();
                    nethMemSize = !string.IsNullOrEmpty(trace.Memory) ? trace.Memory.Length / 2 : 0;
                    nethValid = true;
                    nethIdx++;
                }

                stepNum++;

                bool opMatch = gethOp.ToUpper() == nethOp.ToUpper();
                bool pcMatch = gethPc == nethPc;
                bool depthMatch = gethDepth == nethDepth;
                bool isCallOp = gethOp.Contains("CALL") || gethOp.Contains("CREATE");
                bool gasCostMatch = isCallOp || gethGasCost == nethGasCost;
                bool memSizeMatch = gethMemSize == nethMemSize;

                bool anyMismatch = !opMatch || !pcMatch || !depthMatch || !gasCostMatch || !memSizeMatch;

                if (anyMismatch || stepNum <= 30 || (stepNum % 50 == 0))
                {
                    if (gethValid)
                        _output.WriteLine($"{stepNum,4} | {gethOp,-9} | {gethDepth,5} | {gethPc,4} | {gethGasCost,7} | {gethMemSize,7} | GETH");
                    if (nethValid)
                        _output.WriteLine($"{stepNum,4} | {nethOp,-9} | {nethDepth,5} | {nethPc,4} | {nethGasCost,7} | {nethMemSize,7} | NETH");

                    if (anyMismatch)
                    {
                        mismatchCount++;
                        var issues = new System.Collections.Generic.List<string>();
                        if (!opMatch) issues.Add("OP");
                        if (!pcMatch) issues.Add("PC");
                        if (!depthMatch) issues.Add("DEPTH");
                        if (!gasCostMatch) issues.Add($"GAS({gethGasCost} vs {nethGasCost})");
                        if (!memSizeMatch) issues.Add($"MEM({gethMemSize} vs {nethMemSize})");
                        _output.WriteLine($"     -> MISMATCH: {string.Join(", ", issues)}");
                    }
                    _output.WriteLine("");
                }
            }

            _output.WriteLine($"=== SUMMARY: {mismatchCount} mismatches in first {stepNum} steps ===");
        }

        [Fact]
        public async Task CompareTraces_CALLStackParameters()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found");
                return;
            }

            _output.WriteLine("=== CALL STACK PARAMETERS ANALYSIS ===");
            _output.WriteLine("Examining stack values at each CALL to understand gas calculation.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            _output.WriteLine("Geth CALL operations with stack:");
            _output.WriteLine("CALL# | Depth | GasCost | Stack[0..6] (gas, to, value, inOff, inSz, outOff, outSz)");
            _output.WriteLine("------|-------|---------|------------------------------------------------------------");

            int callNum = 0;
            for (int i = 0; i < gethTraceLines.Count && callNum < 10; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                var op = ParseGethField(line, "opName").Trim('"');
                if (op == "CALL")
                {
                    callNum++;
                    var depth = ParseGethField(line, "depth");
                    var gasCost = ParseGethHexField(line, "gasCost");
                    var stack = ParseGethStack(line);

                    _output.WriteLine($"{callNum,5} | {depth,5} | {gasCost,7} | {string.Join(", ", stack.Take(7))}");
                }
            }

            _output.WriteLine("");
            _output.WriteLine("Nethereum CALL operations with stack:");
            _output.WriteLine("CALL# | Depth | GasCost | Stack[0..6] (gas, to, value, inOff, inSz, outOff, outSz)");
            _output.WriteLine("------|-------|---------|------------------------------------------------------------");

            callNum = 0;
            for (int i = 0; i < nethTraces.Count && callNum < 10; i++)
            {
                var trace = nethTraces[i];
                var op = trace.Instruction?.Instruction?.ToString() ?? "";
                if (op == "CALL")
                {
                    callNum++;
                    var depth = trace.Depth + 1;
                    var gasCost = trace.GasCost;
                    var stack = trace.Stack ?? new System.Collections.Generic.List<string>();

                    var stackTop7 = stack.Skip(Math.Max(0, stack.Count - 7)).Take(7).Reverse().ToList();
                    var stackStr = string.Join(", ", stackTop7.Select(s =>
                    {
                        if (s.Length > 16) return s.Substring(0, 8) + "..";
                        return s;
                    }));

                    _output.WriteLine($"{callNum,5} | {depth,5} | {gasCost,7} | {stackStr}");
                }
            }

            _output.WriteLine("");
            _output.WriteLine("=== ANALYSIS ===");
            _output.WriteLine("CALL stack order (top to bottom): gas, to, value, inOffset, inSize, outOffset, outSize");
            _output.WriteLine("Memory expansion = max(inOffset+inSize, outOffset+outSize)");
            _output.WriteLine("");
            _output.WriteLine("With inOff=0, inSz=64, outOff=0, outSz=64:");
            _output.WriteLine("  max = 64 bytes = 2 words");
            _output.WriteLine("  word_cost(2) = (2*2/512) + (3*2) = 0 + 6 = 6 gas");
            _output.WriteLine("");
            _output.WriteLine("But geth charges 27 gas for memory (127 - 100 warm access = 27)");
            _output.WriteLine("  27 gas = word_cost(9) - word_cost(0) = (81/512) + (3*9) = 0 + 27 = 27");
            _output.WriteLine("  9 words = 288 bytes");
            _output.WriteLine("");
            _output.WriteLine("Question: Where does 288 bytes come from?");
        }

        private System.Collections.Generic.List<string> ParseGethStack(string json)
        {
            var result = new System.Collections.Generic.List<string>();
            var pattern = "\"stack\":";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return result;

            var arrayStart = json.IndexOf('[', idx);
            if (arrayStart < 0) return result;

            var arrayEnd = json.IndexOf(']', arrayStart);
            if (arrayEnd < 0) return result;

            var arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1).Trim();
            if (string.IsNullOrEmpty(arrayContent)) return result;

            var elements = arrayContent.Split(',');
            foreach (var elem in elements)
            {
                var hex = elem.Trim().Trim('"');
                if (hex.StartsWith("0x"))
                {
                    try
                    {
                        var value = Convert.ToInt64(hex, 16);
                        result.Add(value.ToString());
                    }
                    catch
                    {
                        result.Add(hex.Substring(0, Math.Min(hex.Length, 10)));
                    }
                }
            }
            return result;
        }

        [Fact]
        public async Task Compare_GethNeth_AllOpGasCosts()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== COMPARE ALL OP GAS COSTS ===");
            _output.WriteLine("Looking for any opcode where geth and Nethereum charge different gas.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            var gethOpGas = new System.Collections.Generic.Dictionary<string, (long total, int count)>();
            for (int i = 0; i < gethTraceLines.Count; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;
                var op = ParseGethField(line, "opName").Trim('"');
                var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));

                if (op.Contains("CALL") || op.Contains("CREATE")) continue;

                if (!gethOpGas.ContainsKey(op))
                    gethOpGas[op] = (0, 0);
                var current = gethOpGas[op];
                gethOpGas[op] = (current.total + gasCost, current.count + 1);
            }

            var nethOpGas = new System.Collections.Generic.Dictionary<string, (long total, int count)>();
            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                var op = trace.Instruction?.Instruction?.ToString() ?? "";
                var gasCost = (long)trace.GasCost;

                if (op.Contains("CALL") || op.Contains("CREATE")) continue;

                if (!nethOpGas.ContainsKey(op))
                    nethOpGas[op] = (0, 0);
                var current = nethOpGas[op];
                nethOpGas[op] = (current.total + gasCost, current.count + 1);
            }

            _output.WriteLine("Op       | Geth Count | Geth Avg | Neth Count | Neth Avg | Match");
            _output.WriteLine("---------|------------|----------|------------|----------|-------");

            foreach (var op in gethOpGas.Keys.OrderBy(k => k))
            {
                var geth = gethOpGas[op];
                var gethAvg = geth.count > 0 ? (double)geth.total / geth.count : 0;

                if (nethOpGas.ContainsKey(op))
                {
                    var neth = nethOpGas[op];
                    var nethAvg = neth.count > 0 ? (double)neth.total / neth.count : 0;
                    var match = Math.Abs(gethAvg - nethAvg) < 0.001 ? "YES" : $"NO ({gethAvg - nethAvg:F1})";
                    _output.WriteLine($"{op,-8} | {geth.count,10} | {gethAvg,8:F1} | {neth.count,10} | {nethAvg,8:F1} | {match}");
                }
                else
                {
                    _output.WriteLine($"{op,-8} | {geth.count,10} | {gethAvg,8:F1} | {0,10} | {"N/A",8} | MISSING");
                }
            }

            _output.WriteLine("");
            _output.WriteLine("Ops in Nethereum but not Geth:");
            foreach (var op in nethOpGas.Keys.Where(k => !gethOpGas.ContainsKey(k)))
            {
                var neth = nethOpGas[op];
                _output.WriteLine($"  {op}: count={neth.count}, avg={neth.total / (double)neth.count:F1}");
            }
        }

        [Fact]
        public async Task Compare_GethNeth_GasAllocation()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== GETH vs NETHEREUM GAS ALLOCATION COMPARISON ===");
            _output.WriteLine("Testing whether both implementations produce the same gas allocation.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            var gethCALLInfo = new System.Collections.Generic.List<(int depth, long baseCost, long allocated)>();
            for (int i = 0; i < gethTraceLines.Count - 1; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;
                var op = ParseGethField(line, "opName").Trim('"');
                if (op != "CALL") continue;

                var depth = int.Parse(ParseGethField(line, "depth"));
                var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));

                long childGas = 0;
                for (int j = i + 1; j < Math.Min(i + 5, gethTraceLines.Count); j++)
                {
                    var nextLine = gethTraceLines[j];
                    if (!nextLine.StartsWith("{") || !nextLine.Contains("\"opName\"")) continue;
                    var nextDepth = int.Parse(ParseGethField(nextLine, "depth"));
                    if (nextDepth == depth + 1)
                    {
                        childGas = long.Parse(ParseGethHexField(nextLine, "gas"));
                        break;
                    }
                }

                gethCALLInfo.Add((depth, gasCost - childGas, childGas));
            }

            var nethCALLInfo = new System.Collections.Generic.List<(int depth, long baseCost, long allocated)>();
            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                var op = trace.Instruction?.Instruction?.ToString() ?? "";
                if (op != "CALL") continue;

                var depth = trace.Depth + 1;
                var baseCost = (long)trace.GasCost;

                long allocated = 0;
                for (int j = i + 1; j < Math.Min(i + 5, nethTraces.Count); j++)
                {
                    if (nethTraces[j].Depth == trace.Depth + 1)
                    {
                        break;
                    }
                }

                nethCALLInfo.Add((depth, baseCost, 0));
            }

            _output.WriteLine("CALL# | Depth | Geth Base | Neth Base | Base Match | Geth Alloc");
            _output.WriteLine("------|-------|-----------|-----------|------------|------------");

            var maxCompare = Math.Min(gethCALLInfo.Count, nethCALLInfo.Count);
            int baseMatches = 0;
            for (int i = 0; i < Math.Min(20, maxCompare); i++)
            {
                var geth = gethCALLInfo[i];
                var neth = nethCALLInfo[i];
                var match = geth.baseCost == neth.baseCost ? "YES" : $"NO ({geth.baseCost - neth.baseCost})";
                if (geth.baseCost == neth.baseCost) baseMatches++;
                _output.WriteLine($"{i + 1,5} | {geth.depth,5} | {geth.baseCost,9} | {neth.baseCost,9} | {match,10} | {geth.allocated,10}");
            }

            _output.WriteLine("");
            _output.WriteLine($"Total CALLs compared: {maxCompare}");
            _output.WriteLine($"Base cost matches: {baseMatches}/{Math.Min(20, maxCompare)} in first 20");
            _output.WriteLine($"Geth total CALLs: {gethCALLInfo.Count}");
            _output.WriteLine($"Nethereum total CALLs: {nethCALLInfo.Count}");

            if (gethCALLInfo.Count != nethCALLInfo.Count)
            {
                _output.WriteLine("");
                _output.WriteLine($"CALL COUNT MISMATCH: Geth={gethCALLInfo.Count}, Neth={nethCALLInfo.Count}");
                _output.WriteLine("This indicates different recursion depths due to cumulative gas differences.");
            }
        }

        [Fact]
        public async Task Debug_GethCALLGasBreakdown()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== GETH CALL GAS BREAKDOWN ===");
            _output.WriteLine("Examining consecutive trace lines around each CALL to understand gas allocation.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);

            _output.WriteLine("Looking at CALL and the FIRST op of child context:");
            _output.WriteLine("");
            _output.WriteLine("Format: [CALL at depth D] -> [first op at depth D+1]");
            _output.WriteLine("  gasCost = base_cost + allocated_gas");
            _output.WriteLine("  allocated_gas should equal child's gasBefore");
            _output.WriteLine("");

            int callNum = 0;
            for (int i = 0; i < gethTraceLines.Count - 1 && callNum < 15; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                var op = ParseGethField(line, "opName").Trim('"');
                if (op != "CALL") continue;

                callNum++;
                var depth = ParseGethField(line, "depth");
                var gasBefore = ParseGethHexField(line, "gas");
                var gasCost = ParseGethHexField(line, "gasCost");
                var stack = ParseGethStack(line);

                var gasRequested = stack.Count >= 7 ? stack[6] : "?";

                int nextDepth = int.Parse(depth);
                string childGasBefore = "?";
                string childOp = "?";

                for (int j = i + 1; j < Math.Min(i + 5, gethTraceLines.Count); j++)
                {
                    var nextLine = gethTraceLines[j];
                    if (!nextLine.StartsWith("{") || !nextLine.Contains("\"opName\"")) continue;

                    var nextLineDepth = int.Parse(ParseGethField(nextLine, "depth"));
                    if (nextLineDepth == nextDepth + 1)
                    {
                        childGasBefore = ParseGethHexField(nextLine, "gas");
                        childOp = ParseGethField(nextLine, "opName").Trim('"');
                        break;
                    }
                }

                long gasCostVal = long.Parse(gasCost);
                long childGasVal = childGasBefore != "?" ? long.Parse(childGasBefore) : 0;
                long baseCost = gasCostVal - childGasVal;
                long gasReq = long.TryParse(gasRequested, out var gr) ? gr : 0;
                long allocationDiff = gasReq - childGasVal;

                _output.WriteLine($"CALL #{callNum}: depth={depth}");
                _output.WriteLine($"  gasBefore={gasBefore}, gasCost={gasCost}, gasRequested={gasRequested}");
                _output.WriteLine($"  Child: firstOp={childOp}, gasBefore={childGasBefore}");
                _output.WriteLine($"  Derived: baseCost={baseCost}, allocationDiff={allocationDiff}");
                _output.WriteLine($"    (baseCost = gasCost - childGasBefore = {gasCost} - {childGasBefore})");
                _output.WriteLine($"    (allocationDiff = requested - allocated = {gasRequested} - {childGasBefore})");
                _output.WriteLine("");
            }
        }

        [Fact]
        public async Task CompareTraces_CALLMemoryExpansion()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found");
                return;
            }

            _output.WriteLine("=== CALL MEMORY EXPANSION ANALYSIS ===");
            _output.WriteLine("Comparing memory size BEFORE and AFTER each CALL to understand gas calculation.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            _output.WriteLine("Geth CALL operations with memory context:");
            _output.WriteLine("CALL# | Depth | MemBefore | MemAfter | GasCost | Memory Gas Portion");
            _output.WriteLine("------|-------|-----------|----------|---------|--------------------");

            int callNum = 0;
            int prevMemSize = 0;
            for (int i = 0; i < gethTraceLines.Count; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                var op = ParseGethField(line, "opName").Trim('"');
                var memSize = ParseGethMemorySize(line);
                var depth = ParseGethField(line, "depth");
                var gasCost = ParseGethHexField(line, "gasCost");

                if (op == "CALL" || op == "CALLCODE")
                {
                    callNum++;
                    int memAfter = 0;
                    if (i + 1 < gethTraceLines.Count)
                    {
                        var nextLine = gethTraceLines[i + 1];
                        if (nextLine.StartsWith("{"))
                            memAfter = ParseGethMemorySize(nextLine);
                    }

                    int memWords = (memSize + 31) / 32;
                    int memGas = (memWords * memWords / 512) + (3 * memWords);

                    if (callNum <= 20)
                    {
                        _output.WriteLine($"{callNum,5} | {depth,5} | {memSize,9} | {memAfter,8} | {gasCost,7} | words={memWords}, gas={memGas}");
                    }
                }

                prevMemSize = memSize;
            }

            _output.WriteLine("");
            _output.WriteLine("Nethereum CALL operations with memory context:");
            _output.WriteLine("CALL# | Depth | MemSize | GasCost | Memory Gas Portion");
            _output.WriteLine("------|-------|---------|---------|--------------------");

            callNum = 0;
            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                var op = trace.Instruction?.Instruction?.ToString() ?? "";
                if (op == "CALL" || op == "CALLCODE")
                {
                    callNum++;
                    int memSize = !string.IsNullOrEmpty(trace.Memory) ? trace.Memory.Length / 2 : 0;
                    int memWords = (memSize + 31) / 32;
                    int memGas = (memWords * memWords / 512) + (3 * memWords);
                    var depth = trace.Depth + 1;
                    var gasCost = trace.GasCost;

                    if (callNum <= 20)
                    {
                        _output.WriteLine($"{callNum,5} | {depth,5} | {memSize,7} | {gasCost,7} | words={memWords}, gas={memGas}");
                    }
                }
            }

            _output.WriteLine("");
            _output.WriteLine("=== KEY INSIGHT ===");
            _output.WriteLine("The memory expansion gas for CALL depends on:");
            _output.WriteLine("1. Current memory size (curMem)");
            _output.WriteLine("2. max(inOffset+inSize, outOffset+outSize) - the highest byte accessed");
            _output.WriteLine("3. Memory gas = word_cost(new_words) - word_cost(old_words)");
            _output.WriteLine("");
            _output.WriteLine("If geth charges 27 gas (9 words = 288 bytes) but Nethereum charges 6 gas (2 words = 64 bytes),");
            _output.WriteLine("either: (a) geth's memory is already expanded from prior ops, or");
            _output.WriteLine("        (b) the memory parameters on stack differ between implementations.");
        }

        [Fact]
        public async Task Debug_SSTORE_GasComparison()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== SSTORE GAS COMPARISON ===");
            _output.WriteLine("Comparing individual SSTORE gas costs between geth and Nethereum.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            _output.WriteLine("=== GETH SSTORE OPERATIONS (first 30) ===");
            _output.WriteLine("# | Depth | GasBefore | GasCost | Stack[0] (slot) | Stack[1] (value)");
            _output.WriteLine("--|-------|-----------|---------|-----------------|------------------");

            int sstoreNum = 0;
            var gethSstoreCosts = new System.Collections.Generic.List<long>();
            for (int i = 0; i < gethTraceLines.Count && sstoreNum < 30; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                var op = ParseGethField(line, "opName").Trim('"');
                if (op != "SSTORE") continue;

                sstoreNum++;
                var depth = ParseGethField(line, "depth");
                var gasBefore = ParseGethHexField(line, "gas");
                var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));
                var stack = ParseGethStack(line);

                gethSstoreCosts.Add(gasCost);

                var slot = stack.Count >= 1 ? stack[0] : "?";
                var value = stack.Count >= 2 ? stack[1] : "?";

                if (slot.Length > 10) slot = slot.Substring(0, 10) + "...";
                if (value.Length > 10) value = value.Substring(0, 10) + "...";

                _output.WriteLine($"{sstoreNum,2} | {depth,5} | {gasBefore,9} | {gasCost,7} | {slot,15} | {value}");
            }

            _output.WriteLine("");
            _output.WriteLine("=== NETHEREUM SSTORE OPERATIONS (first 30) ===");
            _output.WriteLine("# | Depth | GasCost | Additional Debug Info");
            _output.WriteLine("--|-------|---------|------------------------");

            sstoreNum = 0;
            var nethSstoreCosts = new System.Collections.Generic.List<long>();
            for (int i = 0; i < nethTraces.Count && sstoreNum < 30; i++)
            {
                var trace = nethTraces[i];
                var op = trace.Instruction?.Instruction?.ToString() ?? "";
                if (op != "SSTORE") continue;

                sstoreNum++;
                var gasCost = (long)trace.GasCost;
                nethSstoreCosts.Add(gasCost);

                _output.WriteLine($"{sstoreNum,2} | {trace.Depth + 1,5} | {gasCost,7} |");
            }

            _output.WriteLine("");
            _output.WriteLine("=== SUMMARY ===");
            _output.WriteLine($"Geth SSTORE count: {gethSstoreCosts.Count}");
            _output.WriteLine($"Nethereum SSTORE count: {nethSstoreCosts.Count}");

            if (gethSstoreCosts.Count > 0)
            {
                var gethAvg = gethSstoreCosts.Average();
                var gethMin = gethSstoreCosts.Min();
                var gethMax = gethSstoreCosts.Max();
                _output.WriteLine($"Geth: min={gethMin}, max={gethMax}, avg={gethAvg:F1}");
            }

            if (nethSstoreCosts.Count > 0)
            {
                var nethAvg = nethSstoreCosts.Average();
                var nethMin = nethSstoreCosts.Min();
                var nethMax = nethSstoreCosts.Max();
                _output.WriteLine($"Nethereum: min={nethMin}, max={nethMax}, avg={nethAvg:F1}");
            }

            _output.WriteLine("");
            _output.WriteLine("=== SSTORE GAS FORMULA (EIP-2929/EIP-2200) ===");
            _output.WriteLine("Cold access: +2100 (COLD_SLOAD_COST)");
            _output.WriteLine("SSTORE_NOOP (same value): +100");
            _output.WriteLine("SSTORE_SET (orig=0, new!=0): +20000");
            _output.WriteLine("SSTORE_RESET (orig!=0, new!=0): +2900");
            _output.WriteLine("");
            _output.WriteLine("Expected costs:");
            _output.WriteLine("  Cold + NOOP = 2200");
            _output.WriteLine("  Warm + NOOP = 100");
            _output.WriteLine("  Cold + SET  = 22100");
            _output.WriteLine("  Warm + SET  = 20000");
            _output.WriteLine("  Cold + RESET = 5000");
            _output.WriteLine("  Warm + RESET = 2900");
        }

        [Fact]
        public async Task Debug_SSTORE_ForwardPhaseOnly()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== SSTORE GAS COMPARISON (Forward Phase Only) ===");
            _output.WriteLine("Looking at SSTORE operations BEFORE the OOG unwind (depth increasing).");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);

            _output.WriteLine("=== GETH FORWARD PHASE SSTOREs ===");
            _output.WriteLine("# | Depth | PrevDepth | GasBefore | GasCost | Direction");
            _output.WriteLine("--|-------|-----------|-----------|---------|----------");

            int prevDepth = 0;
            int maxDepthReached = 0;
            int forwardSstoreCount = 0;
            var gethForwardSstoreCosts = new System.Collections.Generic.List<long>();

            for (int i = 0; i < gethTraceLines.Count; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                var depth = int.Parse(ParseGethField(line, "depth"));
                if (depth > maxDepthReached) maxDepthReached = depth;

                var op = ParseGethField(line, "opName").Trim('"');
                if (op == "SSTORE")
                {
                    var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));
                    var gasBefore = ParseGethHexField(line, "gas");
                    var direction = depth >= prevDepth ? "FORWARD" : "UNWIND";

                    if (direction == "FORWARD")
                    {
                        forwardSstoreCount++;
                        gethForwardSstoreCosts.Add(gasCost);
                        if (forwardSstoreCount <= 20)
                        {
                            _output.WriteLine($"{forwardSstoreCount,2} | {depth,5} | {prevDepth,9} | {gasBefore,9} | {gasCost,7} | {direction}");
                        }
                    }
                }
                prevDepth = depth;
            }

            _output.WriteLine("");
            _output.WriteLine($"Geth max depth reached: {maxDepthReached}");
            _output.WriteLine($"Geth forward-phase SSTORE count: {forwardSstoreCount}");
            if (gethForwardSstoreCosts.Count > 0)
            {
                _output.WriteLine($"Geth forward SSTORE gas: min={gethForwardSstoreCosts.Min()}, max={gethForwardSstoreCosts.Max()}, avg={gethForwardSstoreCosts.Average():F1}");
            }

            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            _output.WriteLine("");
            _output.WriteLine("=== NETHEREUM FORWARD PHASE SSTOREs ===");

            prevDepth = 0;
            maxDepthReached = 0;
            forwardSstoreCount = 0;
            var nethForwardSstoreCosts = new System.Collections.Generic.List<long>();

            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                var depth = trace.Depth + 1;
                if (depth > maxDepthReached) maxDepthReached = depth;

                var op = trace.Instruction?.Instruction?.ToString() ?? "";
                if (op == "SSTORE")
                {
                    var gasCost = (long)trace.GasCost;
                    var direction = depth >= prevDepth ? "FORWARD" : "UNWIND";

                    if (direction == "FORWARD")
                    {
                        forwardSstoreCount++;
                        nethForwardSstoreCosts.Add(gasCost);
                        if (forwardSstoreCount <= 20)
                        {
                            _output.WriteLine($"{forwardSstoreCount,2} | {depth,5} | {prevDepth,9} | {gasCost,7} | {direction}");
                        }
                    }
                }
                prevDepth = depth;
            }

            _output.WriteLine("");
            _output.WriteLine($"Nethereum max depth reached: {maxDepthReached}");
            _output.WriteLine($"Nethereum forward-phase SSTORE count: {forwardSstoreCount}");
            if (nethForwardSstoreCosts.Count > 0)
            {
                _output.WriteLine($"Nethereum forward SSTORE gas: min={nethForwardSstoreCosts.Min()}, max={nethForwardSstoreCosts.Max()}, avg={nethForwardSstoreCosts.Average():F1}");
            }

            _output.WriteLine("");
            _output.WriteLine("=== DEPTH COMPARISON ===");
            _output.WriteLine("The max depth determines how many recursive calls completed before OOG.");
            _output.WriteLine("A depth difference indicates cumulative gas discrepancy in the call stack.");
        }

        [Fact]
        public async Task FullTraceComparison()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== FULL TRACE COMPARISON: GETH vs NETHEREUM ===");
            _output.WriteLine("Step-by-step comparison to find exact divergence point.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            // Parse geth traces into structured format
            var gethSteps = new System.Collections.Generic.List<(int step, int depth, int pc, string op, long gas, long gasCost, int memSize)>();
            int gethStep = 0;
            for (int i = 0; i < gethTraceLines.Count; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                gethStep++;
                var depth = int.Parse(ParseGethField(line, "depth"));
                var pc = int.Parse(ParseGethHexField(line, "pc"));
                var op = ParseGethField(line, "opName").Trim('"');
                var gas = long.Parse(ParseGethHexField(line, "gas"));
                var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));
                var memSize = ParseGethMemorySize(line);

                gethSteps.Add((gethStep, depth, pc, op, gas, gasCost, memSize));
            }

            // Parse nethereum traces
            var nethSteps = new System.Collections.Generic.List<(int step, int depth, int pc, string op, long gas, long gasCost, int memSize)>();
            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                var depth = trace.Depth + 1;
                var pc = trace.Instruction?.Step ?? 0;
                var op = trace.Instruction?.Instruction?.ToString() ?? "?";
                var gas = (long)trace.GasRemaining;
                var gasCost = (long)trace.GasCost;
                var memSize = !string.IsNullOrEmpty(trace.Memory) ? trace.Memory.Length / 2 : 0;

                nethSteps.Add((i + 1, depth, pc, op, gas, gasCost, memSize));
            }

            _output.WriteLine($"Geth total steps: {gethSteps.Count}");
            _output.WriteLine($"Nethereum total steps: {nethSteps.Count}");
            _output.WriteLine("");

            // Find matching steps and compare
            _output.WriteLine("Step | Depth | PC  | Op         | G.Gas      | N.Gas      | G.Cost | N.Cost | G.Mem | N.Mem | Match");
            _output.WriteLine("-----|-------|-----|------------|------------|------------|--------|--------|-------|-------|------");

            int gethIdx = 0;
            int nethIdx = 0;
            int matchCount = 0;
            int mismatchCount = 0;
            long cumulativeGethGas = 0;
            long cumulativeNethGas = 0;
            int firstMismatchStep = -1;
            string firstMismatchReason = "";

            while (gethIdx < gethSteps.Count && nethIdx < nethSteps.Count)
            {
                var g = gethSteps[gethIdx];
                var n = nethSteps[nethIdx];

                cumulativeGethGas += g.gasCost;
                cumulativeNethGas += n.gasCost;

                bool depthMatch = g.depth == n.depth;
                bool pcMatch = g.pc == n.pc;
                bool opMatch = g.op == n.op;
                bool gasCostMatch = g.gasCost == n.gasCost;
                bool memMatch = g.memSize == n.memSize;

                string match = "YES";
                if (!depthMatch || !pcMatch || !opMatch)
                {
                    match = "DIVERGE";
                    mismatchCount++;
                    if (firstMismatchStep == -1)
                    {
                        firstMismatchStep = gethIdx + 1;
                        firstMismatchReason = $"depth:{g.depth}vs{n.depth}, pc:{g.pc}vs{n.pc}, op:{g.op}vs{n.op}";
                    }
                }
                else if (!gasCostMatch)
                {
                    match = $"GAS({g.gasCost - n.gasCost:+#;-#;0})";
                    if (firstMismatchStep == -1)
                    {
                        firstMismatchStep = gethIdx + 1;
                        firstMismatchReason = $"gasCost: geth={g.gasCost}, neth={n.gasCost}, diff={g.gasCost - n.gasCost}";
                    }
                }
                else if (!memMatch)
                {
                    match = $"MEM({g.memSize}vs{n.memSize})";
                }
                else
                {
                    matchCount++;
                }

                // Check for gas remaining divergence (ignore CALL gasCost difference since geth includes allocated gas)
                bool gasRemainingMatch = g.gas == n.gas;
                string gasMatch = gasRemainingMatch ? "YES" : $"GAS_REM({g.gas - n.gas})";

                // Print first 50 steps, and any gas remaining mismatches
                bool shouldPrint = (gethIdx < 50) ||
                                   (!gasRemainingMatch) ||
                                   (gethIdx >= Math.Min(gethSteps.Count, nethSteps.Count) - 10);

                if (shouldPrint && gethIdx < 500)
                {
                    _output.WriteLine($"{gethIdx + 1,4} | {g.depth,5} | {g.pc,3} | {g.op,-10} | {g.gas,10} | {n.gas,10} | {g.gasCost,6} | {n.gasCost,6} | {g.memSize,5} | {n.memSize,5} | {gasMatch}");
                }

                gethIdx++;
                nethIdx++;
            }

            // Count gas remaining matches
            int gasRemainingMatches = 0;
            int gasRemainingMismatches = 0;
            int firstGasMismatchStep = -1;
            for (int i = 0; i < Math.Min(gethSteps.Count, nethSteps.Count); i++)
            {
                if (gethSteps[i].gas == nethSteps[i].gas)
                    gasRemainingMatches++;
                else
                {
                    gasRemainingMismatches++;
                    if (firstGasMismatchStep == -1)
                        firstGasMismatchStep = i + 1;
                }
            }

            _output.WriteLine("");
            _output.WriteLine("=== GAS REMAINING ANALYSIS ===");
            _output.WriteLine($"Gas remaining matches: {gasRemainingMatches}/{Math.Min(gethSteps.Count, nethSteps.Count)}");
            _output.WriteLine($"Gas remaining mismatches: {gasRemainingMismatches}");
            if (firstGasMismatchStep > 0)
            {
                _output.WriteLine($"First gas mismatch at step: {firstGasMismatchStep}");

                // Show context around first gas mismatch
                _output.WriteLine("");
                _output.WriteLine($"=== CONTEXT AROUND FIRST GAS DIVERGENCE (step {firstGasMismatchStep}) ===");
                int start = Math.Max(0, firstGasMismatchStep - 10);
                int end = Math.Min(Math.Min(gethSteps.Count, nethSteps.Count), firstGasMismatchStep + 5);

                _output.WriteLine("Step | G.Dep | N.Dep | G.PC | N.PC | G.Op       | N.Op       | G.Gas      | N.Gas      | G.Cost | N.Cost");
                _output.WriteLine("-----|-------|-------|------|------|------------|------------|------------|------------|--------|--------");
                for (int i = start; i < end; i++)
                {
                    var g = gethSteps[i];
                    var n = nethSteps[i];
                    var marker = (i + 1 == firstGasMismatchStep) ? ">>>" : "   ";
                    _output.WriteLine($"{marker}{i + 1,4} | {g.depth,5} | {n.depth,5} | {g.pc,4} | {n.pc,4} | {g.op,-10} | {n.op,-10} | {g.gas,10} | {n.gas,10} | {g.gasCost,6} | {n.gasCost,6}");
                }
            }

            // Show last 15 steps of both traces
            _output.WriteLine("");
            _output.WriteLine("=== LAST 15 STEPS OF NETHEREUM TRACE ===");
            _output.WriteLine("Step | Depth | PC  | Op         | N.Gas      | N.Cost | N.Mem");
            _output.WriteLine("-----|-------|-----|------------|------------|--------|------");
            int startNeth = Math.Max(0, nethSteps.Count - 15);
            for (int i = startNeth; i < nethSteps.Count; i++)
            {
                var n = nethSteps[i];
                _output.WriteLine($"{i + 1,4} | {n.depth,5} | {n.pc,3} | {n.op,-10} | {n.gas,10} | {n.gasCost,6} | {n.memSize,5}");
            }

            _output.WriteLine("");
            _output.WriteLine("=== GETH STEPS AT SAME INDICES ===");
            _output.WriteLine("Step | Depth | PC  | Op         | G.Gas      | G.Cost | G.Mem");
            _output.WriteLine("-----|-------|-----|------------|------------|--------|------");
            for (int i = startNeth; i < Math.Min(gethSteps.Count, nethSteps.Count); i++)
            {
                var g = gethSteps[i];
                _output.WriteLine($"{i + 1,4} | {g.depth,5} | {g.pc,3} | {g.op,-10} | {g.gas,10} | {g.gasCost,6} | {g.memSize,5}");
            }

            // Show ACTUAL last 15 steps of geth trace (different indices than nethereum)
            _output.WriteLine("");
            _output.WriteLine("=== ACTUAL LAST 15 STEPS OF GETH TRACE ===");
            _output.WriteLine("Step | Depth | PC  | Op         | G.Gas      | G.Cost | G.Mem");
            _output.WriteLine("-----|-------|-----|------------|------------|--------|------");
            int startGeth = Math.Max(0, gethSteps.Count - 15);
            for (int i = startGeth; i < gethSteps.Count; i++)
            {
                var g = gethSteps[i];
                _output.WriteLine($"{i + 1,4} | {g.depth,5} | {g.pc,3} | {g.op,-10} | {g.gas,10} | {g.gasCost,6} | {g.memSize,5}");
            }

            // Compare final states at depth 1
            _output.WriteLine("");
            _output.WriteLine("=== FINAL STATE COMPARISON ===");
            var gethFinal = gethSteps.LastOrDefault(s => s.depth == 1 && s.op == "STOP");
            var nethFinal = nethSteps.LastOrDefault(s => s.depth == 1 && s.op == "STOP");
            if (gethFinal.step > 0)
            {
                _output.WriteLine($"Geth final STOP at depth 1: step={gethFinal.step}, gas={gethFinal.gas}");
            }
            if (nethFinal.step > 0)
            {
                _output.WriteLine($"Neth final STOP at depth 1: step={nethFinal.step}, gas={nethFinal.gas}");
            }
            if (gethFinal.step > 0 && nethFinal.step > 0)
            {
                var gasDiff = gethFinal.gas - nethFinal.gas;
                _output.WriteLine($"Gas difference at final STOP: {gasDiff} (geth-neth)");
                _output.WriteLine($"This should match the balance diff / gasPrice: {gasDiff} gas");
            }

            _output.WriteLine("");
            _output.WriteLine("=== SUMMARY ===");
            _output.WriteLine($"Steps compared: {Math.Min(gethIdx, nethIdx)}");
            _output.WriteLine($"Matching steps: {matchCount}");
            _output.WriteLine($"Mismatching steps: {mismatchCount}");
            _output.WriteLine($"Cumulative geth gas: {cumulativeGethGas}");
            _output.WriteLine($"Cumulative neth gas: {cumulativeNethGas}");
            _output.WriteLine($"Cumulative difference: {cumulativeGethGas - cumulativeNethGas}");

            if (firstMismatchStep > 0)
            {
                _output.WriteLine("");
                _output.WriteLine($"=== FIRST MISMATCH at step {firstMismatchStep} ===");
                _output.WriteLine($"Reason: {firstMismatchReason}");

                // Show context around first mismatch
                _output.WriteLine("");
                _output.WriteLine("Context around first mismatch:");
                int start = Math.Max(0, firstMismatchStep - 6);
                int end = Math.Min(Math.Min(gethSteps.Count, nethSteps.Count), firstMismatchStep + 5);

                _output.WriteLine("Step | Depth | PC  | Op         | G.Gas      | N.Gas      | G.Cost | N.Cost | G.Mem | N.Mem");
                _output.WriteLine("-----|-------|-----|------------|------------|------------|--------|--------|-------|------");
                for (int i = start; i < end; i++)
                {
                    var g = gethSteps[i];
                    var n = nethSteps[i];
                    var marker = (i + 1 == firstMismatchStep) ? ">>>" : "   ";
                    _output.WriteLine($"{marker}{i + 1,4} | {g.depth,5} | {g.pc,3} | {g.op,-10} | {g.gas,10} | {n.gas,10} | {g.gasCost,6} | {n.gasCost,6} | {g.memSize,5} | {n.memSize,5}");
                }
            }

            if (gethSteps.Count != nethSteps.Count)
            {
                _output.WriteLine("");
                _output.WriteLine($"=== TRACE LENGTH DIFFERENCE ===");
                _output.WriteLine($"Geth has {gethSteps.Count - nethSteps.Count} more steps than Nethereum");
                _output.WriteLine("This indicates Nethereum ran out of gas earlier.");
            }
        }

        [Fact]
        public async Task CompareSSTORE_GasCosts()
        {
            if (_testVectorsPath == null) return;

            var testFile = Path.Combine(_testVectorsPath, "stCallCodes", "callcallcall_ABCB_RECURSIVE.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath)) return;

            _output.WriteLine("=== SSTORE GAS COST COMPARISON ===");
            _output.WriteLine("Comparing SSTORE operations at each depth between geth and Nethereum.");
            _output.WriteLine("");

            var gethTraceLines = await RunGethTraceWithMemoryAsync(gethEvmPath, testFile);
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();

            // Collect all SSTORE operations by depth for both traces
            var gethSStores = new System.Collections.Generic.List<(int step, int depth, long gas, long gasCost)>();
            var nethSStores = new System.Collections.Generic.List<(int step, int depth, long gas, long gasCost)>();

            int gethStep = 0;
            for (int i = 0; i < gethTraceLines.Count; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;
                gethStep++;

                var op = ParseGethField(line, "opName").Trim('"');
                if (op != "SSTORE") continue;

                var depth = int.Parse(ParseGethField(line, "depth"));
                var gas = long.Parse(ParseGethHexField(line, "gas"));
                var gasCost = long.Parse(ParseGethHexField(line, "gasCost"));
                gethSStores.Add((gethStep, depth, gas, gasCost));
            }

            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                if (trace.Instruction?.Instruction != Instruction.SSTORE) continue;

                var depth = trace.Depth + 1;
                var gas = (long)trace.GasRemaining;
                var gasCost = (long)trace.GasCost;
                nethSStores.Add((i + 1, depth, gas, gasCost));
            }

            _output.WriteLine($"Geth SSTORE count: {gethSStores.Count}");
            _output.WriteLine($"Neth SSTORE count: {nethSStores.Count}");
            _output.WriteLine("");

            // Group by depth and compare
            var gethByDepth = gethSStores.GroupBy(s => s.depth).ToDictionary(g => g.Key, g => g.ToList());
            var nethByDepth = nethSStores.GroupBy(s => s.depth).ToDictionary(g => g.Key, g => g.ToList());

            var allDepths = gethByDepth.Keys.Union(nethByDepth.Keys).OrderBy(d => d).ToList();

            _output.WriteLine("=== SSTORE COUNT BY DEPTH ===");
            _output.WriteLine("Depth | Geth | Neth | Match");
            _output.WriteLine("------|------|------|------");

            foreach (var depth in allDepths.Take(20))
            {
                var gCount = gethByDepth.ContainsKey(depth) ? gethByDepth[depth].Count : 0;
                var nCount = nethByDepth.ContainsKey(depth) ? nethByDepth[depth].Count : 0;
                var match = gCount == nCount ? "YES" : "NO";
                _output.WriteLine($"{depth,5} | {gCount,4} | {nCount,4} | {match}");
            }

            // Compare SSTORE at specific common depths (1, 2, 3)
            _output.WriteLine("");
            _output.WriteLine("=== SSTORE DETAILS AT LOW DEPTHS ===");
            foreach (var depth in new[] { 1, 2, 3, 4, 5, 6 })
            {
                if (!gethByDepth.ContainsKey(depth) && !nethByDepth.ContainsKey(depth)) continue;

                _output.WriteLine($"\n--- Depth {depth} ---");
                if (gethByDepth.ContainsKey(depth))
                {
                    _output.WriteLine("Geth SSTOREs:");
                    foreach (var s in gethByDepth[depth])
                        _output.WriteLine($"  Step={s.step}, Gas={s.gas}, Cost={s.gasCost}");
                }
                if (nethByDepth.ContainsKey(depth))
                {
                    _output.WriteLine("Neth SSTOREs:");
                    foreach (var s in nethByDepth[depth])
                        _output.WriteLine($"  Step={s.step}, Gas={s.gas}, Cost={s.gasCost}");
                }
            }

            // Calculate total SSTORE gas consumed
            long gethTotalSStoreCost = gethSStores.Sum(s => s.gasCost);
            long nethTotalSStoreCost = nethSStores.Sum(s => s.gasCost);
            _output.WriteLine("");
            _output.WriteLine($"=== TOTAL SSTORE GAS ===");
            _output.WriteLine($"Geth total SSTORE gas: {gethTotalSStoreCost}");
            _output.WriteLine($"Neth total SSTORE gas: {nethTotalSStoreCost}");
            _output.WriteLine($"Difference: {gethTotalSStoreCost - nethTotalSStoreCost}");
        }

        [Fact]
        public async Task AnalyzeStaticcallGas_Precompile()
        {
            if (_testVectorsPath == null)
            {
                Assert.True(false, "Test vectors not found");
                return;
            }

            var testFile = Path.Combine(_testVectorsPath, "stStaticCall", "StaticcallToPrecompileFromTransaction.json");
            var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
            var gethEvmPath = Path.Combine(projectRoot, "geth-tools", "geth-alltools-windows-amd64-1.14.12-293a300d", "evm.exe");

            if (!File.Exists(gethEvmPath))
            {
                _output.WriteLine($"geth evm.exe not found at: {gethEvmPath}");
                Assert.True(false, "geth evm.exe not found");
                return;
            }

            _output.WriteLine("Running geth trace...");
            var gethTraceLines = await RunGethTraceAsync(gethEvmPath, testFile);
            _output.WriteLine($"Geth trace: {gethTraceLines.Count} lines");

            _output.WriteLine("Running Nethereum trace...");
            var runner = new GeneralStateTestRunner(_output, "Prague");
            var result = await runner.RunTestWithTraceAsync(testFile);
            var nethTraces = result.Results.FirstOrDefault()?.Traces ?? new System.Collections.Generic.List<ProgramTrace>();
            _output.WriteLine($"Nethereum trace: {nethTraces.Count} lines");

            _output.WriteLine("");
            _output.WriteLine("=== STATICCALL GAS ANALYSIS ===");
            _output.WriteLine("");

            // Find all STATICCALL instructions in Geth trace
            _output.WriteLine("GETH STATICCALL Instructions:");
            _output.WriteLine("Step | PC   | gasCost    | gas_before | gas_after  | actual_consumed");
            _output.WriteLine("-----|------|------------|------------|------------|----------------");

            long gethTotalStaticCallConsumed = 0;
            int gethStaticCallCount = 0;

            for (int i = 0; i < gethTraceLines.Count; i++)
            {
                var line = gethTraceLines[i];
                if (!line.StartsWith("{") || !line.Contains("\"opName\"")) continue;

                var opName = ParseGethField(line, "opName").Trim('"');
                var gasStr = ParseGethHexField(line, "gas");
                var gasCostStr = ParseGethHexField(line, "gasCost");
                var pcStr = ParseGethField(line, "pc");

                if (opName == "STATICCALL")
                {
                    gethStaticCallCount++;
                    string nextGas = "?";
                    // Find next line's gas to calculate actual consumption
                    for (int j = i + 1; j < gethTraceLines.Count; j++)
                    {
                        var nextLine = gethTraceLines[j];
                        if (nextLine.StartsWith("{") && nextLine.Contains("\"opName\""))
                        {
                            nextGas = ParseGethHexField(nextLine, "gas");
                            break;
                        }
                    }

                    long gasBefore = long.TryParse(gasStr, out var gb) ? gb : 0;
                    long gasAfter = long.TryParse(nextGas, out var ga) ? ga : 0;
                    long actualConsumed = gasBefore - gasAfter;
                    gethTotalStaticCallConsumed += actualConsumed;

                    _output.WriteLine($"{gethStaticCallCount,4} | {pcStr,4} | {gasCostStr,10} | {gasStr,10} | {nextGas,10} | {actualConsumed,14}");
                }
            }

            _output.WriteLine("");
            _output.WriteLine($"Geth STATICCALL count: {gethStaticCallCount}");
            _output.WriteLine($"Geth total STATICCALL gas consumed: {gethTotalStaticCallConsumed}");

            // Find all STATICCALL in Nethereum trace
            _output.WriteLine("");
            _output.WriteLine("NETHEREUM STATICCALL Instructions:");
            _output.WriteLine("Step | PC   | trace_cost | gas_before | gas_after  | actual_consumed");
            _output.WriteLine("-----|------|------------|------------|------------|----------------");

            long nethTotalStaticCallConsumed = 0;
            int nethStaticCallCount = 0;

            for (int i = 0; i < nethTraces.Count; i++)
            {
                var trace = nethTraces[i];
                if (trace.Instruction?.Instruction?.ToString() == "STATICCALL")
                {
                    nethStaticCallCount++;
                    var gasBefore = trace.GasRemaining;
                    var gasCost = trace.GasCost;

                    // Find next instruction's gas
                    long gasAfter = 0;
                    if (i + 1 < nethTraces.Count)
                    {
                        gasAfter = (long)nethTraces[i + 1].GasRemaining;
                    }

                    var actualConsumed = (long)gasBefore - gasAfter;
                    nethTotalStaticCallConsumed += actualConsumed;

                    _output.WriteLine($"{nethStaticCallCount,4} | {trace.Instruction?.Step,4} | {gasCost,10} | {gasBefore,10} | {gasAfter,10} | {actualConsumed,14}");
                }
            }

            _output.WriteLine("");
            _output.WriteLine($"Nethereum STATICCALL count: {nethStaticCallCount}");
            _output.WriteLine($"Nethereum total STATICCALL gas consumed: {nethTotalStaticCallConsumed}");

            _output.WriteLine("");
            _output.WriteLine("=== COMPARISON ===");
            _output.WriteLine($"STATICCALL gas difference: {nethTotalStaticCallConsumed - gethTotalStaticCallConsumed}");
            _output.WriteLine($"Per-call difference (avg): {(double)(nethTotalStaticCallConsumed - gethTotalStaticCallConsumed) / Math.Max(1, nethStaticCallCount):F2}");
        }
    }
}

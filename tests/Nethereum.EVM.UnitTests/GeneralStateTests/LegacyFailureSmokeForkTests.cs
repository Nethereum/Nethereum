using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Fast regression detector: one or two representative fixtures per
    /// (fork, opcode-cluster) cell that are known-passing at the
    /// committed baseline. A failure here means a real regression and
    /// the full sweep is unnecessary to confirm it.
    ///
    /// Picks fixtures that exercise:
    ///   - basic value transfer (touched-empty cleanup, Transfer always
    ///     running at tx-level)
    ///   - EXTCODEHASH read paths (read-path materialisation fix)
    ///   - CALL / CALLCODE / DELEGATECALL / STATICCALL with both
    ///     ToOneStorageKey (real contract) and ToEmpty (touched-empty)
    ///   - Refund counter (SUICIDE + SSTORE refund)
    ///   - CREATE / CREATE2
    ///
    /// Total runtime target: under 60 seconds for all 13 forks. Run with
    ///   dotnet test --filter "Category=LegacyFork-SmokeFork"
    /// </summary>
    public class LegacyFailureSmokeForkTests
    {
        private readonly ITestOutputHelper _output;
        public LegacyFailureSmokeForkTests(ITestOutputHelper output) { _output = output; }

        private static string LegacyRoot(string branch)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return Path.Combine(dir.FullName, "external", "legacytests", branch, "GeneralStateTests");
                dir = dir.Parent;
            }
            return null;
        }

        [Theory]
        [Trait("Category", "LegacyFork-SmokeFork")]
        // Pre-EIP-161 cluster: value transfer + simple CALL — must stay PASS
        [InlineData("Constantinople", "stCallCodes", "callcodeEmptycontract.json", "Frontier", 0)]
        [InlineData("Constantinople", "stCallCodes", "callcodeEmptycontract.json", "Homestead", 0)]
        [InlineData("Constantinople", "stCallCodes", "callcodeEmptycontract.json", "EIP150", 0)]
        // EIP-161 cluster: empty-account cleanup tests — historically broken,
        // now should pass with the touched-empty strategy fix.
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "EIP158", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Byzantium", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Constantinople", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALLCODE_ToEmpty.json", "Constantinople", 0)]
        // EXTCODEHASH read-path cluster — Constantinople+, must stay PASS.
        [InlineData("Constantinople", "stExtCodeHash", "extCodeHashNonExistingAccount.json", "Constantinople", 0)]
        [InlineData("Constantinople", "stExtCodeHash", "extCodeHashPrecompiles.json", "Constantinople", 1)]
        [InlineData("Constantinople", "stExtCodeHash", "extCodeHashSubcallOOG.json", "Constantinople", 3)]
        // Refund cluster — must stay PASS.
        [InlineData("Constantinople", "stRefundTest", "refund50_1.json", "Constantinople", 0)]
        // Cancun-branch newer-fork smoke: known passing at baseline.
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Istanbul", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Berlin", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "London", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Paris", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Shanghai", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Cancun", 0)]
        public async Task SmokeFork(string branch, string category, string file, string fork, int dataIndex)
        {
            var root = LegacyRoot(branch);
            if (root == null) { _output.WriteLine("legacytests not cloned; skipping."); return; }
            var path = Path.Combine(root, category, file);
            if (!File.Exists(path)) { _output.WriteLine($"fixture missing: {path}"); return; }

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(path, specificDataIndex: dataIndex, captureTraces: false);
            var single = result.Results.FirstOrDefault(r => r.DataIndex == dataIndex && !r.Skipped)
                         ?? result.Results.FirstOrDefault(r => !r.Skipped);

            if (single == null || single.Skipped)
            {
                _output.WriteLine($"SKIPPED: {file} [{dataIndex}] @ {fork}: {single?.SkipReason ?? "no result"}");
                return; // Skipped is not a regression
            }

            Assert.True(single.Passed,
                $"REGRESSION: {category}/{file} [{dataIndex}] @ {fork}\n  expected={single.ExpectedStateRoot}\n  actual  ={single.ActualStateRoot}\n  msg={single.Message}");
            _output.WriteLine($"PASS: {category}/{file} [{dataIndex}] @ {fork}");
        }
    }
}

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    [Trait("Requires", "Externals")]
    [Trait("Category", "GeneralStateTests")]
    public class LegacyFailureSmokeForkTests
    {
        private readonly ITestOutputHelper _output;
        public LegacyFailureSmokeForkTests(ITestOutputHelper output) { _output = output; }

        private static string LegacyRoot(string branch)
        {
            return Path.Combine(TestEnvironment.RequireExternalPath("LegacyTests"), branch, "GeneralStateTests");
        }

        [Theory]
        [Trait("Category", "LegacyFork-SmokeFork")]
        [InlineData("Constantinople", "stCallCodes", "callcodeEmptycontract.json", "Frontier", 0)]
        [InlineData("Constantinople", "stCallCodes", "callcodeEmptycontract.json", "Homestead", 0)]
        [InlineData("Constantinople", "stCallCodes", "callcodeEmptycontract.json", "EIP150", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "EIP158", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Byzantium", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALL_ToEmpty.json", "Constantinople", 0)]
        [InlineData("Constantinople", "stZeroCallsTest", "ZeroValue_CALLCODE_ToEmpty.json", "Constantinople", 0)]
        [InlineData("Constantinople", "stExtCodeHash", "extCodeHashNonExistingAccount.json", "Constantinople", 0)]
        [InlineData("Constantinople", "stExtCodeHash", "extCodeHashPrecompiles.json", "Constantinople", 1)]
        [InlineData("Constantinople", "stExtCodeHash", "extCodeHashSubcallOOG.json", "Constantinople", 3)]
        [InlineData("Constantinople", "stRefundTest", "refund50_1.json", "Constantinople", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Istanbul", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Berlin", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "London", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Paris", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Shanghai", 0)]
        [InlineData("Cancun", "stZeroCallsTest", "ZeroValue_CALL_ToOneStorageKey.json", "Cancun", 0)]
        public async Task SmokeFork(string branch, string category, string file, string fork, int dataIndex)
        {
            var root = LegacyRoot(branch);
            Assert.True(Directory.Exists(root), $"legacytests directory missing at '{root}'; check ExternalPaths:LegacyTests and the branch name.");
            var path = Path.Combine(root, category, file);
            Assert.True(File.Exists(path), $"Fixture missing: {path}");

            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            var result = await runner.RunTestWithExecutorAsync(path, specificDataIndex: dataIndex, captureTraces: false);
            var single = result.Results.FirstOrDefault(r => r.DataIndex == dataIndex && !r.Skipped)
                         ?? result.Results.FirstOrDefault(r => !r.Skipped);

            if (single == null || single.Skipped)
            {
                _output.WriteLine($"SKIPPED: {file} [{dataIndex}] @ {fork}: {single?.SkipReason ?? "no result"}");
                return;
            }

            Assert.True(single.Passed,
                $"REGRESSION: {category}/{file} [{dataIndex}] @ {fork}\n  expected={single.ExpectedStateRoot}\n  actual  ={single.ActualStateRoot}\n  msg={single.Message}");
            _output.WriteLine($"PASS: {category}/{file} [{dataIndex}] @ {fork}");
        }
    }
}

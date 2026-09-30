using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class CallFrameInitRuleForkWiringTests
    {
        private static readonly Dictionary<HardforkName, CallFrameInitRules> ExpectedRuleByFork =
            new Dictionary<HardforkName, CallFrameInitRules>
            {
                [HardforkName.Frontier] = CallFrameInitRules.Empty,
                [HardforkName.Homestead] = CallFrameInitRules.Empty,
                [HardforkName.TangerineWhistle] = CallFrameInitRules.Empty,
                [HardforkName.SpuriousDragon] = CallFrameInitRules.Empty,
                [HardforkName.Byzantium] = CallFrameInitRules.Empty,
                [HardforkName.Constantinople] = CallFrameInitRules.Empty,
                [HardforkName.Petersburg] = CallFrameInitRules.Empty,
                [HardforkName.Istanbul] = CallFrameInitRules.Empty,
                [HardforkName.Berlin] = CallFrameInitRules.Empty,
                [HardforkName.London] = CallFrameInitRules.Empty,
                [HardforkName.Paris] = CallFrameInitRules.Empty,
                [HardforkName.Shanghai] = CallFrameInitRules.Empty,
                [HardforkName.Cancun] = CallFrameInitRuleSets.Cancun,
                [HardforkName.Prague] = CallFrameInitRuleSets.Prague,
                [HardforkName.Osaka] = CallFrameInitRuleSets.Osaka,
                [HardforkName.OsakaBpo1] = CallFrameInitRuleSets.Osaka,
                [HardforkName.OsakaBpo2] = CallFrameInitRuleSets.Osaka,
                [HardforkName.Amsterdam] = CallFrameInitRuleSets.Amsterdam,
            };

        [Fact]
        public void Given_EveryHardforkName_When_TheCallFrameInitRuleIsResolved_Then_ItMatchesTheDeclaredExpectation()
        {
            var mismatches = RuleSetForkWiringGate.FindMismatches(
                HardforkSpecRegistry.All,
                ExpectedRuleByFork,
                spec => HardforkConfigFromSpec.Build(spec).CallFrameInitRules,
                "CallFrameInit");

            Assert.True(mismatches.Count == 0,
                "CallFrameInit wiring drifted from expectations:\n  " + string.Join("\n  ", mismatches));
        }
    }
}

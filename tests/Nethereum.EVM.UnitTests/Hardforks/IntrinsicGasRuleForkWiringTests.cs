using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class IntrinsicGasRuleForkWiringTests
    {
        private static readonly Dictionary<HardforkName, IntrinsicGasRules> ExpectedRuleByFork =
            new Dictionary<HardforkName, IntrinsicGasRules>
            {
                [HardforkName.Frontier] = IntrinsicGasRuleSets.Frontier,
                [HardforkName.Homestead] = IntrinsicGasRuleSets.Homestead,
                [HardforkName.TangerineWhistle] = IntrinsicGasRuleSets.TangerineWhistle,
                [HardforkName.SpuriousDragon] = IntrinsicGasRuleSets.SpuriousDragon,
                [HardforkName.Byzantium] = IntrinsicGasRuleSets.Byzantium,
                [HardforkName.Constantinople] = IntrinsicGasRuleSets.Constantinople,
                [HardforkName.Petersburg] = IntrinsicGasRuleSets.Petersburg,
                [HardforkName.Istanbul] = IntrinsicGasRuleSets.Istanbul,
                [HardforkName.Berlin] = IntrinsicGasRuleSets.Berlin,
                [HardforkName.London] = IntrinsicGasRuleSets.London,
                [HardforkName.Paris] = IntrinsicGasRuleSets.Paris,
                [HardforkName.Shanghai] = IntrinsicGasRuleSets.Shanghai,
                [HardforkName.Cancun] = IntrinsicGasRuleSets.Cancun,
                [HardforkName.Prague] = IntrinsicGasRuleSets.Prague,
                [HardforkName.Osaka] = IntrinsicGasRuleSets.Osaka,
                [HardforkName.OsakaBpo1] = IntrinsicGasRuleSets.OsakaBpo1,
                [HardforkName.OsakaBpo2] = IntrinsicGasRuleSets.OsakaBpo2,
                [HardforkName.Amsterdam] = IntrinsicGasRuleSets.Amsterdam,
            };

        [Fact]
        public void Given_EveryHardforkName_When_TheIntrinsicGasRuleIsResolved_Then_ItMatchesTheDeclaredExpectation()
        {
            var mismatches = RuleSetForkWiringGate.FindMismatches(
                HardforkSpecRegistry.All,
                ExpectedRuleByFork,
                spec => HardforkConfigFromSpec.Build(spec).IntrinsicGasRules,
                "IntrinsicGas");

            Assert.True(mismatches.Count == 0,
                "IntrinsicGas wiring drifted from expectations:\n  " + string.Join("\n  ", mismatches));
        }
    }
}

using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Execution.TransactionSetup;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class TransactionSetupRuleForkWiringTests
    {
        private static readonly Dictionary<HardforkName, TransactionSetupRules> ExpectedRuleByFork =
            new Dictionary<HardforkName, TransactionSetupRules>
            {
                [HardforkName.Frontier] = TransactionSetupRuleSets.Frontier,
                [HardforkName.Homestead] = TransactionSetupRuleSets.Homestead,
                [HardforkName.TangerineWhistle] = TransactionSetupRuleSets.Frontier,
                [HardforkName.SpuriousDragon] = TransactionSetupRuleSets.Frontier,
                [HardforkName.Byzantium] = TransactionSetupRuleSets.Byzantium,
                [HardforkName.Constantinople] = TransactionSetupRuleSets.Constantinople,
                [HardforkName.Petersburg] = TransactionSetupRuleSets.Frontier,
                [HardforkName.Istanbul] = TransactionSetupRuleSets.Istanbul,
                [HardforkName.Berlin] = TransactionSetupRuleSets.Frontier,
                [HardforkName.London] = TransactionSetupRuleSets.Frontier,
                [HardforkName.Paris] = TransactionSetupRuleSets.Frontier,
                [HardforkName.Shanghai] = TransactionSetupRuleSets.Frontier,
                [HardforkName.Cancun] = TransactionSetupRuleSets.Cancun,
                [HardforkName.Prague] = TransactionSetupRuleSets.Prague,
                [HardforkName.Osaka] = TransactionSetupRuleSets.Osaka,
                [HardforkName.OsakaBpo1] = TransactionSetupRuleSets.Osaka,
                [HardforkName.OsakaBpo2] = TransactionSetupRuleSets.Osaka,
                [HardforkName.Amsterdam] = TransactionSetupRuleSets.Amsterdam,
            };

        [Fact]
        public void Given_EveryHardforkName_When_TheTransactionSetupRuleIsResolved_Then_ItMatchesTheDeclaredExpectation()
        {
            var mismatches = RuleSetForkWiringGate.FindMismatches(
                HardforkSpecRegistry.All,
                ExpectedRuleByFork,
                spec => HardforkConfigFromSpec.Build(spec).TransactionSetupRules,
                "TransactionSetup");

            Assert.True(mismatches.Count == 0,
                "TransactionSetup wiring drifted from expectations:\n  " + string.Join("\n  ", mismatches));
        }
    }
}

using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class TransactionValidationRuleForkWiringTests
    {
        private static readonly Dictionary<HardforkName, TransactionValidationRules> ExpectedRuleByFork =
            new Dictionary<HardforkName, TransactionValidationRules>
            {
                [HardforkName.Frontier] = TransactionValidationRuleSets.Frontier,
                [HardforkName.Homestead] = TransactionValidationRuleSets.Frontier,
                [HardforkName.TangerineWhistle] = TransactionValidationRuleSets.Frontier,
                [HardforkName.SpuriousDragon] = TransactionValidationRuleSets.Frontier,
                [HardforkName.Byzantium] = TransactionValidationRuleSets.Frontier,
                [HardforkName.Constantinople] = TransactionValidationRuleSets.Frontier,
                [HardforkName.Petersburg] = TransactionValidationRuleSets.Constantinople,
                [HardforkName.Istanbul] = TransactionValidationRuleSets.Frontier,
                [HardforkName.Berlin] = TransactionValidationRuleSets.Berlin,
                [HardforkName.London] = TransactionValidationRuleSets.London,
                [HardforkName.Paris] = TransactionValidationRuleSets.London,
                [HardforkName.Shanghai] = TransactionValidationRuleSets.London,
                [HardforkName.Cancun] = TransactionValidationRuleSets.Cancun,
                [HardforkName.Prague] = TransactionValidationRuleSets.Prague,
                [HardforkName.Osaka] = TransactionValidationRuleSets.Osaka,
                [HardforkName.OsakaBpo1] = TransactionValidationRuleSets.Osaka,
                [HardforkName.OsakaBpo2] = TransactionValidationRuleSets.Osaka,
                [HardforkName.Amsterdam] = TransactionValidationRuleSets.Amsterdam,
            };

        [Fact]
        public void Given_EveryHardforkName_When_TheTransactionValidationRuleIsResolved_Then_ItMatchesTheDeclaredExpectation()
        {
            var mismatches = RuleSetForkWiringGate.FindMismatches(
                HardforkSpecRegistry.All,
                ExpectedRuleByFork,
                spec => HardforkConfigFromSpec.Build(spec).TransactionValidationRules,
                "TransactionValidation");

            Assert.True(mismatches.Count == 0,
                "TransactionValidation wiring drifted from expectations:\n  " + string.Join("\n  ", mismatches));
        }
    }
}

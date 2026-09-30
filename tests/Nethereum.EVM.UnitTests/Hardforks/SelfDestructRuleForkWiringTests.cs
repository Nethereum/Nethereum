using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.SelfDestruct;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class SelfDestructRuleForkWiringTests
    {
        private static readonly Dictionary<HardforkName, ISelfDestructRule> ExpectedRuleByFork =
            new Dictionary<HardforkName, ISelfDestructRule>
            {
                [HardforkName.Frontier] = SelfDestructRuleSets.Frontier,
                [HardforkName.Homestead] = SelfDestructRuleSets.Frontier,
                [HardforkName.TangerineWhistle] = SelfDestructRuleSets.Frontier,
                [HardforkName.SpuriousDragon] = SelfDestructRuleSets.Frontier,
                [HardforkName.Byzantium] = SelfDestructRuleSets.Frontier,
                [HardforkName.Constantinople] = SelfDestructRuleSets.Frontier,
                [HardforkName.Petersburg] = SelfDestructRuleSets.Frontier,
                [HardforkName.Istanbul] = SelfDestructRuleSets.Frontier,
                [HardforkName.Berlin] = SelfDestructRuleSets.Frontier,
                [HardforkName.London] = SelfDestructRuleSets.London,
                [HardforkName.Paris] = SelfDestructRuleSets.London,
                [HardforkName.Shanghai] = SelfDestructRuleSets.London,
                [HardforkName.Cancun] = SelfDestructRuleSets.Cancun,
                [HardforkName.Prague] = SelfDestructRuleSets.Cancun,
                [HardforkName.Osaka] = SelfDestructRuleSets.Cancun,
                [HardforkName.OsakaBpo1] = SelfDestructRuleSets.Cancun,
                [HardforkName.OsakaBpo2] = SelfDestructRuleSets.Cancun,
                [HardforkName.Amsterdam] = SelfDestructRuleSets.Amsterdam,
            };

        private static ISelfDestructRule ResolveWiredRule(OpcodeHandlerTable table)
        {
            var exec = table.GetExecutor(Instruction.SELFDESTRUCT);
            Assert.True(exec != null, "No SELFDESTRUCT executor registered on this fork's table.");

            var selfDestructExecutor = Assert.IsType<SelfDestructExecutor>(exec);
            return selfDestructExecutor.Rule;
        }

        [Fact]
        public void Given_EveryHardforkName_When_SelfDestructRuleResolved_Then_ExactlyOneRuleIsWired()
        {
            var mismatches = new List<string>();

            foreach (var spec in HardforkSpecRegistry.All)
            {
                if (!ExpectedRuleByFork.TryGetValue(spec.Name, out var expectedRule))
                {
                    mismatches.Add($"{spec.Name}: no expected SELFDESTRUCT rule declared in this test's " +
                        $"ExpectedRuleByFork map — add an entry naming the rule instance this fork should wire.");
                    continue;
                }

                var wiredRule = ResolveWiredRule(spec.Opcodes);

                if (!ReferenceEquals(expectedRule, wiredRule))
                {
                    mismatches.Add($"{spec.Name}: expected {expectedRule.GetType().Name} but table wires {wiredRule.GetType().Name}");
                }
            }

            Assert.True(mismatches.Count == 0,
                "SELFDESTRUCT wiring drifted from expectations:\n  " + string.Join("\n  ", mismatches));
        }
    }
}

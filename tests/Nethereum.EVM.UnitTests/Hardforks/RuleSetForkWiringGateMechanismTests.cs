using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class RuleSetForkWiringGateMechanismTests
    {
        [Fact]
        public void Given_AForkWithNoDeclaredExpectation_When_TheGateRuns_Then_ItFailsNamingTheFork()
        {
            var specs = HardforkSpecRegistry.All;
            var missingFork = specs.Last().Name;

            var incompleteMap = new Dictionary<HardforkName, CallFrameInitRules>();
            foreach (var spec in specs)
            {
                if (spec.Name == missingFork)
                    continue;

                incompleteMap[spec.Name] = HardforkConfigFromSpec.Build(spec).CallFrameInitRules;
            }

            var mismatches = RuleSetForkWiringGate.FindMismatches(
                specs,
                incompleteMap,
                spec => HardforkConfigFromSpec.Build(spec).CallFrameInitRules,
                "CallFrameInit");

            Assert.Single(mismatches);
            Assert.Contains(missingFork.ToString(), mismatches[0]);
            Assert.Contains("no expected CallFrameInit rule declared", mismatches[0]);
            Assert.Contains("add an entry naming the rule instance this fork should wire", mismatches[0]);
        }

        [Fact]
        public void Given_AForkWithNoDeclaredExpectation_When_TheMapIsInsteadComplete_Then_TheGateReportsNoMismatches()
        {
            var specs = HardforkSpecRegistry.All;

            var completeMap = specs.ToDictionary(
                spec => spec.Name,
                spec => HardforkConfigFromSpec.Build(spec).CallFrameInitRules);

            var mismatches = RuleSetForkWiringGate.FindMismatches(
                specs,
                completeMap,
                spec => HardforkConfigFromSpec.Build(spec).CallFrameInitRules,
                "CallFrameInit");

            Assert.Empty(mismatches);
        }
    }
}

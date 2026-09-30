using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class StateGasActivePredicateTests
    {
        private static List<HardforkName> RegisteredForks(out List<HardforkName> unavailable)
        {
            var available = new List<HardforkName>();
            unavailable = new List<HardforkName>();
            foreach (HardforkName fork in Enum.GetValues(typeof(HardforkName)))
            {
                try
                {
                    if (DefaultMainnetHardforkRegistry.Instance.Get(fork) != null) available.Add(fork);
                    else unavailable.Add(fork);
                }
                catch { unavailable.Add(fork); }
            }
            return available;
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_EveryRegisteredFork_When_AskedForStateGas_Then_OnlyAmsterdamOnwardSaysYes()
        {
            var disagreements = new List<string>();
            var forks = RegisteredForks(out var unavailable);

            Assert.Contains(HardforkName.Amsterdam, forks);

            Assert.Equal(new[] { HardforkName.Unspecified }, unavailable.ToArray());

            foreach (var fork in forks)
            {
                var rules = DefaultMainnetHardforkRegistry.Instance.Get(fork).IntrinsicGasRules;
                var expected = fork >= HardforkName.Amsterdam;

                if (rules.StateGasActive != expected)
                {
                    disagreements.Add(
                        $"{fork}: StateGasActive={rules.StateGasActive}, expected {expected} " +
                        $"(Recipient rule {(rules.Recipient == null ? "absent" : rules.Recipient.GetType().Name)})");
                }
            }

            Assert.True(disagreements.Count == 0,
                "the state-gas predicate no longer tracks the fork it is supposed to:" +
                Environment.NewLine + string.Join(Environment.NewLine, disagreements));
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public void Given_AForkWithoutStateGas_When_ComparedToAmsterdam_Then_ThePredicateDiscriminates()
        {
            var prague = DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Prague).IntrinsicGasRules;
            var amsterdam = DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Amsterdam).IntrinsicGasRules;

            Assert.False(prague.StateGasActive);
            Assert.True(amsterdam.StateGasActive);
        }

    }
}

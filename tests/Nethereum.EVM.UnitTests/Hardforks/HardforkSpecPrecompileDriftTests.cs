using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Hardforks;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class HardforkSpecPrecompileDriftTests
    {
        [Fact]
        public void EveryFork_SpecPrecompiles_match_RuntimePrecompiles()
        {
            var discrepancies = new List<string>();

            foreach (var spec in HardforkSpecRegistry.All)
            {
                var runtimeConfig = DefaultMainnetHardforkRegistry.Instance.Get(spec.Name);
                if (runtimeConfig?.Precompiles == null)
                {
                    discrepancies.Add($"{spec.Name}: runtime has no Precompiles registry wired");
                    continue;
                }

                var specAddresses = new HashSet<int>();
                for (int i = 0; i < spec.Precompiles.Length; i++)
                    specAddresses.Add(spec.Precompiles[i].Address);

                var runtimeAddresses = new HashSet<int>();
                foreach (var a in runtimeConfig.Precompiles.GetAddresses())
                    runtimeAddresses.Add(a);

                if (!specAddresses.SetEquals(runtimeAddresses))
                {
                    var specOnly = new HashSet<int>(specAddresses); specOnly.ExceptWith(runtimeAddresses);
                    var runtimeOnly = new HashSet<int>(runtimeAddresses); runtimeOnly.ExceptWith(specAddresses);
                    discrepancies.Add(
                        $"{spec.Name}: in spec only = [{string.Join(", ", specOnly.OrderBy(x => x).Select(x => $"0x{x:x}"))}], " +
                        $"in runtime only = [{string.Join(", ", runtimeOnly.OrderBy(x => x).Select(x => $"0x{x:x}"))}]");
                }
            }

            Assert.True(discrepancies.Count == 0,
                "HardforkSpec.Precompiles drifted from runtime registry:\n  " +
                string.Join("\n  ", discrepancies));
        }

        [Fact]
        public void EveryFork_RuntimeHandler_addresses_match_GasCalculator_addresses()
        {
            var discrepancies = new List<string>();

            foreach (var spec in HardforkSpecRegistry.All)
            {
                var runtimeConfig = DefaultMainnetHardforkRegistry.Instance.Get(spec.Name);
                if (runtimeConfig?.Precompiles == null) continue;

                var handlerAddresses = new HashSet<int>();
                foreach (var a in runtimeConfig.Precompiles.GetAddresses())
                    handlerAddresses.Add(a);

                var gasCalcAddresses = new HashSet<int>();
                foreach (var a in runtimeConfig.Precompiles.GasCalculators.GetAddresses())
                    gasCalcAddresses.Add(a);

                if (!handlerAddresses.SetEquals(gasCalcAddresses))
                {
                    var handlerOnly = new HashSet<int>(handlerAddresses); handlerOnly.ExceptWith(gasCalcAddresses);
                    var calcOnly = new HashSet<int>(gasCalcAddresses); calcOnly.ExceptWith(handlerAddresses);
                    discrepancies.Add(
                        $"{spec.Name}: handler-only = [{string.Join(", ", handlerOnly.OrderBy(x => x).Select(x => $"0x{x:x}"))}], " +
                        $"gas-calc-only = [{string.Join(", ", calcOnly.OrderBy(x => x).Select(x => $"0x{x:x}"))}]");
                }
            }

            Assert.True(discrepancies.Count == 0,
                "Runtime handler/gas-calc address sets drifted:\n  " +
                string.Join("\n  ", discrepancies));
        }
    }
}

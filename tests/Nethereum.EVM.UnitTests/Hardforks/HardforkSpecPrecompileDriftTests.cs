using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Hardforks;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    /// <summary>
    /// Cross-validation that the spec-declared precompile set on each
    /// <see cref="HardforkSpec"/> matches the runtime-wired precompile
    /// set in <see cref="DefaultMainnetHardforkRegistry"/>.
    ///
    /// <para><b>Why this test exists:</b> the spec carries
    /// <c>Precompiles[]</c> as a declarative list (address + EIP gas
    /// formula variant), but at runtime the precompile registry is
    /// wired separately via <c>MainnetHardforkRegistry.Build(...)</c>
    /// calls to <c>WithPrecompiles(...)</c>. If those two sources of
    /// truth drift apart — e.g. the spec says BLS12-381 is active at
    /// Prague but the runtime forgets to wire 0x0B–0x11 — the spec
    /// silently lies and the failure surfaces far downstream as a
    /// state-root divergence or OOG.</para>
    ///
    /// <para>This test catches every such drift at the
    /// <c>dotnet test</c> boundary with a clear "Fork X: spec has Y
    /// addresses but runtime has Z" message. Until the runtime is
    /// rewired to consume <c>spec.Precompiles[]</c> directly, this
    /// test is the safety net.</para>
    /// </summary>
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

        /// <summary>
        /// Asserts that every fork's runtime executor (handler) set
        /// matches the gas-calculator set — i.e. for every precompile
        /// you can call, there is a gas calculator to price it. The
        /// inverse (gas calc with no executor) would also be invalid
        /// but harder to hit accidentally.
        ///
        /// <para>Without this check, a wired backend like Blake2f at
        /// pre-Istanbul forks (the bug commit 736169f2 just fixed) is
        /// callable via the registry but produces undefined gas
        /// behaviour because no calculator entry exists.</para>
        /// </summary>
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

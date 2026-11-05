using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Precompiles.Backends;
using Xunit;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    public class ManagedBn254BackendCompositionTests
    {
        [Fact]
        public void Given_the_default_precompile_composition_When_the_bn128_backend_is_resolved_Then_it_is_the_managed_backend()
        {
            Assert.Same(ManagedBn254Backend.Instance, DefaultPrecompileBackends.Instance.Bn128);
        }

        [Fact]
        public void Given_the_default_bn128_backend_When_it_is_used_directly_Then_it_stays_available_and_agrees_with_the_managed_backend()
        {
            var input = new byte[96];
            input[31] = 1;
            input[63] = 2;
            input[95] = 7;

            Assert.True(DefaultBn128Backend.Instance.TryMul(input, out var reference));
            Assert.True(ManagedBn254Backend.Instance.TryMul(input, out var managed));
            Assert.Equal(reference, managed);
        }

        [Fact]
        public void Given_the_default_registry_for_every_fork_with_bn128_When_ecAdd_and_ecMul_run_Then_the_managed_results_come_back()
        {
            var add = new byte[128];
            add[31] = 1;
            add[63] = 2;
            add[95] = 1;
            add[127] = 2;
            var mul = new byte[96];
            mul[31] = 1;
            mul[63] = 2;
            mul[95] = 2;

            foreach (var registry in new[]
            {
                DefaultPrecompileRegistries.ByzantiumBase(), DefaultPrecompileRegistries.IstanbulBase(),
                DefaultPrecompileRegistries.BerlinBase(), DefaultPrecompileRegistries.CancunBase(),
                DefaultPrecompileRegistries.PragueBase(), DefaultPrecompileRegistries.OsakaBase()
            })
            {
                Assert.True(registry.TryExecute(6, add, out var doubled));
                Assert.True(registry.TryExecute(7, mul, out var multiplied));
                Assert.Equal(doubled, multiplied);
            }
        }
    }
}

using System;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class HostFixturePrecompileWiringTests
    {
        private static readonly int[] PluginBackedPrecompiles =
            { 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0x10, 0x11 };

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public void Given_the_host_fixture_chain_config_When_its_Amsterdam_precompiles_are_resolved_Then_none_is_an_unwired_placeholder()
        {
            var precompiles = HostBlockchainTestRunner
                .CreateChainConfig(chainId: 1, blockGasLimit: 120_000_000, baseFee: 7)
                .GetHardforkConfig()
                .Precompiles;

            var unwired = precompiles.GetAddresses()
                .Where(a => precompiles.Get(a) is PlaceholderPrecompile)
                .Select(a => "0x" + a.ToString("x"))
                .ToList();

            Assert.True(unwired.Count == 0,
                "Amsterdam host fixtures would execute with THROWING placeholders at: " +
                string.Join(", ", unwired));
        }

        private const string FixtureKzgPointEvaluationInput =
            "018156b94fe9735e573bab36dad05d60feb720d424ccd20aaf719343c31e4246" +
            "019123bcb9d06356701f7be08b4494625b87a7b02edc566126fb81f6306e915f" +
            "6c2eb1e94c2532935b8465351ba1bd88eabe2b3fa1aadff7d1cd816e8315bd38" +
            "a9546d41993e10df2a7429b8490394ea9ee62807bae6f326d1044a51581306f5" +
            "8d4b9dfd5931e044688855280ff3799ea2ea83d9391e0ee42e0c650acc7a1f84" +
            "2a7d385189485ddb4fd54ade3d9fd50d608167dca6c776aad4b8ad5c20691bfe";

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public void Given_the_host_fixture_registry_When_a_BLS12_381_and_a_KZG_precompile_are_executed_Then_both_return_a_real_result()
        {
            var precompiles = HostBlockchainTestRunner.FixtureRegistry
                .Get(HardforkName.Amsterdam).Precompiles;

            var fpElement = new byte[64];
            fpElement[63] = 1;
            Assert.Equal(128, precompiles.Execute(0x10, fpElement).Length);

            var kzgResult = precompiles.Execute(0x0a, FixtureKzgPointEvaluationInput.HexToByteArray());
            Assert.Equal(64, kzgResult.Length);
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public void Given_the_default_registry_the_fixture_runner_no_longer_uses_When_the_plugin_backed_precompiles_are_executed_Then_every_one_throws_unwired()
        {
            var precompiles = DefaultMainnetHardforkRegistry.Instance
                .Get(HardforkName.Amsterdam).Precompiles;

            foreach (var address in PluginBackedPrecompiles)
            {
                Assert.True(precompiles.CanHandle(address), $"0x{address:x} is not claimed at Amsterdam");
                var thrown = Assert.Throws<UnwiredPrecompileException>(
                    () => precompiles.Execute(address, new byte[192]));
                Assert.Contains("no backend wired", thrown.Message);
                Assert.True(EvmHostException.IsHostOrSystemFault(thrown));
            }
        }
    }
}

using Nethereum.Documentation;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainSystemContractPredeployTests
    {
        private const string BeaconRootsContract = "0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02";

        [Fact]
        public async Task Given_TheDefaultConfig_When_TheNodeStarts_Then_TheBeaconRootsContractIsPredeployed()
        {
            using var node = new DevChainNode(DevChainConfig.Default);
            await node.StartAsync();

            var code = await node.GetCodeAsync(BeaconRootsContract);

            Assert.NotNull(code);
            Assert.NotEmpty(code);
        }

        [Fact]
        [NethereumDocExample(DocSection.InProcessNode, "devchain", "Skip the genesis system-contract predeploys")]
        public async Task Given_PredeploySystemContractsIsFalse_When_TheNodeStarts_Then_GenesisHasNoSystemContracts()
        {
            var config = DevChainConfig.Default;
            config.PredeploySystemContracts = false;
            using var node = new DevChainNode(config);
            await node.StartAsync();

            var code = await node.GetCodeAsync(BeaconRootsContract);

            Assert.True(code == null || code.Length == 0);
        }
    }
}

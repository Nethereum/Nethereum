using System;
using Nethereum.DevChain.Configuration;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainServerConfigValidatorTests
    {
        [Fact]
        public void Given_Discv4ExplicitlyEnabled_When_ItIsValidated_Then_ItRefuses()
        {
            var config = new DevChainServerConfig();
            config.Node.Network.Discovery.DisableDiscv4 = false;

            var ex = Assert.Throws<InvalidOperationException>(
                () => DevChainServerConfigValidator.Validate(config));
            Assert.Contains("discv4/discv5 peer discovery is not supported on this node type (DevChain)", ex.Message);
        }

        [Fact]
        public void Given_Discv5ExplicitlyEnabled_When_ItIsValidated_Then_ItRefuses()
        {
            var config = new DevChainServerConfig();
            config.Node.Network.Discovery.DisableDiscv5 = false;

            Assert.Throws<InvalidOperationException>(() => DevChainServerConfigValidator.Validate(config));
        }

        [Fact]
        public void Given_ADiscv5PortSet_When_ItIsValidated_Then_ItRefuses()
        {
            var config = new DevChainServerConfig();
            config.Node.Network.Discovery.Discv5Port = 30305;

            Assert.Throws<InvalidOperationException>(() => DevChainServerConfigValidator.Validate(config));
        }

        [Fact]
        public void Given_TheDefaultDiscoveryConfig_When_ItIsValidated_Then_ItIsAccepted()
        {
            DevChainServerConfigValidator.Validate(new DevChainServerConfig());
        }
    }
}

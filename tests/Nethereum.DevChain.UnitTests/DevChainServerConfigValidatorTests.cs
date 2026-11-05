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
        public void Given_TheDefaultNodeConfig_When_DiscoveryIsRead_Then_Discv5IsOff()
        {
            var config = new DevChainServerConfig();

            Assert.True(config.Node.Network.Discovery.DisableDiscv5);
            Assert.Equal(0, config.Node.Network.Discovery.Discv5Port);
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

        [Theory]
        [InlineData("TrustedPeers", "DevChain:Node:Network:TrustedPeers:0")]
        [InlineData("TrustedBootnodes", "DevChain:Node:Network:TrustedBootnodes:0")]
        [InlineData("FollowPeerEnode", "DevChain:Node:Sync:FollowPeerEnode")]
        public void Given_AMalformedEnodeUnderAConfigKey_When_ItIsValidated_Then_ItRefusesNamingThatKeyAndTheEntry(
            string where, string expectedKey)
        {
            const string malformed = "enode://not-a-real-pubkey@127.0.0.1:30702";
            var config = new DevChainServerConfig();
            switch (where)
            {
                case "TrustedPeers":
                    config.Node.Network.TrustedPeers = new[] { malformed };
                    break;
                case "TrustedBootnodes":
                    config.Node.Network.TrustedBootnodes = new[] { malformed };
                    break;
                default:
                    config.Node.Sync.FollowPeerEnode = malformed;
                    break;
            }

            var ex = Assert.Throws<InvalidOperationException>(() => DevChainServerConfigValidator.Validate(config));

            Assert.Contains($"{expectedKey} = '{malformed}'", ex.Message);
        }

        [Fact]
        public void Given_AWellFormedTrustedPeer_When_ItIsValidated_Then_ItIsAccepted()
        {
            var config = new DevChainServerConfig();
            config.Node.Network.TrustedPeers = new[] { $"enode://{new string('a', 128)}@127.0.0.1:30303" };

            DevChainServerConfigValidator.Validate(config);
        }

        [Fact]
        public void Given_TheDefaultDiscoveryConfig_When_ItIsValidated_Then_ItIsAccepted()
        {
            DevChainServerConfigValidator.Validate(new DevChainServerConfig());
        }
    }
}

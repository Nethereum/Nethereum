using System;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainServerConfigTests
    {
        private static AppChainServerConfig Sequencer()
        {
            var config = new AppChainServerConfig();
            config.Genesis.Owner.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Sequencer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            return config;
        }

        [Fact]
        public void Given_OnlyPrivateKeys_When_AddressesAreDerived_Then_EveryRoleIsIdentified()
        {
            var config = Sequencer();
            config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();

            config.DeriveAddresses();

            Assert.True(config.Genesis.Owner.IsIdentified);
            Assert.True(config.Consensus.Sequencer.IsIdentified);
            Assert.True(config.Consensus.Clique.Signer.IsIdentified);
        }

        [Fact]
        public void Given_AnAddressAlreadySet_When_AddressesAreDerived_Then_ItIsNotOverwritten()
        {
            var config = Sequencer();
            config.Genesis.Owner.Address = "0x1111111111111111111111111111111111111111";

            config.DeriveAddresses();

            Assert.Equal("0x1111111111111111111111111111111111111111", config.Genesis.Owner.Address);
        }

        [Fact]
        public void Given_ASequencerWithKeys_When_ItIsValidated_Then_ItIsAccepted()
        {
            Sequencer().Validate();
        }

        [Fact]
        public void Given_ASequencerWithNoPrivateKeys_When_ItIsValidated_Then_ItRefuses()
        {
            Assert.Throws<InvalidOperationException>(() => new AppChainServerConfig().Validate());
        }

        [Fact]
        public void Given_AFollowerWithAddressesOnly_When_ItIsValidated_Then_ItIsAccepted()
        {
            var config = new AppChainServerConfig();
            config.Genesis.Owner.Address = "0x1111111111111111111111111111111111111111";
            config.Consensus.Sequencer.Address = "0x2222222222222222222222222222222222222222";
            config.Node.Sync.Mode = ChainNode.Hosting.Configuration.SyncMode.ForwardExecute;
            config.Node.Sync.FollowPeerEnode = "enode://" + new string('a', 128) + "@127.0.0.1:30303";

            config.Validate();
        }

        [Fact]
        public void Given_CliqueWithNoInitialSigners_When_ItIsValidated_Then_ItRefuses()
        {
            var config = Sequencer();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Fact]
        public void Given_CliqueWithSignersButNoPeers_When_ItIsValidated_Then_ItRefuses()
        {
            var config = Sequencer();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Clique.InitialSigners = new[] { "0x1111111111111111111111111111111111111111" };

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Fact]
        public void Given_CliqueReachingSiblingsOverDevP2P_When_ItIsValidated_Then_ItIsAccepted()
        {
            var config = Sequencer();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Clique.InitialSigners = new[] { "0x1111111111111111111111111111111111111111" };
            config.Node.Network.TrustedPeers = new[]
            {
                $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30404"
            };

            config.Validate();
        }

        [Fact]
        public void Given_ACliqueSignerConfigWithSyncModeNone_When_Validated_Then_ItRefuses()
        {
            var config = Sequencer();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Clique.InitialSigners = new[] { "0x1111111111111111111111111111111111111111" };
            config.Node.Network.TrustedPeers = new[]
            {
                $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30404"
            };
            config.Node.Sync.Mode = ChainNode.Hosting.Configuration.SyncMode.None;

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Fact]
        public void Given_ACliqueSignerConfigWithSyncModeForwardExecute_When_Validated_Then_ItIsAccepted()
        {
            var config = Sequencer();
            config.Consensus.Mode = AppChainConsensusMode.Clique;
            config.Consensus.Clique.Signer.PrivateKey = EthECKey.GenerateKey().GetPrivateKey();
            config.Consensus.Clique.InitialSigners = new[] { "0x1111111111111111111111111111111111111111" };
            config.Node.Network.TrustedPeers = new[]
            {
                $"enode://{EthECKey.GenerateKey().GetPubKeyNoPrefix().ToHex()}@127.0.0.1:30404"
            };
            config.Node.Sync.Mode = ChainNode.Hosting.Configuration.SyncMode.ForwardExecute;

            config.Validate();
        }

        [Fact]
        public void Given_Discv4ExplicitlyEnabled_When_ItIsValidated_Then_ItRefuses()
        {
            var config = Sequencer();
            config.Node.Network.Discovery.DisableDiscv4 = false;

            var ex = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.Contains("discv4/discv5 peer discovery is not supported on this node type (AppChain)", ex.Message);
        }

        [Fact]
        public void Given_Discv5ExplicitlyEnabled_When_ItIsValidated_Then_ItRefuses()
        {
            var config = Sequencer();
            config.Node.Network.Discovery.DisableDiscv5 = false;

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Fact]
        public void Given_ADiscv4PortSet_When_ItIsValidated_Then_ItRefuses()
        {
            var config = Sequencer();
            config.Node.Network.Discovery.Discv4Port = 30301;

            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Fact]
        public void Given_TheDefaultDiscoveryConfig_When_ItIsValidated_Then_ItIsAccepted()
        {
            Sequencer().Validate();
        }

        [Fact]
        public void Given_AnUnknownConsensusModeName_When_ItIsParsed_Then_ItRefuses()
        {
            Assert.Throws<InvalidOperationException>(() => AppChainConsensusModeParser.Parse("raft"));
        }

        [Fact]
        public void Given_TheCliqueModeName_When_ItIsParsed_Then_ItIsClique()
        {
            Assert.Equal(AppChainConsensusMode.Clique, AppChainConsensusModeParser.Parse("clique"));
        }
    }
}

using System;
using System.Linq;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeConfigTests
    {
        private static string EnodeFor(EthECKey key, int port) =>
            $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";


        [Theory]
        [InlineData(ChainNodeRole.Signer, MempoolRetention.Full, MempoolRelay.Ours)]
        [InlineData(ChainNodeRole.Follower, MempoolRetention.RelayOnly, MempoolRelay.Ours)]
        [InlineData(ChainNodeRole.GossipNode, MempoolRetention.Full, MempoolRelay.All)]
        public void Given_ARole_When_ItIsAppliedToAConfig_Then_BothMempoolAxesAreSetForThatRole(
            ChainNodeRole role, MempoolRetention retention, MempoolRelay relay)
        {
            var config = ChainNodeConfigFactory.Archive().AsRole(role);

            Assert.Equal(retention, config.Mempool.Retention);
            Assert.Equal(relay, config.Mempool.Relay);
        }

        [Fact]
        public void Given_ARole_When_ItIsAppliedToAConfig_Then_TheStoragePresetItWasBuiltOnSurvives()
        {
            var archive = ChainNodeConfigFactory.Archive();
            var journalBlocks = archive.Storage.JournalBlocks;
            var pathKeyed = archive.Storage.PathKeyedState;

            archive.AsRole(ChainNodeRole.Signer);

            Assert.Equal(journalBlocks, archive.Storage.JournalBlocks);
            Assert.Equal(pathKeyed, archive.Storage.PathKeyedState);
        }

        [Fact]
        public void Given_AFollowPeerAndTrustedPeers_When_DialEnodesAreResolved_Then_TheFollowPeerLeads()
        {
            var producer = EnodeFor(EthECKey.GenerateKey(), 30403);
            var sibling = EnodeFor(EthECKey.GenerateKey(), 30404);

            var config = new ChainNodeConfig
            {
                Sync = new ChainNodeSyncConfig { FollowPeerEnode = producer },
                Network = new ChainNodeNetworkConfig { TrustedPeers = new[] { sibling } },
            };

            Assert.Equal(new[] { producer, sibling }, config.ResolveDialEnodes());
        }

        [Fact]
        public void Given_AFollowPeerAlsoListedAsTrusted_When_DialEnodesAreResolved_Then_ItIsDialledOnce()
        {
            var producer = EnodeFor(EthECKey.GenerateKey(), 30403);

            var config = new ChainNodeConfig
            {
                Sync = new ChainNodeSyncConfig { FollowPeerEnode = producer },
                Network = new ChainNodeNetworkConfig { TrustedPeers = new[] { producer } },
            };

            Assert.Single(config.ResolveDialEnodes());
        }

        [Fact]
        public void Given_NoFollowPeer_When_DialEnodesAreResolved_Then_OnlyTheTrustedPeersAreDialled()
        {
            var sibling = EnodeFor(EthECKey.GenerateKey(), 30404);

            var config = new ChainNodeConfig
            {
                Network = new ChainNodeNetworkConfig { TrustedPeers = new[] { sibling } },
            };

            Assert.Equal(new[] { sibling }, config.ResolveDialEnodes());
        }

        [Fact]
        public void Given_TrustedEnodes_When_NodeIdsAreResolved_Then_TheyAreTheNodeIdNotTheWholeEnode()
        {
            var key = EthECKey.GenerateKey();
            var config = new ChainNodeConfig
            {
                Network = new ChainNodeNetworkConfig { TrustedPeers = new[] { EnodeFor(key, 30403) } },
            };

            Assert.Equal(new[] { key.GetPubKeyNoPrefix().ToHex() }, config.ResolveTrustedNodeIds());
        }

        [Fact]
        public void Given_AMalformedEnode_When_NodeIdsAreResolved_Then_NothingIsTrusted()
        {
            var config = new ChainNodeConfig
            {
                Network = new ChainNodeNetworkConfig { TrustedPeers = new[] { "not-an-enode", "enode://short@1.2.3.4:1" } },
            };

            Assert.Empty(config.ResolveTrustedNodeIds());
        }

        [Fact]
        public void Given_AProducerWithNoFollowPeer_When_AskedIfItFollows_Then_ItDoesNot()
        {
            Assert.False(new ChainNodeConfig().FollowsAPeer);
        }

        [Fact]
        public void Given_AFollowPeerButSyncModeNone_When_AskedIfItFollows_Then_ItDoesNot()
        {
            var config = new ChainNodeConfig
            {
                Sync = new ChainNodeSyncConfig
                {
                    Mode = SyncMode.None,
                    FollowPeerEnode = EnodeFor(EthECKey.GenerateKey(), 30403),
                },
            };

            Assert.False(config.FollowsAPeer);
        }

        [Fact]
        public void Given_AFollowPeerAndASyncMode_When_AskedIfItFollows_Then_ItDoes()
        {
            var config = new ChainNodeConfig
            {
                Sync = new ChainNodeSyncConfig
                {
                    Mode = SyncMode.Snap,
                    FollowPeerEnode = EnodeFor(EthECKey.GenerateKey(), 30403),
                },
            };

            Assert.True(config.FollowsAPeer);
        }
    }
}

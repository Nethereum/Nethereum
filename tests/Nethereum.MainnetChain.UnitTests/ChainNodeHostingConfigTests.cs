using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.ChainNode.Hosting.Rpc;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class ChainNodeHostingConfigTests
    {
        private const string Enode =
            "enode://6f8a80d14311c39f35f516fa664deaaaa13e85b2f7493f37f6144d86991ec012937307647bd3b9a82abe2974e1407241d54947bbb39763a4cac9f77166ad92a0@127.0.0.1:30303";

        [Fact]
        public void Given_TheDefaultNetworkConfig_Then_TheHandshakeAndIdleDeadlinesAreFiveAndThirtySeconds()
        {
            var network = new ChainNodeNetworkConfig();

            Assert.Equal(5_000, network.HandshakeTimeoutMs);
            Assert.Equal(TimeSpan.FromSeconds(30), network.IdleTimeout);
            Assert.False(network.MirrorRemoteStatus);
        }

        [Fact]
        public void Given_TheDefaultRpcConfig_Then_OnlyEthNetAndWeb3AreServedAndNoHostOrOriginIsAdded()
        {
            var rpc = new ChainNodeRpcConfig();
            var modules = rpc.ToModuleSet();
            var options = rpc.ToHttpOptions();

            Assert.True(modules.Contains("eth") && modules.Contains("net") && modules.Contains("web3"));
            Assert.False(modules.Contains("debug") || modules.Contains("admin") || modules.Contains("txpool"));
            Assert.Empty(options.AllowedHosts);
            Assert.Empty(options.AllowedOrigins);
            Assert.Equal(rpc.Host, options.BindHost);
        }

        [Fact]
        public void Given_AnUnknownRpcModule_When_TheModuleSetIsBuilt_Then_ItIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new ChainNodeRpcConfig { Modules = "eth,wallet" }.ToModuleSet());
        }

        [Fact]
        public void Given_ACommaSeparatedEnodeList_When_ItIsSplit_Then_EntriesAreTrimmedAndEmptyOnesDropped()
        {
            Assert.Equal(new[] { Enode, Enode }, ChainNodeNetworkConfig.SplitEnodeList(" " + Enode + " ,, " + Enode + " "));
            Assert.Empty(ChainNodeNetworkConfig.SplitEnodeList((string?)null));
            Assert.Equal(new[] { Enode, Enode }, ChainNodeNetworkConfig.SplitEnodeList(new[] { Enode + "," + Enode }));
        }

        [Fact]
        public void Given_AMalformedTrustedBootnode_When_TheConfigIsChecked_Then_TheKeyAndTheEntryAreNamed()
        {
            var node = new ChainNodeConfig();
            node.Network.TrustedPeers = new[] { Enode };
            node.Network.TrustedBootnodes = new[] { Enode, "enode://nothex@host:30303" };

            var error = Assert.Throws<InvalidOperationException>(() => ChainNodeEnodeValidator.RefuseMalformed(node, "Test:Node"));

            Assert.Contains("Test:Node:Network:TrustedBootnodes:1", error.Message);
            Assert.Contains("enode://nothex@host:30303", error.Message);
        }

        [Fact]
        public void Given_TwoInMemoryNodesWithNoKeyConfigured_When_TheirKeysAreResolved_Then_EachHasItsOwnStableKey()
        {
            var first = new ChainNodeConfig { Storage = { InMemory = true } };
            var second = new ChainNodeConfig { Storage = { InMemory = true } };

            var firstKey = first.ResolveNodeKey();
            var secondKey = second.ResolveNodeKey();

            Assert.Equal(firstKey.GetPrivateKey(), first.ResolveNodeKey().GetPrivateKey());
            Assert.NotEqual(firstKey.GetPrivateKey(), secondKey.GetPrivateKey());
        }

        [Fact]
        public void Given_ANonSnapModeAndSnapBootstrapFalse_When_ItIsSet_Then_TheModeIsUnchangedAndOnlySnapFallsBackToForwardExecute()
        {
            var none = new ChainNodeSyncConfig { Mode = SyncMode.None };
            none.SnapBootstrap = false;
            var snap = new ChainNodeSyncConfig { Mode = SyncMode.Snap };
            snap.SnapBootstrap = false;

            Assert.Equal(SyncMode.None, none.Mode);
            Assert.Equal(SyncMode.ForwardExecute, snap.Mode);
        }

        [Fact]
        public void Given_AProducerSection_When_ItIsApplied_Then_OnlyTheValuesThatAreSetReachTheChainConfig()
        {
            var chain = new Nethereum.CoreChain.ChainConfig();
            var originalGasLimit = chain.BlockGasLimit;

            new ChainNodeProducerConfig { BaseFee = 7 }.ApplyTo(chain);

            Assert.Equal(7, (int)chain.BaseFee);
            Assert.Equal(originalGasLimit, chain.BlockGasLimit);
            Assert.Throws<InvalidOperationException>(() =>
                ChainNodeProducerValidator.Validate(new ChainNodeProducerConfig { BlockGasLimit = 0 }, "Test:Producer"));
        }

        [Fact]
        public void Given_TheHostPolicy_When_HeadersAreChecked_Then_LocalhostIpsTheBindHostAndListedNamesPass()
        {
            var policy = new RpcHostPolicy(new[] { "rpc.example.org" }, "node.internal");

            Assert.True(policy.IsAllowed("localhost:8545"));
            Assert.True(policy.IsAllowed("10.0.0.5:8545"));
            Assert.True(policy.IsAllowed("[::1]:8545"));
            Assert.True(policy.IsAllowed("node.internal"));
            Assert.True(policy.IsAllowed("rpc.example.org:443"));
            Assert.False(policy.IsAllowed("evil.example"));
            Assert.True(new RpcHostPolicy(new[] { RpcHostPolicy.Wildcard }, null).IsAllowed("evil.example"));
        }
    }
}

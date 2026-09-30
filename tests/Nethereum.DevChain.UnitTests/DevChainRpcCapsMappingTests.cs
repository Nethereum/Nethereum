using Nethereum.DevChain.Configuration;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevChainRpcCapsMappingTests
    {
        [Fact]
        public void RpcCapsSetOnNodeConfig_FlowThroughToTheLiveChainConfigLogQueryGuardsRead()
        {
            var config = new DevChainServerConfig();
            config.Node.Rpc.MaxLogBlockRange = 42;
            config.Node.Rpc.MaxLogResults = 7;
            config.Node.Rpc.GasCap = 123_456;

            var chain = config.GetLiveChainConfig();

            Assert.Equal(42, chain.RpcMaxLogBlockRange);
            Assert.Equal(7, chain.RpcMaxLogResults);
            Assert.Equal((System.Numerics.BigInteger)123_456, chain.RpcGasCap);
        }

        [Fact]
        public void RpcCapsLeftUnset_LiveChainConfigKeepsProductionDefaults()
        {
            var config = new DevChainServerConfig();

            var chain = config.GetLiveChainConfig();

            Assert.Equal(10_000, chain.RpcMaxLogBlockRange);
            Assert.Equal(10_000, chain.RpcMaxLogResults);
            Assert.Equal((System.Numerics.BigInteger)50_000_000, chain.RpcGasCap);
        }

        [Fact]
        public void HostAndPortLeftUnset_MatchTheSharedNodeRpcDefaults()
        {
            var config = new DevChainServerConfig();

            Assert.Equal(config.Node.Rpc.Host, config.Host);
            Assert.Equal(config.Node.Rpc.Port, config.Port);
            Assert.Equal("127.0.0.1", config.Host);
            Assert.Equal(8545, config.Port);
        }

        [Fact]
        public void NodeRpcHostAndPortSetDirectly_FlowThroughToTheDevChainServerConfigBindingFields()
        {
            var config = new DevChainServerConfig();

            config.Node.Rpc.Host = "10.0.0.5";
            config.Node.Rpc.Port = 19999;

            Assert.Equal("10.0.0.5", config.Host);
            Assert.Equal(19999, config.Port);
        }

        [Fact]
        public void HostAndPortSetTheWayTheFlatCliFlagsDo_FlowThroughToNodeRpc()
        {
            var config = new DevChainServerConfig();

            config.Host = "192.168.1.1";
            config.Port = 18888;

            Assert.Equal("192.168.1.1", config.Node.Rpc.Host);
            Assert.Equal(18888, config.Node.Rpc.Port);
        }
    }
}

using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.DevP2P;
using Nethereum.MainnetChain.Configuration;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetTrustedPeerDialTargetsTests
    {
        private static string[] SixteenTrustedPeers()
        {
            var nibbles = "0123456789abcdef";
            var enodes = new string[nibbles.Length];
            for (var i = 0; i < nibbles.Length; i++)
            {
                var hostPort = i switch
                {
                    0 => "127.0.0.1:30307",
                    1 => "112.154.155.200:20001",
                    _ => $"10.0.0.{i}:30303",
                };
                enodes[i] = $"enode://{new string(nibbles[i], 128)}@{hostPort}";
            }
            return enodes;
        }

        private static ChainNodeConfig MapFromCli(params string[] args)
        {
            var config = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(config, args);
            return config.ToChainNodeConfig();
        }

        [Fact]
        public void Given_TheTrustedPeerFlagCarriesSixteenCommaSeparatedEnodes_When_TheServerConfigIsMappedForThePeerPool_Then_EveryTrustedDialTargetIsExactlyOneParseableEnode()
        {
            var trustedPeers = SixteenTrustedPeers();

            var node = MapFromCli("--trusted-peer", string.Join(",", trustedPeers));
            var trustedDialTargets = node.ResolveDialEnodes().Concat(node.Network.TrustedBootnodes).ToList();

            Assert.Equal(trustedPeers, trustedDialTargets);
            Assert.All(trustedDialTargets, target =>
            {
                Assert.DoesNotContain(",", target);
                Assert.True(EnodeUrl.TryParse(target, out _), $"not a parseable enode: {target}");
            });
        }

        [Fact]
        public void Given_TheTrustedPeerFlagCarriesSixteenCommaSeparatedEnodes_When_TrustedNodeIdsAreResolved_Then_EachPeerIsTrustedByItsOwnNodeIdAndNoneIsMalformed()
        {
            var trustedPeers = SixteenTrustedPeers();

            var node = MapFromCli("--trusted-peer", string.Join(",", trustedPeers));
            var nodeIds = node.ResolveTrustedNodeIds(out var malformed);

            Assert.Empty(malformed);
            Assert.Equal(trustedPeers.Select(p => EnodeUrl.Parse(p).PeerId), nodeIds);
        }

        [Fact]
        public void Given_SixteenEnodesJoinedIntoTheOneStringTheFlagCarries_When_ItIsParsedAsASingleEnode_Then_ItIsRejected()
        {
            var joined = string.Join(",", SixteenTrustedPeers());

            Assert.False(EnodeUrl.TryParse(joined, out _));
        }
    }
}

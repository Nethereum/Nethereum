using Nethereum.MainnetChain.Configuration;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class ConsensusStartupGuardTests
    {
        private static MainnetChainServerConfig WithBeacon(string url) => new MainnetChainServerConfig
        {
            LightClient = new LightClientConfigSection { BeaconEndpoint = url }
        };

        [Fact]
        public void BeaconConfigured_IsVerified()
            => Assert.Equal(ConsensusStartupGuard.Decision.Verified,
                ConsensusStartupGuard.Evaluate(WithBeacon("http://localhost:5052")));

        [Fact]
        public void NoBeacon_NotAllowed_RefusesToStart()
            => Assert.Equal(ConsensusStartupGuard.Decision.RefuseNoBeacon,
                ConsensusStartupGuard.Evaluate(new MainnetChainServerConfig()));

        [Fact]
        public void NoBeacon_ExplicitlyAllowed_ProceedsUnverified()
            => Assert.Equal(ConsensusStartupGuard.Decision.UnverifiedAllowed,
                ConsensusStartupGuard.Evaluate(new MainnetChainServerConfig { AllowUnverifiedConsensus = true }));

        [Fact]
        public void BlankBeaconEndpoint_IsTreatedAsNoBeacon()
            => Assert.Equal(ConsensusStartupGuard.Decision.RefuseNoBeacon,
                ConsensusStartupGuard.Evaluate(WithBeacon("   ")));

        [Fact]
        public void CliFlag_SetsAllowUnverifiedConsensus()
        {
            var c = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(c, new[] { "--allow-unverified-consensus" });
            Assert.True(c.AllowUnverifiedConsensus);
        }
    }
}

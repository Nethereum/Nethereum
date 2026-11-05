using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient.Tests.SecurityAudit71.S8;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class LightClientConfigDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "The genesis time of a known network")]
        public void TryGetGenesisTime_AndCreateConfig_CarryTheNetworkGenesisTime()
        {
            Assert.True(LightClientNetworks.TryGetGenesisTime(new BigInteger(1), out var mainnet));
            Assert.Equal(LightClientNetworks.MainnetGenesisTime, mainnet);
            Assert.True(LightClientNetworks.TryGetGenesisTime(new BigInteger(11155111), out var sepolia));
            Assert.Equal(LightClientNetworks.SepoliaGenesisTime, sepolia);
            Assert.True(LightClientNetworks.TryGetGenesisTime(new BigInteger(17000), out var holesky));
            Assert.Equal(LightClientNetworks.HoleskyGenesisTime, holesky);
            Assert.False(LightClientNetworks.TryGetGenesisTime(new BigInteger(999), out var unknown));
            Assert.Equal(0UL, unknown);

            var config = LightClientNetworks.CreateConfig(new BigInteger(11155111));

            Assert.Equal(LightClientNetworks.SepoliaGenesisTime, config.GenesisTime);
            Assert.Equal(16UL, config.MaxCatchUpPeriods);
            Assert.False(config.AllowBeaconRebootstrap);
        }

        [Fact]
        [NethereumDocExample(DocSection.Consensus, "light-client", "The trusted header provider names itself")]
        public async Task TrustedHeaderProvider_NameIsLightClient()
        {
            var bootstrap = LightClientServiceTests.TestDataFactory.CreateBootstrap(S8Support.PeriodStart + 4000, 100);
            var (service, _) = await S8Support.StartAsync(bootstrap);

            Assert.Equal("LightClient", new TrustedHeaderProvider(service).Name);
            Assert.Equal(nameof(NamelessProvider), ((ITrustedHeaderProvider)new NamelessProvider()).Name);
        }

        private sealed class NamelessProvider : ITrustedHeaderProvider
        {
            public TrustedExecutionHeader GetLatestFinalized() => null;
            public TrustedExecutionHeader GetLatestOptimistic() => null;
            public byte[] GetBlockHash(ulong blockNumber) => null;
        }
    }
}

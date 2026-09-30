using System.Linq;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Peering;
using Xunit;

namespace Nethereum.DevP2P.UnitTests
{
    public class DevP2PConfigMaxPeersTests
    {
        [Fact]
        public void DialSchedulerOptions_HasNoMaxPeersProperty()
        {
            var propertyNames = typeof(DialSchedulerOptions)
                .GetProperties()
                .Select(p => p.Name)
                .ToArray();

            Assert.DoesNotContain("MaxPeers", propertyNames);
        }

        [Fact]
        public void SchedulerBuiltFromConfig_OutboundCap_DerivesFromConfigMaxPeers()
        {
            var config = new DevP2PConfig { MaxPeers = 25 };
            var scheduler = new DialScheduler(config.DialScheduler, config.MaxPeers);

            Assert.Equal((25 / 2) + 1, scheduler.OutboundCap);
        }

        [Fact]
        public void ChangingConfigMaxPeers_BeforeConstruction_ChangesTheDerivedCap()
        {
            var config = new DevP2PConfig { MaxPeers = 25 };

            config.MaxPeers = 9;
            var scheduler = new DialScheduler(config.DialScheduler, config.MaxPeers);

            Assert.Equal((9 / 2) + 1, scheduler.OutboundCap);
        }

        [Fact]
        public void ForDevChain_MaxPeersOverride_DerivesMatchingOutboundCap()
        {
            var config = DevP2PConfig.ForDevChain(genesisHash: new byte[32]);
            var scheduler = new DialScheduler(config.DialScheduler, config.MaxPeers);

            Assert.Equal(5, config.MaxPeers);
            Assert.Equal(3, scheduler.OutboundCap);
        }

        [Fact]
        public void ReplacingDialSchedulerOptions_DoesNotAffectOutboundCap()
        {
            var config = new DevP2PConfig { MaxPeers = 8 };
            config.DialScheduler = new DialSchedulerOptions { MaxActiveDials = 32 };

            var scheduler = new DialScheduler(config.DialScheduler, config.MaxPeers);

            Assert.Equal(32, config.DialScheduler.MaxActiveDials);
            Assert.Equal((8 / 2) + 1, scheduler.OutboundCap);
        }
    }
}

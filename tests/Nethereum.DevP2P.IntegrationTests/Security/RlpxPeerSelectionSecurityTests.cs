using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Peering;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxPeerSelectionSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public void Given_RemoteAdvertisingOnlyAnEthVersionWeDoNotSpeak_When_CapabilitiesAreNegotiated_Then_NoSharedEthCapability()
        {
            var local = new List<P2PCapability>
            {
                new P2PCapability { Name = "eth", Version = 68 },
                new P2PCapability { Name = "eth", Version = 69 },
                new P2PCapability { Name = "eth", Version = 70 },
                new P2PCapability { Name = "eth", Version = 71 }
            };
            var remote = new List<P2PCapability> { new P2PCapability { Name = "eth", Version = 66 } };

            var shared = CapabilityNegotiator.Negotiate(local, remote);

            Assert.DoesNotContain(shared, c => c.Name == "eth");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_ManyConcurrentReservationsOfTheSameCandidate_When_Reserved_Then_ExactlyOneDialIsAdmitted()
        {
            var violations = 0;
            for (var round = 0; round < 40; round++)
            {
                var scheduler = new DialScheduler(new DialSchedulerOptions { MaxActiveDials = 64 }, maxPeers: 200);
                var candidate = new DialCandidate("node-" + round);
                using var barrier = new Barrier(8);
                var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    return await scheduler.TryReserveSlotAsync(candidate, CancellationToken.None);
                })).ToArray();
                var results = await Task.WhenAll(tasks);
                if (results.Count(r => r) > 1) violations++;
            }

            Assert.Equal(0, violations);
        }
    }
}

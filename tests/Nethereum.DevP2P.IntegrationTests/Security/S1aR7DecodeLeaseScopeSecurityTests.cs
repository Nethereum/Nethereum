using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class S1aR7DecodeLeaseScopeSecurityTests
    {
        private const int SlowReaders = 3;

        public static IEnumerable<object[]> Loops()
        {
            yield return new object[] { LeaseLoop.Eth68ServerSession };
            yield return new object[] { LeaseLoop.MultiProtocolRlpxSession };
            yield return new object[] { LeaseLoop.PeerListener };
        }

        [Theory]
        [MemberData(nameof(Loops))]
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_02_Given_SlowReadersHoldingPaddedRequestsInTheHandler_When_AnHonestPeerSendsARequest_Then_ItIsDecodedAndServed(LeaseLoop loop)
        {
            var capacity = SlowReaders * (long)LeaseLoopHarness.PaddedRequestLength(loop);
            await using var harness = new LeaseLoopHarness(loop, capacity);
            await harness.StartAsync();

            for (var i = 0; i < SlowReaders; i++)
            {
                var slow = await harness.ConnectAsync();
                await harness.SendPaddedRequestAsync(slow);
            }
            Assert.True(await harness.WaitForSlowRequestsAsync(SlowReaders, TimeSpan.FromSeconds(30)),
                $"only {harness.SlowRequestsInHandlers} padded requests reached the handler");

            var honest = await harness.ConnectAsync();
            await harness.SendProbeRequestAsync(honest);

            var served = await Task.WhenAny(harness.ProbeServed, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(served == harness.ProbeServed,
                $"the honest request was not decoded while {harness.Limiter.InFlightBytes} of {harness.Limiter.CapacityBytes} limiter bytes were held by handlers");
        }

        [Theory]
        [MemberData(nameof(Loops))]
        [Trait("Category", "SecurityFinding")]
        public async Task S1aR7_02_Given_TheLimiterIsFull_When_APeerSendsARequest_Then_ThatLoopWaitsForALeaseBeforeDecoding(LeaseLoop loop)
        {
            await using var harness = new LeaseLoopHarness(loop, 1024 * 1024);
            await harness.StartAsync();
            var held = await harness.Limiter.AcquireAsync(harness.Limiter.CapacityBytes);

            var peer = await harness.ConnectAsync();
            await harness.SendProbeRequestAsync(peer);

            var servedWhileFull = await Task.WhenAny(harness.ProbeServed, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.False(servedWhileFull == harness.ProbeServed, "the request was decoded without a lease while the limiter was full");

            held.Dispose();

            var servedAfterRelease = await Task.WhenAny(harness.ProbeServed, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(servedAfterRelease == harness.ProbeServed, "the request was never served after the lease was released");
            Assert.Equal(0, harness.Limiter.InFlightBytes);
        }
    }
}

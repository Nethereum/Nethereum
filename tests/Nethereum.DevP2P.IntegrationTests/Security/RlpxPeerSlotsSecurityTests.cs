using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection("NoGcRegionExclusive")]
    public class RlpxPeerSlotsSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public void Given_ManyThreadsAcquiringTogetherRoundAfterRound_When_MaxPeersIsSmall_Then_NoRoundGrantsMoreThanMaxPeers()
        {
            const int maxPeers = 1;
            const int threads = 16;
            const int rounds = 5000;
            var slots = new RlpxPeerSlots(maxPeers);
            var granted = 0;
            var worst = 0;
            using var barrier = new Barrier(threads, _ =>
            {
                var g = Volatile.Read(ref granted);
                if (g > worst) worst = g;
                Volatile.Write(ref granted, 0);
                slots = new RlpxPeerSlots(maxPeers);
            });

            var workers = Enumerable.Range(0, threads).Select(_ => Task.Factory.StartNew(() =>
            {
                for (var round = 0; round < rounds && worst <= maxPeers; round++)
                {
                    if (Volatile.Read(ref slots).TryAcquire(false)) Interlocked.Increment(ref granted);
                    barrier.SignalAndWait();
                }
            }, TaskCreationOptions.LongRunning)).ToArray();
            Task.WaitAll(workers);

            Assert.True(worst <= maxPeers, $"{worst} slots granted with MaxPeers {maxPeers}");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_FullSlotsAndATrustedPeer_When_Acquiring_Then_TrustedIsGrantedAndReleaseFreesASlot()
        {
            var slots = new RlpxPeerSlots(1);

            Assert.True(slots.TryAcquire(false));
            Assert.False(slots.TryAcquire(false));
            Assert.True(slots.TryAcquire(true));
            Assert.Equal(2, slots.Active);

            slots.Release();
            slots.Release();

            Assert.Equal(0, slots.Active);
            Assert.True(slots.TryAcquire(false));
        }
    }
}

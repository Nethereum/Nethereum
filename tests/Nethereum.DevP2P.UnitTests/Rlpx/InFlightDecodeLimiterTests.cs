using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class InFlightDecodeLimiterTests
    {
        [Fact]
        public async Task Given_AcquisitionsWithinCapacity_When_Acquired_Then_TheyCompleteImmediatelyAndCountAsInFlight()
        {
            var limiter = new InFlightDecodeLimiter(100);

            var first = await limiter.AcquireAsync(40);
            var second = await limiter.AcquireAsync(60);

            Assert.Equal(100, limiter.InFlightBytes);
            first.Dispose();
            second.Dispose();
            Assert.Equal(0, limiter.InFlightBytes);
        }

        [Fact]
        public async Task Given_ALimiterAtCapacity_When_AnotherIsRequested_Then_ItWaitsUntilALeaseIsReleased()
        {
            var limiter = new InFlightDecodeLimiter(100);
            var held = await limiter.AcquireAsync(80);

            var waiting = limiter.AcquireAsync(30).AsTask();
            await Task.Delay(50);
            Assert.False(waiting.IsCompleted);

            held.Dispose();
            var lease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(30, limiter.InFlightBytes);
            lease.Dispose();
        }

        [Fact]
        public async Task Given_WaitersQueuedInOrder_When_CapacityFrees_Then_TheyAreAdmittedFirstInFirstOut()
        {
            var limiter = new InFlightDecodeLimiter(100);
            var held = await limiter.AcquireAsync(100);
            var order = new System.Collections.Concurrent.ConcurrentQueue<int>();

            var tasks = new Task[3];
            for (var i = 0; i < tasks.Length; i++)
            {
                var index = i;
                var acquire = limiter.AcquireAsync(100).AsTask();
                tasks[i] = acquire.ContinueWith(t =>
                {
                    order.Enqueue(index);
                    t.Result.Dispose();
                });
            }

            held.Dispose();
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { 0, 1, 2 }, order.ToArray());
        }

        [Fact]
        public async Task Given_ALeaseDisposedTwice_When_Released_Then_TheBytesAreReleasedOnce()
        {
            var limiter = new InFlightDecodeLimiter(100);
            var first = await limiter.AcquireAsync(50);
            var second = await limiter.AcquireAsync(50);

            first.Dispose();
            first.Dispose();

            Assert.Equal(50, limiter.InFlightBytes);
            second.Dispose();
        }

        [Fact]
        public async Task Given_AMessageLargerThanTheCapacity_When_TheLimiterIsIdle_Then_ItIsAdmittedAlone()
        {
            var limiter = new InFlightDecodeLimiter(10);

            var lease = await limiter.AcquireAsync(50).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var second = limiter.AcquireAsync(1).AsTask();
            await Task.Delay(50);

            Assert.False(second.IsCompleted);
            lease.Dispose();
            (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
        }

        [Fact]
        public async Task Given_AWaiterThatIsCancelled_When_CapacityFrees_Then_ItNeverHoldsBytesAndLaterWaitersProceed()
        {
            var limiter = new InFlightDecodeLimiter(100);
            var held = await limiter.AcquireAsync(100);
            using var cts = new CancellationTokenSource();

            var cancelled = limiter.AcquireAsync(100, cts.Token).AsTask();
            var next = limiter.AcquireAsync(100).AsTask();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

            held.Dispose();
            var lease = await next.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(100, limiter.InFlightBytes);
            lease.Dispose();
            Assert.Equal(0, limiter.InFlightBytes);
        }

        [Fact]
        public async Task Given_AlreadyCancelledToken_When_Acquired_Then_ItThrowsWithoutHoldingBytes()
        {
            var limiter = new InFlightDecodeLimiter(100);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await limiter.AcquireAsync(10, cts.Token));

            Assert.Equal(0, limiter.InFlightBytes);
        }

        [Fact]
        public void Given_NoConfiguredLimiter_When_TheSharedLimiterIsRead_Then_ItIsMaxPeersTimesFourMebibytes()
        {
            Assert.Equal(25L * 4 * 1024 * 1024, InFlightDecodeLimiter.Shared.CapacityBytes);
            Assert.Equal(10L * 4 * 1024 * 1024, InFlightDecodeLimiter.ForMaxPeers(10).CapacityBytes);
        }

        [Fact]
        public async Task Given_ManyConcurrentAcquirers_When_TheyRunTogether_Then_InFlightBytesNeverExceedCapacity()
        {
            var limiter = new InFlightDecodeLimiter(1000);
            long current = 0;
            long peak = 0;

            var tasks = new Task[64];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (var round = 0; round < 50; round++)
                    {
                        using (await limiter.AcquireAsync(300))
                        {
                            var now = Interlocked.Add(ref current, 300);
                            long observed;
                            while ((observed = Interlocked.Read(ref peak)) < now
                                   && Interlocked.CompareExchange(ref peak, now, observed) != observed) { }
                            await Task.Yield();
                            Interlocked.Add(ref current, -300);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(peak <= 1000, "peak " + peak);
            Assert.Equal(0, limiter.InFlightBytes);
        }
    }
}

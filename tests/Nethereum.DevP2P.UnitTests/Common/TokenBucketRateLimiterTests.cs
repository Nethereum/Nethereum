using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Common
{
    public class TokenBucketRateLimiterTests
    {
        [Fact]
        public void Given_HonestCadenceWithinRate_When_AcquireCalledOncePerInterval_Then_AllSucceed()
        {
            var limiter = new TokenBucketRateLimiter<string>(rate: 9, burst: 9);

            for (int i = 0; i < 9; i++)
            {
                Assert.True(limiter.TryAcquire("ip-A"));
            }

            Thread.Sleep(120);
            Assert.True(limiter.TryAcquire("ip-A"));
        }

        [Fact]
        public void Given_FreshBucket_When_BurstManyAcquires_Then_BurstSucceedAndNextFails()
        {
            const int burst = 9;
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: burst);

            for (int i = 0; i < burst; i++)
            {
                Assert.True(limiter.TryAcquire("ip-A"));
            }

            Assert.False(limiter.TryAcquire("ip-A"));
        }

        [Fact]
        public void Given_DrainedBucket_When_WaitedHalfSecond_Then_RefillAdmitsExpectedTokens()
        {
            var limiter = new TokenBucketRateLimiter<string>(rate: 10, burst: 10);

            for (int i = 0; i < 10; i++)
            {
                Assert.True(limiter.TryAcquire("ip-A"));
            }

            Thread.Sleep(500);

            int admitted = 0;
            for (int i = 0; i < 10; i++)
            {
                if (limiter.TryAcquire("ip-A")) admitted++;
                else break;
            }

            Assert.InRange(admitted, 3, 7);
        }

        [Fact]
        public void Given_TwoKeys_When_OneDrained_Then_OtherIsUnaffected()
        {
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: 3);

            for (int i = 0; i < 3; i++) Assert.True(limiter.TryAcquire("ip-A"));
            Assert.False(limiter.TryAcquire("ip-A"));

            for (int i = 0; i < 3; i++) Assert.True(limiter.TryAcquire("ip-B"));
            Assert.False(limiter.TryAcquire("ip-B"));
        }

        [Fact]
        public void Given_CacheAtMaxKeys_When_NewKeyArrives_Then_LeastRecentlyAccessedEvicted()
        {
            const int max = 4;
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: 1, maxCachedKeys: max);

            Assert.True(limiter.TryAcquire("k0"));
            Thread.Sleep(1);
            Assert.True(limiter.TryAcquire("k1"));
            Thread.Sleep(1);
            Assert.True(limiter.TryAcquire("k2"));
            Thread.Sleep(1);
            Assert.True(limiter.TryAcquire("k3"));

            Assert.Equal(max, limiter.CachedKeyCount);

            Thread.Sleep(1);
            Assert.False(limiter.TryAcquire("k1"));
            Thread.Sleep(1);
            Assert.False(limiter.TryAcquire("k2"));
            Thread.Sleep(1);
            Assert.False(limiter.TryAcquire("k3"));

            Thread.Sleep(1);
            limiter.TryAcquire("k4");

            Assert.Equal(max, limiter.CachedKeyCount);
        }

        [Fact]
        public void Given_DrainedBucket_When_ResetCalled_Then_BucketFullAgain()
        {
            const int burst = 4;
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: burst);

            for (int i = 0; i < burst; i++) Assert.True(limiter.TryAcquire("ip-A"));
            Assert.False(limiter.TryAcquire("ip-A"));

            limiter.Reset("ip-A");

            for (int i = 0; i < burst; i++) Assert.True(limiter.TryAcquire("ip-A"));
            Assert.False(limiter.TryAcquire("ip-A"));
        }

        [Fact]
        public void Given_UnknownKey_When_ResetCalled_Then_NoOp()
        {
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: 1);

            limiter.Reset("never-seen");

            Assert.Equal(0, limiter.CachedKeyCount);
        }

        [Theory]
        [InlineData(50, 9)]
        [InlineData(200, 16)]
        public async Task Given_NParallelAcquiresOnSameKey_When_RunConcurrently_Then_ExactlyBurstSucceedInFirstInstant(int parallel, int burst)
        {
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: burst);
            var barrier = new Barrier(parallel);
            int successes = 0;

            var tasks = new Task[parallel];
            for (int i = 0; i < parallel; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    if (limiter.TryAcquire("hot-key")) Interlocked.Increment(ref successes);
                });
            }
            await Task.WhenAll(tasks);

            Assert.InRange(successes, burst, burst + 1);
        }

        [Fact]
        public void Given_InvalidRate_When_Constructed_Then_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter<string>(rate: 0, burst: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter<string>(rate: 1, burst: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TokenBucketRateLimiter<string>(rate: 1, burst: 1, maxCachedKeys: 0));
        }

        [Fact]
        public void Given_TokensGreaterThanBurst_When_Acquired_Then_Rejected()
        {
            var limiter = new TokenBucketRateLimiter<string>(rate: 1, burst: 4);

            Assert.False(limiter.TryAcquire("ip-A", tokens: 5));
            Assert.True(limiter.TryAcquire("ip-A", tokens: 4));
        }
    }
}

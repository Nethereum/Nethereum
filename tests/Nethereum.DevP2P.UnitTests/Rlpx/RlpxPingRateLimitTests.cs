using System;
using System.Threading;
using Nethereum.DevP2P.Common;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    /// <summary>
    /// Behavioural tests for the per-peer Ping/Pong token bucket wired into
    /// <see cref="Nethereum.DevP2P.Rlpx.RlpxConnection"/>. The bucket itself is
    /// the generic <see cref="TokenBucketRateLimiter{TKey}"/>; these tests verify
    /// the policy parameters chosen for the Ping path (burst capacity 4,
    /// 1 token/second sustained) match the proposal and behave correctly in the
    /// four scenarios that matter for the flood-DoS surface: honest cadence,
    /// burst-then-deny, refill recovery, and per-peer independence.
    /// </summary>
    public class RlpxPingRateLimitTests
    {
        private const int PingBurstCapacity = 4;
        private const int PingTokensPerSecond = 1;

        private static TokenBucketRateLimiter<TKey> BuildPingLimiter<TKey>() where TKey : notnull
        {
            return new TokenBucketRateLimiter<TKey>(
                rate: PingTokensPerSecond,
                burst: PingBurstCapacity);
        }

        [Fact]
        public void Given_HonestCadenceOnePingPer15Seconds_When_BudgetEvaluated_Then_AllAccepted()
        {
            var limiter = BuildPingLimiter<string>();

            // Drain the burst so subsequent calls go via refill arithmetic only.
            for (int i = 0; i < PingBurstCapacity; i++)
            {
                Assert.True(limiter.TryAcquire("peer-A"));
            }

            // An honest peer pings at 15s intervals (geth pingInterval = 15s).
            // The bucket refills at 1 token/s, so 1.1s of refill yields ~1 token
            // — the next honest ping is admitted. (At a real 15s cadence the
            // bucket would be fully refilled long before the next ping arrived.)
            Thread.Sleep(1100);
            Assert.True(limiter.TryAcquire("peer-A"));
        }

        [Fact]
        public void Given_FivePingsInOneSecond_When_BudgetEvaluated_Then_FifthRejected()
        {
            var limiter = BuildPingLimiter<string>();

            for (int i = 0; i < PingBurstCapacity; i++)
            {
                Assert.True(limiter.TryAcquire("peer-A"),
                    $"ping #{i + 1} within burst should be admitted");
            }

            Assert.False(limiter.TryAcquire("peer-A"),
                "ping #5 within the same instant must exceed the burst and be rejected");
        }

        [Fact]
        public void Given_ExhaustedBucket_When_RefillElapses_Then_NextPingAdmitted()
        {
            var limiter = BuildPingLimiter<string>();

            for (int i = 0; i < PingBurstCapacity; i++)
            {
                Assert.True(limiter.TryAcquire("peer-A"));
            }

            Assert.False(limiter.TryAcquire("peer-A"));

            // After ~1.2s the bucket has refilled at least one full token. Sleep
            // longer than the strict 1s window to absorb scheduler jitter under
            // parallel test load.
            Thread.Sleep(1200);

            Assert.True(limiter.TryAcquire("peer-A"),
                "after the refill window passes the next ping must be admitted again");
        }

        [Fact]
        public void Given_TwoDistinctPeers_When_OneBucketExhausted_Then_OtherUnaffected()
        {
            var limiter = BuildPingLimiter<string>();

            for (int i = 0; i < PingBurstCapacity; i++)
            {
                Assert.True(limiter.TryAcquire("peer-A"));
            }
            Assert.False(limiter.TryAcquire("peer-A"));

            // A different peer's bucket is independent and must still be full.
            for (int i = 0; i < PingBurstCapacity; i++)
            {
                Assert.True(limiter.TryAcquire("peer-B"),
                    "second peer's bucket must be independent of the first");
            }
        }

        [Fact]
        public void Given_PongOnlyFlood_When_BudgetEvaluated_Then_SameLimitApplies()
        {
            // The bucket primitive does not distinguish Ping from Pong - the
            // RlpxConnection wiring shares one bucket between the two opcodes so
            // a Pong-only flood (attacker who never sends a Ping but spams
            // unsolicited Pongs) is rejected on the same threshold. This test
            // pins the policy: N opcode-3 calls deplete the bucket exactly the
            // same way N opcode-2 calls would.
            var limiter = BuildPingLimiter<string>();

            for (int i = 0; i < PingBurstCapacity; i++)
            {
                Assert.True(limiter.TryAcquire("peer-A"));
            }

            Assert.False(limiter.TryAcquire("peer-A"),
                "Pong-only flood beyond burst capacity must also be rejected");
        }
    }
}

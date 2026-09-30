using System.Collections.Concurrent;
using Nethereum.DevP2P.Netutil;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Netutil
{
    public class ConcurrentCounterTests
    {
        [Fact]
        public void Given_BelowCap_When_TryReserve_Then_AdmitsAndIncrements()
        {
            var counts = new ConcurrentDictionary<string, int>();

            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 3));
            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 3));

            Assert.Equal(2, counts["a"]);
        }

        [Fact]
        public void Given_AtCap_When_TryReserve_Then_RefusesWithoutIncrement()
        {
            var counts = new ConcurrentDictionary<string, int>();
            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 2));
            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 2));

            Assert.False(ConcurrentCounter.TryReserve(counts, "a", 2));

            Assert.Equal(2, counts["a"]);
        }

        [Fact]
        public void Given_ZeroCap_When_TryReserve_Then_RefusesEvenFirst()
        {
            var counts = new ConcurrentDictionary<string, int>();

            Assert.False(ConcurrentCounter.TryReserve(counts, "a", 0));
            Assert.False(ConcurrentCounter.TryReserve(counts, "a", -1));

            Assert.False(counts.ContainsKey("a"));
        }

        [Fact]
        public void Given_ReservedToCap_When_DecrementOrRemove_Then_RestoresAndReReserves()
        {
            var counts = new ConcurrentDictionary<string, int>();
            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 2));
            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 2));
            Assert.False(ConcurrentCounter.TryReserve(counts, "a", 2));

            ConcurrentCounter.DecrementOrRemove(counts, "a");
            Assert.Equal(1, counts["a"]);

            ConcurrentCounter.DecrementOrRemove(counts, "a");
            Assert.False(counts.ContainsKey("a"));

            Assert.True(ConcurrentCounter.TryReserve(counts, "a", 2));
            Assert.Equal(1, counts["a"]);
        }

        [Fact]
        public void Given_ManyThreadsRacingFreshKeys_When_TryReserveCapOne_Then_ExactlyOneWinnerEachRound()
        {
            const int rounds = 20000;
            const int threads = 4;

            var counts = new ConcurrentDictionary<int, int>();
            var winnersPerRound = new int[rounds];
            var barrier = new Barrier(threads);

            Parallel.For(0, threads, _ =>
            {
                for (int round = 0; round < rounds; round++)
                {
                    barrier.SignalAndWait();
                    if (ConcurrentCounter.TryReserve(counts, round, 1))
                        Interlocked.Increment(ref winnersPerRound[round]);
                }
            });

            for (int round = 0; round < rounds; round++)
                Assert.Equal(1, winnersPerRound[round]);
        }
    }
}

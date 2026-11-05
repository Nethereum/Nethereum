using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class BackoffTests
    {
        private static List<int> Waits(Backoff backoff, int count, int capMs)
        {
            var waits = new List<int>();
            for (var i = 0; i < count; i++)
            {
                waits.Add(backoff.Current);
                backoff.Advance(capMs);
            }
            return waits;
        }

        [Fact]
        public void Given_FiveBackoffRanges_When_Doubled_Then_EveryStepEqualsTheOldArithmetic()
        {
            PivotAcquisitionRange();
            SnapAttemptRangeWithAChangingCap();
            Phase1StallRange();
            EmptyDispatchRangeWithResets();
            FreezerRoundFailureRange();
        }

        private static void PivotAcquisitionRange()
        {
            var old = 500;
            var expected = new List<int>();
            for (var i = 0; i < 12; i++)
            {
                expected.Add(old);
                old = Math.Min(old * 2, 10_000);
            }

            Assert.Equal(expected, Waits(new Backoff(500), 12, 10_000));
            Assert.Equal(new[] { 500, 1_000, 2_000, 4_000, 8_000, 10_000, 10_000 }, expected.Take(7));
        }

        private static void SnapAttemptRangeWithAChangingCap()
        {
            var caps = new[] { 30_000, 30_000, 30_000, 30_000, 30_000, 30_000, 600_000, 600_000, 600_000, 600_000, 600_000, 600_000, 600_000, 600_000 };
            var old = 2_000;
            var backoff = new Backoff(2_000);
            foreach (var cap in caps)
            {
                Assert.Equal(old, backoff.Current);
                old = Math.Min(old * 2, cap);
                backoff.Advance(cap);
            }

            Assert.Equal(600_000, backoff.Current);
        }

        private static void Phase1StallRange()
        {
            var max = TimeSpan.FromSeconds(30);
            var old = TimeSpan.FromSeconds(2);
            var backoff = new Backoff(2_000);
            for (var i = 0; i < 10; i++)
            {
                Assert.Equal(old, TimeSpan.FromMilliseconds(backoff.Current));
                var doubled = old.Ticks * 2;
                old = doubled > max.Ticks ? max : TimeSpan.FromTicks(doubled);
                backoff.Advance(30_000);
            }
        }

        private static void EmptyDispatchRangeWithResets()
        {
            var backoff = new Backoff(1_000);
            var consecutive = 0;
            var script = new[] { 'e', 'e', 'e', 'r', 'e', 'e', 'e', 'e', 'e', 'e', 'e', 'e', 'e', 'e', 'e', 'e', 'r', 'e' };
            foreach (var step in script)
            {
                if (step == 'r')
                {
                    consecutive = 0;
                    backoff.Reset();
                    continue;
                }

                consecutive++;
                var old = Math.Min(1_000 * (1 << Math.Min(consecutive - 1, 8)), 30_000);
                Assert.Equal(old, backoff.Current);
                backoff.Advance(30_000);
            }
        }

        private static void FreezerRoundFailureRange()
        {
            var old = TimeSpan.Zero;
            var backoff = new Backoff(1_000);
            for (var i = 0; i < 10; i++)
            {
                var next = old == TimeSpan.Zero ? TimeSpan.FromSeconds(1) : old + old;
                old = next > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : next;
                Assert.Equal(old, TimeSpan.FromMilliseconds(backoff.Current));
                backoff.Advance(30_000);
            }
        }

        [Fact]
        public void Given_FreezerRoundFailures_When_BackedOff_Then_WaitsAre1_2_4_8_16_30_30_AndResetOnSuccess()
        {
            var backoff = new Backoff(1_000);

            Assert.Equal(new[] { 1_000, 2_000, 4_000, 8_000, 16_000, 30_000, 30_000 }, Waits(backoff, 7, 30_000));

            backoff.Reset();

            Assert.Equal(new[] { 1_000, 2_000, 4_000 }, Waits(backoff, 3, 30_000));
        }

        [Fact]
        public void Given_ACapBelowTheCurrentWait_When_Advanced_Then_TheWaitDropsToTheCap()
        {
            var backoff = new Backoff(40_000);

            backoff.Advance(30_000);

            Assert.Equal(30_000, backoff.Current);
        }

        [Fact]
        public void Given_AFreshBackoff_When_ReadBeforeAdvancing_Then_CurrentIsTheInitialValue()
        {
            Assert.Equal(500, new Backoff(500).Current);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Given_ANonPositiveInitialValue_When_Constructed_Then_ArgumentOutOfRangeException(int initialMs)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Backoff(initialMs));
        }
    }
}

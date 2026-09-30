using System;
using Nethereum.DevP2P.Sync;
using Xunit;
using Nethereum.DevP2P.Sync.Common;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class ProgressStallDetectorTests
    {
        private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

        [Fact]
        public void NeverStalls_WhileTheValueKeepsAdvancing()
        {
            var d = new ProgressStallDetector(TimeSpan.FromMinutes(10));
            Assert.False(d.Observe(100, T0));
            Assert.False(d.Observe(101, T0.AddMinutes(8)));
            Assert.False(d.Observe(102, T0.AddMinutes(16)));
            Assert.False(d.Observe(102, T0.AddMinutes(18)));
        }

        [Fact]
        public void Stalls_WhenFrozenPastThreshold_AndRecoversOnAdvance()
        {
            var d = new ProgressStallDetector(TimeSpan.FromMinutes(10));
            Assert.False(d.Observe(100, T0));
            Assert.False(d.Observe(100, T0.AddMinutes(9)));
            Assert.True(d.Observe(100, T0.AddMinutes(10)));
            Assert.True(d.Observe(100, T0.AddMinutes(99)));
            Assert.False(d.Observe(101, T0.AddMinutes(100)));
            Assert.Equal(TimeSpan.Zero, d.StalledFor(T0.AddMinutes(100)));
        }

        [Fact]
        public void StalledFor_TracksTheFrozenDuration()
        {
            var d = new ProgressStallDetector(TimeSpan.FromMinutes(10));
            d.Observe(5, T0);
            d.Observe(5, T0.AddMinutes(7));
            Assert.Equal(TimeSpan.FromMinutes(7), d.StalledFor(T0.AddMinutes(7)));
        }
    }
}

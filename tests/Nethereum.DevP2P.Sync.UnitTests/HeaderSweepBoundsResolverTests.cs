using Nethereum.DevP2P.Sync;
using Xunit;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class HeaderSweepBoundsResolverTests
    {
        [Fact]
        public void Override_WinsOverCursor()
        {
            var (from, to, src) = HeaderSweepBoundsResolver.ResolveSweep((22_918_986UL, 0UL), pivot: 25_457_614, cursor: 1_010_364, cursorHeaderExists: true);
            Assert.Equal(22_918_986UL, from);
            Assert.Equal(0UL, to);
            Assert.Equal(HeaderSweepSource.Override, src);
        }

        [Fact]
        public void Cursor_UsedWhenPresentAndItsHeaderExists()
        {
            var (from, to, src) = HeaderSweepBoundsResolver.ResolveSweep(null, pivot: 100, cursor: 40, cursorHeaderExists: true);
            Assert.Equal(40UL, from);
            Assert.Equal(0UL, to);
            Assert.Equal(HeaderSweepSource.Cursor, src);
        }

        [Fact]
        public void StaleCursor_FallsBackToPivot_AndIsFlagged()
        {
            var (from, _, src) = HeaderSweepBoundsResolver.ResolveSweep(null, pivot: 25_457_614, cursor: 1_010_364, cursorHeaderExists: false);
            Assert.Equal(25_457_614UL, from);
            Assert.Equal(HeaderSweepSource.CursorStale, src);
        }

        [Fact]
        public void NoCursor_SweepsFromPivot()
        {
            var (from, _, src) = HeaderSweepBoundsResolver.ResolveSweep(null, pivot: 100, cursor: 0, cursorHeaderExists: false);
            Assert.Equal(100UL, from);
            Assert.Equal(HeaderSweepSource.Pivot, src);
        }

        [Fact]
        public void CursorAtOrAbovePivot_SweepsFromPivot()
        {
            var (from, _, src) = HeaderSweepBoundsResolver.ResolveSweep(null, pivot: 100, cursor: 100, cursorHeaderExists: true);
            Assert.Equal(100UL, from);
            Assert.Equal(HeaderSweepSource.Pivot, src);
        }
    }
}

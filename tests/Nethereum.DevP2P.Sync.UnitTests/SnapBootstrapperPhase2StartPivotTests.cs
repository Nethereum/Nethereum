using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Model;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperPhase2StartPivotTests
    {
        private static byte[] Fill(byte b) { var a = new byte[32]; for (int i = 0; i < 32; i++) a[i] = b; return a; }

        private static BlockHeader Header(int blockNumber, byte stateRootFill)
            => new() { BlockNumber = blockNumber, StateRoot = Fill(stateRootFill) };

        [Fact]
        public async Task Given_ARefresherWithALaterTip_When_AnchoringAtPhase2Start_Then_ItOpensAgainstTheFreshRootNotTheStaleBootPivot()
        {
            var bootPivot = Header(100, 0x01);
            var rollingPivot = new SnapBootstrapper.RollingPivot(bootPivot, Fill(0x11));
            var freshHeader = Header(350, 0x02);
            var freshHash = Fill(0x22);

            var targetRoot = await SnapBootstrapper.AnchorFreshPivotAtStartAsync(
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>((freshHeader, freshHash)),
                rollingPivot, bootPivot, NullLogger.Instance, default);

            Assert.Equal(freshHeader.StateRoot, targetRoot);
            Assert.NotEqual(bootPivot.StateRoot, targetRoot);
            Assert.Equal(freshHeader.StateRoot, rollingPivot.Current.Header.StateRoot);
        }

        [Fact]
        public async Task Given_AnchoringAtPhase2Start_When_ItAsksTheRefresher_Then_ItForcesAFreshPivotRatherThanTheStaleGatedRefresh()
        {
            bool? observedForceFresh = null;
            var bootPivot = Header(100, 0x01);
            var rollingPivot = new SnapBootstrapper.RollingPivot(bootPivot, Fill(0x11));

            await SnapBootstrapper.AnchorFreshPivotAtStartAsync(
                (forceFresh, ct) =>
                {
                    observedForceFresh = forceFresh;
                    return Task.FromResult<(BlockHeader, byte[])?>((Header(120, 0x02), Fill(0x22)));
                },
                rollingPivot, bootPivot, NullLogger.Instance, default);

            Assert.True(observedForceFresh);
        }

        [Fact]
        public async Task Given_TheRefresherReturnsNull_When_AnchoringAtPhase2Start_Then_ItFallsBackToTheBootPivotRootUnchanged()
        {
            var bootPivot = Header(100, 0x01);
            var rollingPivot = new SnapBootstrapper.RollingPivot(bootPivot, Fill(0x11));

            var targetRoot = await SnapBootstrapper.AnchorFreshPivotAtStartAsync(
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>(null),
                rollingPivot, bootPivot, NullLogger.Instance, default);

            Assert.Equal(bootPivot.StateRoot, targetRoot);
            Assert.Equal(bootPivot.StateRoot, rollingPivot.Current.Header.StateRoot);
        }

        [Fact]
        public async Task Given_NoRefresher_When_AnchoringAtPhase2Start_Then_ItUsesTheBootPivotRootWithoutFailing()
        {
            var bootPivot = Header(100, 0x01);
            var rollingPivot = new SnapBootstrapper.RollingPivot(bootPivot, Fill(0x11));

            var targetRoot = await SnapBootstrapper.AnchorFreshPivotAtStartAsync(
                null, rollingPivot, bootPivot, NullLogger.Instance, default);

            Assert.Equal(bootPivot.StateRoot, targetRoot);
        }

        [Fact]
        public async Task Given_TheRefresherReturnsAnOlderPivotThanCurrent_When_AnchoringAtPhase2Start_Then_ItKeepsTheHigherRollingPivot()
        {
            var bootPivot = Header(500, 0x05);
            var rollingPivot = new SnapBootstrapper.RollingPivot(bootPivot, Fill(0x55));
            var olderHeader = Header(300, 0x03);

            var targetRoot = await SnapBootstrapper.AnchorFreshPivotAtStartAsync(
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>((olderHeader, Fill(0x33))),
                rollingPivot, bootPivot, NullLogger.Instance, default);

            Assert.Equal(bootPivot.StateRoot, targetRoot);
            Assert.NotEqual(olderHeader.StateRoot, targetRoot);
        }
    }
}

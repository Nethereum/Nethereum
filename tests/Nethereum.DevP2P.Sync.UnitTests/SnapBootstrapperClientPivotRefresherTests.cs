using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperClientPivotRefresherTests
    {
        private static byte[] Fill(byte b) { var a = new byte[32]; for (int i = 0; i < 32; i++) a[i] = b; return a; }

        private static BlockHeader Header(int blockNumber, byte stateRootFill)
            => new() { BlockNumber = blockNumber, StateRoot = Fill(stateRootFill) };

        [Fact]
        public async Task Given_TheRefresherReturnsAFreshPivot_When_TheClientPivotRefresherRuns_Then_ItAdoptsIntoTheRollingPivotAndReturnsIt()
        {
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, 0x01), Fill(0x11));
            var freshHeader = Header(200, 0x02);
            var freshHash = Fill(0x22);
            var refresher = SnapBootstrapper.BuildClientPivotRefresher(
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>((freshHeader, freshHash)),
                rollingPivot,
                NullLogger.Instance);

            var result = await refresher(default);

            Assert.Equal(freshHeader.StateRoot, result);
            Assert.Equal(freshHeader.StateRoot, rollingPivot.Current.Header.StateRoot);
            Assert.Equal(freshHash, rollingPivot.Current.Hash);
        }

        [Fact]
        public async Task Given_TheRefresherReturnsNull_When_TheClientPivotRefresherRuns_Then_ItFallsBackToTheCurrentRollingPivotUnchanged()
        {
            var originalHeader = Header(100, 0x01);
            var originalHash = Fill(0x11);
            var rollingPivot = new SnapBootstrapper.RollingPivot(originalHeader, originalHash);
            var refresher = SnapBootstrapper.BuildClientPivotRefresher(
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>(null),
                rollingPivot,
                NullLogger.Instance);

            var result = await refresher(default);

            Assert.Equal(originalHeader.StateRoot, result);
            Assert.Equal(originalHash, rollingPivot.Current.Hash);
        }

        [Fact]
        public async Task Given_TheClientPivotRefresherRuns_When_Called_Then_ItAlwaysPassesForceFreshFalseToTheCallersRefresher()
        {
            bool? observedForceFresh = null;
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, 0x01), Fill(0x11));
            var refresher = SnapBootstrapper.BuildClientPivotRefresher(
                (forceFresh, ct) =>
                {
                    observedForceFresh = forceFresh;
                    return Task.FromResult<(BlockHeader, byte[])?>(null);
                },
                rollingPivot,
                NullLogger.Instance);

            await refresher(default);

            Assert.False(observedForceFresh);
        }

        [Fact]
        public async Task Given_AnotherConsumerAlreadyAdoptedAHigherPivot_When_TheClientPivotRefresherRuns_Then_ItReturnsTheAuthoritativeRollingPivotNotItsOwnLowerLocalRotate()
        {
            var rollingPivot = new SnapBootstrapper.RollingPivot(Header(100, 0x01), Fill(0x11));
            var higherHeader = Header(500, 0x99);
            var higherHash = Fill(0x99);
            rollingPivot.Adopt(new SnapBootstrapper.PivotState(higherHeader, higherHash));

            var lowerRolledHeader = Header(300, 0x02);
            var lowerRolledHash = Fill(0x22);
            var refresher = SnapBootstrapper.BuildClientPivotRefresher(
                (forceFresh, ct) => Task.FromResult<(BlockHeader, byte[])?>((lowerRolledHeader, lowerRolledHash)),
                rollingPivot,
                NullLogger.Instance);

            var result = await refresher(default);

            Assert.Equal(higherHeader.StateRoot, result);
            Assert.NotEqual(lowerRolledHeader.StateRoot, result);
        }
    }
}

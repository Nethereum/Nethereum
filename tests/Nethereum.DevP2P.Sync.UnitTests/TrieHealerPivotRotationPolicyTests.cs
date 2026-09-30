using Nethereum.DevP2P.Sync;
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
    public class TrieHealerPivotRotationPolicyTests
    {
        private const ulong Stale = 128;

        [Fact]
        public void SameRoot_NeverRotates()
        {
            Assert.False(TrieHealer.ShouldRotateHealPivot(stalled: false, rootChanged: false, currentPivotBlock: 1000, newPivotBlock: 2000, staleDistanceBlocks: Stale));
            Assert.False(TrieHealer.ShouldRotateHealPivot(stalled: true, rootChanged: false, currentPivotBlock: 1000, newPivotBlock: 2000, staleDistanceBlocks: Stale));
        }

        [Fact]
        public void Stalled_RotatesRegardlessOfDistance()
        {
            Assert.True(TrieHealer.ShouldRotateHealPivot(stalled: true, rootChanged: true, currentPivotBlock: 1000, newPivotBlock: 1001, staleDistanceBlocks: Stale));
        }

        [Fact]
        public void SmallForwardMove_DoesNotRotate()
        {
            Assert.False(TrieHealer.ShouldRotateHealPivot(stalled: false, rootChanged: true, currentPivotBlock: 1000, newPivotBlock: 1000 + 64, staleDistanceBlocks: Stale));
            Assert.False(TrieHealer.ShouldRotateHealPivot(stalled: false, rootChanged: true, currentPivotBlock: 1000, newPivotBlock: 1000 + 127, staleDistanceBlocks: Stale));
        }

        [Fact]
        public void StaleForwardMove_Rotates()
        {
            Assert.True(TrieHealer.ShouldRotateHealPivot(stalled: false, rootChanged: true, currentPivotBlock: 1000, newPivotBlock: 1000 + 128, staleDistanceBlocks: Stale));
            Assert.True(TrieHealer.ShouldRotateHealPivot(stalled: false, rootChanged: true, currentPivotBlock: 1000, newPivotBlock: 1000 + 500, staleDistanceBlocks: Stale));
        }

    }
}

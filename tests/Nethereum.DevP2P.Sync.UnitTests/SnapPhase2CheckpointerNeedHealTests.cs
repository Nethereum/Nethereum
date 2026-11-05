using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2CheckpointerNeedHealTests
    {
        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private static SnapPhase2Checkpointer NewCheckpointer(SnapTaskSet set, List<SnapSyncClient.SnapSyncCheckpoint> captured)
            => new SnapPhase2Checkpointer(new SnapPhase2State(null, null), set, null, captured.Add, bytesThreshold: 1);

        [Fact]
        public void Given_AnAccountMarkedNeedHeal_When_ACheckpointIsEmitted_Then_TheCheckpointCarriesIt()
        {
            var set = new SnapTaskSet(1);
            var marked = Acc(0x21);
            set.MarkNeedHeal(marked);
            var captured = new List<SnapSyncClient.SnapSyncCheckpoint>();

            NewCheckpointer(set, captured).Emit(SnapPhase.Phase2Running, healTargetRoot: null);

            var checkpoint = Assert.Single(captured);
            var carried = Assert.Single(checkpoint.AccountsNeedingHeal);
            Assert.Equal(marked, carried.AccountHash, ByteArrayComparer.Current);
        }

        [Fact]
        public void Given_NoAccountMarkedNeedHeal_When_ACheckpointIsEmitted_Then_TheCheckpointCarriesNone()
        {
            var set = new SnapTaskSet(1);
            var captured = new List<SnapSyncClient.SnapSyncCheckpoint>();

            NewCheckpointer(set, captured).Emit(SnapPhase.Phase2Running, healTargetRoot: null);

            var checkpoint = Assert.Single(captured);
            Assert.Empty(checkpoint.AccountsNeedingHeal);
        }
    }
}

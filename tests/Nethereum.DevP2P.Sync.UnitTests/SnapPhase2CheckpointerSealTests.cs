using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2CheckpointerSealTests
    {
        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        [Fact]
        public void Given_TheFinalCheckpointWasEmitted_When_AStragglerCompletesARangeAndCrossesTheThreshold_Then_NoFurtherCheckpointIsPersisted()
        {
            var set = new SnapTaskSet(1);
            var originalNext = set.Tasks[0].Next;
            var persisted = new List<SnapSyncState>();
            var checkpointer = new SnapPhase2Checkpointer(
                new SnapPhase2State(null, null), set, null, cp => persisted.Add(cp.State), bytesThreshold: 1);
            var straggler = (SnapFragment.AccountRange)set.LeaseNext();

            checkpointer.EmitFinal(SnapPhase.Phase2Running, healTargetRoot: null);
            var acct = Acc(0x10);
            set.CompleteAccountRange(straggler,
                new List<AccountClassification> { new(acct, DefaultValues.EMPTY_TRIE_HASH, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: acct, done: true);
            checkpointer.MaybeCheckpoint(1);
            checkpointer.Emit(SnapPhase.Phase2Running, healTargetRoot: null);

            var only = Assert.Single(persisted);
            Assert.Equal(originalNext, only.Tasks[0].Next, ByteArrayComparer.Current);
        }

        [Fact]
        public void Given_NoFinalCheckpointYet_When_TheThresholdIsCrossed_Then_APeriodicCheckpointIsPersisted()
        {
            var set = new SnapTaskSet(1);
            var persisted = new List<SnapSyncState>();
            var checkpointer = new SnapPhase2Checkpointer(
                new SnapPhase2State(null, null), set, null, cp => persisted.Add(cp.State), bytesThreshold: 1);

            checkpointer.MaybeCheckpoint(1);
            checkpointer.EmitFinal(SnapPhase.Phase2Running, healTargetRoot: null);

            Assert.Equal(2, persisted.Count);
        }
    }
}

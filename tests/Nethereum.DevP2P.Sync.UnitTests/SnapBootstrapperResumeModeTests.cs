using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Util;
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
    public class SnapBootstrapperResumeModeTests
    {
        [Fact]
        public void Phase3_WithMovedPivot_ResumesHeal_NotPhase2()
        {
            Assert.Equal(SnapResumeMode.Phase3Heal,
                SnapBootstrapper.DecideResumeMode(SnapPhase.Phase3Running, healTargetValid: true, pivotMoved: true));
        }

        [Fact]
        public void Phase3_WithUnchangedPivot_ResumesHeal()
        {
            Assert.Equal(SnapResumeMode.Phase3Heal,
                SnapBootstrapper.DecideResumeMode(SnapPhase.Phase3Running, healTargetValid: true, pivotMoved: false));
        }

        [Fact]
        public void Phase3_WithInvalidHealTarget_FallsBackToPhase2()
        {
            Assert.Equal(SnapResumeMode.Phase2,
                SnapBootstrapper.DecideResumeMode(SnapPhase.Phase3Running, healTargetValid: false, pivotMoved: true));
        }

        [Fact]
        public void Phase2_ResumesPhase2_MovedOrNot()
        {
            Assert.Equal(SnapResumeMode.Phase2,
                SnapBootstrapper.DecideResumeMode(SnapPhase.Phase2Running, healTargetValid: false, pivotMoved: true));
            Assert.Equal(SnapResumeMode.Phase2,
                SnapBootstrapper.DecideResumeMode(SnapPhase.Phase2Running, healTargetValid: false, pivotMoved: false));
        }

        [Fact]
        public void Complete_UnchangedPivot_ClearsOrphan()
        {
            Assert.Equal(SnapResumeMode.ClearOrphan,
                SnapBootstrapper.DecideResumeMode(SnapPhase.Complete, healTargetValid: false, pivotMoved: false));
        }

        [Fact]
        public void NotStarted_IsFresh()
        {
            Assert.Equal(SnapResumeMode.Fresh,
                SnapBootstrapper.DecideResumeMode(SnapPhase.NotStarted, healTargetValid: false, pivotMoved: false));
        }


        private static SnapSyncState SavedStateAt(ulong pivotBlockNumber, byte[] pivotBlockHash) => new()
        {
            SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
            Phase = SnapPhase.Phase2Running,
            PivotBlockNumber = pivotBlockNumber,
            PivotBlockHash = pivotBlockHash,
            HealTargetRoot = new byte[32],
            Tasks = System.Array.Empty<SnapSyncAccountTask>(),
            Counters = SnapSyncCounters.Zero,
        };

        [Fact]
        public void PivotMoved_SameNumber_DifferentHash_IsMoved()
        {
            var savedHash = new byte[32]; savedHash[0] = 0xAA;
            var currentHash = new byte[32]; currentHash[0] = 0xBB;
            var savedState = SavedStateAt(pivotBlockNumber: 100, pivotBlockHash: savedHash);
            var pivot = new BlockHeader { BlockNumber = (EvmUInt256)100 };

            Assert.True(SnapBootstrapper.PivotMoved(savedState, pivot, currentHash),
                "a same-height reorg (same block number, different hash) must be classified as a pivot move");
        }

        [Fact]
        public void PivotMoved_SameNumber_SameHash_IsNotMoved()
        {
            var hash = new byte[32]; hash[0] = 0xAA;
            var savedState = SavedStateAt(pivotBlockNumber: 100, pivotBlockHash: hash);
            var pivot = new BlockHeader { BlockNumber = (EvmUInt256)100 };

            Assert.False(SnapBootstrapper.PivotMoved(savedState, pivot, (byte[])hash.Clone()));
        }

        [Fact]
        public void PivotMoved_DifferentNumber_SameHash_IsMoved()
        {
            var hash = new byte[32]; hash[0] = 0xAA;
            var savedState = SavedStateAt(pivotBlockNumber: 100, pivotBlockHash: hash);
            var pivot = new BlockHeader { BlockNumber = (EvmUInt256)200 };

            Assert.True(SnapBootstrapper.PivotMoved(savedState, pivot, hash));
        }

        [Fact]
        public void PivotMoved_DifferentNumber_DifferentHash_IsMoved()
        {
            var savedHash = new byte[32]; savedHash[0] = 0xAA;
            var currentHash = new byte[32]; currentHash[0] = 0xBB;
            var savedState = SavedStateAt(pivotBlockNumber: 100, pivotBlockHash: savedHash);
            var pivot = new BlockHeader { BlockNumber = (EvmUInt256)200 };

            Assert.True(SnapBootstrapper.PivotMoved(savedState, pivot, currentHash));
        }
    }
}

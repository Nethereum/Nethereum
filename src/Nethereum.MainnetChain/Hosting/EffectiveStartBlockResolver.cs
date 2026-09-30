using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Configuration;

namespace Nethereum.MainnetChain.Hosting
{
    public static class EffectiveStartBlockResolver
    {
        public readonly record struct EffectiveBounds(
            ulong StartBlock,
            ulong? EndBlock,
            StartBlockReason Reason);

        public enum StartBlockReason
        {
            FreshStart,
            ResumeFromLastBlock,
            PostSnapPivotFastStart,
        }

        public static EffectiveBounds Resolve(
            SnapSyncState? snapState,
            ulong lastBlock,
            MainnetChainServerConfig config)
        {
            ulong effectiveStart;
            StartBlockReason reason;

            if (snapState is not null
                && snapState.Phase == SnapPhase.Complete
                && snapState.PivotBlockNumber > lastBlock)
            {
                effectiveStart = snapState.PivotBlockNumber + 1;
                reason = StartBlockReason.PostSnapPivotFastStart;
            }
            else if (lastBlock > 0)
            {
                effectiveStart = lastBlock + 1;
                reason = StartBlockReason.ResumeFromLastBlock;
            }
            else
            {
                effectiveStart = config.StartBlock;
                reason = StartBlockReason.FreshStart;
            }

            ulong? endBlock = config.Blocks == ulong.MaxValue
                ? null
                : effectiveStart + config.Blocks - 1;

            return new EffectiveBounds(effectiveStart, endBlock, reason);
        }

        public static FollowerOptions BuildOptions(
            EffectiveBounds bounds,
            MainnetChainServerConfig config)
            => new FollowerOptions(
                StartBlock: bounds.StartBlock,
                CheckpointEvery: config.CheckpointEvery,
                AnchorEvery: 0UL,
                EndBlock: bounds.EndBlock,
                KeepLatestCheckpoints: config.KeepLatestCheckpoints);
    }
}

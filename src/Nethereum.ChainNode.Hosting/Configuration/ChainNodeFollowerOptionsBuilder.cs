using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeFollowerOptionsBuilder
    {
        public enum EffectiveStartBlockReason
        {
            FreshStart,
            ResumeFromLastBlock,
            PostSnapPivotFastStart,
        }

        public readonly record struct EffectiveBounds(
            ulong StartBlock,
            ulong? EndBlock,
            EffectiveStartBlockReason Reason);

        public static EffectiveBounds Resolve(
            ChainNodeSyncConfig sync,
            SnapSyncState? snapState,
            ulong lastBlock)
        {
            ulong effectiveStart;
            EffectiveStartBlockReason reason;

            if (snapState is not null
                && snapState.Phase == SnapPhase.Complete
                && snapState.PivotBlockNumber > lastBlock)
            {
                effectiveStart = snapState.PivotBlockNumber + 1;
                reason = EffectiveStartBlockReason.PostSnapPivotFastStart;
            }
            else if (lastBlock > 0)
            {
                effectiveStart = lastBlock + 1;
                reason = EffectiveStartBlockReason.ResumeFromLastBlock;
            }
            else
            {
                effectiveStart = sync.StartBlock;
                reason = EffectiveStartBlockReason.FreshStart;
            }

            ulong? endBlock = sync.Blocks == ulong.MaxValue
                ? null
                : effectiveStart + sync.Blocks - 1;

            return new EffectiveBounds(effectiveStart, endBlock, reason);
        }

        public static FollowerOptions BuildFollowerOptions(EffectiveBounds bounds, ChainNodeSyncConfig sync) =>
            new FollowerOptions(
                StartBlock: bounds.StartBlock,
                CheckpointEvery: sync.CheckpointEvery,
                AnchorEvery: 0UL,
                EndBlock: bounds.EndBlock,
                KeepLatestCheckpoints: sync.KeepLatestCheckpoints);

        public static StrictValidationPolicy BuildStrictValidationPolicy(
            ChainNodeSyncConfig sync, ILogger<StrictValidationPolicy>? logger = null) =>
            new StrictValidationPolicy(sync.ContinueOnMismatch, anchorEvery: 0, logger: logger);
    }
}

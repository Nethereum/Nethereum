using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Server.Configuration;

namespace Nethereum.MainnetChain.Server.Hosting
{
    /// <summary>
    /// Computes the <see cref="FollowerOptions.StartBlock"/> the follower
    /// loop should resume at, given the persisted state of the bundle and
    /// the user's configured <see cref="MainnetChainServerConfig.StartBlock"/>.
    ///
    /// <para>Three resume modes, in priority order:</para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// <b>Post-snap fast-start.</b> When <see cref="IChainMetadataStore.GetSnapSyncState"/>
    /// reports <see cref="SnapPhase.Complete"/> and the executor cursor
    /// (<see cref="IChainMetadataStore.GetLastBlock"/>) has not yet
    /// advanced past <see cref="SnapSyncState.PivotBlockNumber"/>, the
    /// follower must resume at <c>pivot + 1</c>. Blocks 1..pivot were
    /// never fetched by the snap path (state was streamed wholesale at the
    /// pivot); attempting to execute from block 1 against pivot-state
    /// would crash on the first missing ancestor-hash lookup.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Normal resume.</b> When the metadata cursor is non-zero (a
    /// prior follower run committed blocks), resume at
    /// <c>lastBlock + 1</c>. This covers both the no-snap path and the
    /// post-snap path once the executor has already moved past the pivot.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Fresh start.</b> No snap state, no committed blocks — honour
    /// <see cref="MainnetChainServerConfig.StartBlock"/> as-is so an
    /// operator-configured replay window still applies.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    public static class EffectiveStartBlockResolver
    {
        /// <summary>Outcome of <see cref="Resolve"/>.</summary>
        /// <param name="StartBlock">Block number the follower stream should begin at.</param>
        /// <param name="EndBlock">
        /// Upper bound passed to <see cref="FollowerOptions.EndBlock"/>. Null when
        /// <see cref="MainnetChainServerConfig.Blocks"/> is <see cref="ulong.MaxValue"/>;
        /// otherwise computed relative to <paramref name="StartBlock"/> so a finite
        /// replay window remains a window of <c>Blocks</c> blocks regardless of
        /// where resume actually picks up.
        /// </param>
        /// <param name="Reason">Which resume branch was taken.</param>
        public readonly record struct EffectiveBounds(
            ulong StartBlock,
            ulong? EndBlock,
            StartBlockReason Reason);

        /// <summary>Diagnostic label for which branch produced the result.</summary>
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

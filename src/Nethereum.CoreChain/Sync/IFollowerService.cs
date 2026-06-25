using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Single-writer executor loop. Pulls bundles from the source, runs them
    /// through an <see cref="IBlockExecutor"/>, verifies the post-state root,
    /// consults <see cref="ICanonicalStateRootSource"/> per the supplied
    /// <see cref="IValidationPolicy"/>, and routes divergence outcomes
    /// through the policy. The follower owns the bundle's lifecycle for the
    /// duration of <see cref="RunAsync"/>; the bundle is created from the
    /// supplied factory and disposed before the method returns.
    /// Snapshot-restore is deferred to the caller: when the rewind
    /// coordinator returns a snapshot recommendation, the follower returns
    /// <see cref="FollowerExitReason.SnapshotRestoreRequested"/> and the
    /// caller performs the restore before invoking <see cref="RunAsync"/>
    /// again with a refreshed factory.
    /// </summary>
    public interface IFollowerService
    {
        Task<FollowerRunResult> RunAsync(
            IBlockSource source,
            Func<IChainStoreBundle> bundleFactory,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            CancellationToken ct,
            ILogger logger = null);
    }

    /// <summary>Tunable knobs for the executor loop.</summary>
    /// <param name="StartBlock">Block number to ask the source for.</param>
    /// <param name="CheckpointEvery">Save a checkpoint every N executed blocks. 0 disables.</param>
    /// <param name="AnchorEvery">Periodic anchor cadence (informational; final say belongs to <see cref="IValidationPolicy.ShouldAnchorAt"/>).</param>
    /// <param name="MaxConsecutiveDivergences">Hard stop after this many state-root divergences in a row; protects against rewind loops.</param>
    /// <param name="MaxRewindCycles">Hard stop after this many cumulative rewind cycles within one run.</param>
    /// <param name="EndBlock">Optional upper bound. When set, RunAsync returns SourceCompleted once lastCommittedBlock reaches this number. Null disables the cap (default).</param>
    /// <param name="MaxConsecutiveSourceFailures">Hard stop after this many consecutive source/peer fetch failures (transient network errors, peer timeouts, scheduler exhaustion). Much higher than MaxConsecutiveDivergences because peer flakiness is normal under load — mainnet body-fetch failure rates can exceed 20% during congestion and a sweep of bad peers can burn 30 attempts in 5 minutes. Default 120 (~20 min at 10s/attempt) gives the scheduler enough budget to recycle through peer pool and reseed via DNS before we conclude the source is truly stuck.</param>
    /// <param name="KeepLatestCheckpoints">Cap on how many checkpoints to retain on disk. After each successful SaveCheckpointAsync, older checkpoints are dropped so only the most recent N remain (oldest deleted first). Null = unlimited (the historical default; safe for short runs but every checkpoint adds gigabytes on mainnet — 1 mainnet sync exhausted 90 GB of disk after 29 checkpoints). Set to ~5 for production sync runs; the most-recent checkpoint is the safe rewind anchor and a couple more give headroom for journal-rewind failure.</param>
    /// <param name="TipPollInterval">When the tip-poll loop is active (the follower was constructed with an <see cref="ICanonicalStateRootSource"/> AND a backward-walker delegate), the cadence at which <c>GetLatestAsync</c> is polled. Default is the mainnet slot of 12 seconds; matches a consensus-driven head push cadence (we poll for the same downstream effect). Ignored when the tip-poll loop is inactive.</param>
    /// <param name="WalkerInvocationThreshold">When the tip advances by more than this many blocks beyond the local cursor, the walker is invoked to skeleton-fill the gap before forward execution. Default 1 — invoke for any non-zero delta in steady state. Tune up for high-throughput follower scenarios where tiny one-block deltas are better absorbed by the forward executor without walker overhead.</param>
    public sealed record FollowerOptions(
        ulong StartBlock,
        ulong CheckpointEvery,
        ulong AnchorEvery,
        int MaxConsecutiveDivergences = 3,
        int MaxRewindCycles = 3,
        ulong? EndBlock = null,
        int MaxConsecutiveSourceFailures = 120,
        int? KeepLatestCheckpoints = null,
        TimeSpan? TipPollInterval = null,
        ulong WalkerInvocationThreshold = 1UL,
        ulong MaxReorgDepth = 1024UL);

    /// <summary>Summary of how a follower run ended.</summary>
    /// <param name="ExitReason">Terminal outcome.</param>
    /// <param name="LastExecutedBlock">Cursor at exit time.</param>
    /// <param name="BlocksExecuted">Number of bundles successfully executed (root matched).</param>
    /// <param name="RootMismatches">Number of bundles whose post-state root failed verification.</param>
    /// <param name="RewindCyclesUsed">Number of rewind cycles invoked during this run.</param>
    /// <param name="SnapshotRestoreTarget">When <see cref="ExitReason"/> is <see cref="FollowerExitReason.SnapshotRestoreRequested"/>, the checkpoint block the caller should restore to. Null otherwise.</param>
    /// <param name="Detail">Human-readable reason for the exit.</param>
    public sealed record FollowerRunResult(
        FollowerExitReason ExitReason,
        ulong LastExecutedBlock,
        ulong BlocksExecuted,
        ulong RootMismatches,
        ulong RewindCyclesUsed,
        ChainCheckpoint? SnapshotRestoreTarget,
        string Detail);

    public enum FollowerExitReason
    {
        SourceCompleted,
        Cancelled,
        FatalVerdict,
        RewindUnavailable,
        SnapshotRestoreRequested,
        SourceUnavailable,
    }
}

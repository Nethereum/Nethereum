using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Pull-shaped contract that yields ordered, self-consistent
    /// <see cref="BlockBundle"/>s starting at <paramref name="fromBlock"/>.
    /// Implementations may fetch in parallel internally; the returned
    /// stream is sequential and intra-batch parent-hash validated. The
    /// follower verifies the post-state root against canonical.
    /// </summary>
    public interface IBlockSource
    {
        /// <summary>
        /// Stream bundles. Parent-hash continuity within the stream is the
        /// source's responsibility. The source completes the stream when
        /// it has nothing more to yield or when <paramref name="ct"/> is
        /// cancelled.
        /// </summary>
        IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            CancellationToken ct);

        /// <summary>
        /// Self-assessed health. Callers consult this before periodic
        /// anchor checks and on consecutive divergences.
        /// </summary>
        Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct);

        /// <summary>
        /// Tells the source that a bundle previously yielded at
        /// <paramref name="blockNumber"/> was rejected by the executor.
        /// Sources may use this to demote or ban the peer / channel that
        /// served it.
        /// </summary>
        Task ReportBadBundleAsync(
            ulong blockNumber,
            BadBundleReason reason,
            CancellationToken ct);

        /// <summary>
        /// When the source terminates the stream because peers report a
        /// chain it cannot reconcile with local state, this records the
        /// shape of the disagreement. The follower consults this after
        /// stream completion and fabricates a <see cref="DivergenceVerdict"/>
        /// routed through <see cref="IValidationPolicy.OnVerdict"/>, so
        /// chain-break and state-root mismatch take the same recovery path.
        /// Null when the stream completed normally (exhausted, cancelled,
        /// or peer-level errors handled internally).
        /// </summary>
        DivergenceSignal LastChainBreak { get; }
    }

    /// <summary>
    /// Surfaced by <see cref="IBlockSource.LastChainBreak"/> when peers
    /// agree on a chain the local node cannot follow. Carries enough to
    /// build a <see cref="Nethereum.CoreChain.Validation.DivergenceVerdict"/>
    /// without coupling the source to validation types.
    /// </summary>
    public sealed record DivergenceSignal(
        ulong AtBlock,
        byte[] PeerParentHash,
        byte[] OurParentHash,
        int QuorumPeerCount,
        string SourceName);

    public enum BlockSourceHealth { Healthy, Degraded, Unavailable }

    public enum BadBundleReason
    {
        WrongParent,
        StateRootMismatch,
        TxValidationFailed,
        MalformedRlp,
    }
}

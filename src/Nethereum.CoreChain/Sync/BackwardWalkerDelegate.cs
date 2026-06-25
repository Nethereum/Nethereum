using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// CoreChain-side abstraction over the skeleton walker the DevP2P.Sync
    /// layer ships. <see cref="FollowerService"/> takes this delegate so the
    /// tip-poll loop can be wired without CoreChain depending on the
    /// DevP2P.Sync project. Production wiring adapts the concrete walker's
    /// WalkAsync to this delegate at composition time.
    /// </summary>
    /// <param name="fromBlockNumber">Trusted-tip block number; walk descends from here.</param>
    /// <param name="fromHash">Trusted-tip block hash; anchors the parent-hash chain.</param>
    /// <param name="toBlockNumber">Stop when the walker's bottom header reaches this block.</param>
    /// <param name="bundle">Bundle whose local storage backs the walk's resume predicate.</param>
    /// <param name="ct">Cancellation token.</param>
    public delegate Task<WalkerOutcome> BackwardWalkerDelegate(
        ulong fromBlockNumber,
        byte[] fromHash,
        ulong toBlockNumber,
        IChainStoreBundle bundle,
        CancellationToken ct);

    /// <summary>
    /// Slim adapter shape returned by <see cref="BackwardWalkerDelegate"/>. The
    /// concrete walker in <c>Nethereum.DevP2P.Sync</c> returns a richer
    /// <c>WalkResult</c> with skeleton bounds + body counts; the tip-poll loop
    /// only needs the exit reason, the number of headers persisted, and the
    /// divergence block (when applicable).
    /// </summary>
    public sealed record WalkerOutcome(
        WalkerExitReason ExitReason,
        ulong HeadersWritten,
        ulong? DivergenceBlock);

    /// <summary>
    /// Why a backward walker invocation stopped. Owned by CoreChain so the
    /// tip-poll loop and the concrete <c>BackwardBlockWalker</c> in
    /// <c>Nethereum.DevP2P.Sync</c> share a single definition (avoids the
    /// silent-bug-class of two positionally-aligned enums getting out of sync).
    /// </summary>
    public enum WalkerExitReason
    {
        /// <summary>Lowest persisted header reached the caller-specified target.</summary>
        ReachedTarget,
        /// <summary>Batch's bottom header was found in local storage with matching hash; resume succeeded.</summary>
        MetExistingStore,
        /// <summary>Walker hit block 0.</summary>
        StructuralGenesis,
        /// <summary>Batch's bottom block was in local storage with a DIFFERENT hash; caller triggers findAncestor + rewind.</summary>
        LastKnownGoodDivergence,
        /// <summary>Header fetch could not be satisfied across the walker's anchor-retry budget.</summary>
        PeerPoolEmpty,
        /// <summary>Cancellation token tripped during the walk.</summary>
        Cancelled,
    }
}

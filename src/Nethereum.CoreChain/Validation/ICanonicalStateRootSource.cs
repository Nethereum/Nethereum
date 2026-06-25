using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    /// <summary>
    /// Authoritative source of the canonical post-state root (and block
    /// hash) for a given block number, plus the latest canonical tip.
    /// Plugged into sync (pivot selection + post-execution validation),
    /// AppChain followers, anchoring proof producers, and audit-replay tools
    /// any time we need an out-of-band cross-check on a block's identity or
    /// the current chain head.
    ///
    /// <para>
    /// Why an interface, not a concrete RPC class: the "what's canonical
    /// at block N" question has many equivalent answers depending on
    /// context. Each implementation has different cost, latency, and
    /// trust assumptions; callers compose them via
    /// <see cref="CompositeCanonicalStateRootSource"/>.
    /// </para>
    ///
    /// <para>
    /// Concrete implementations:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>RpcCanonicalSource</c> (SyncNode): calls a trusted
    ///     JSON-RPC node (e.g. Infura / Alchemy / a local execution client).
    ///     Cheap, immediate, trust = the operator's choice of RPC.</item>
    ///   <item><c>LightClientCanonicalSource</c>
    ///     (Nethereum.Consensus.LightClient): backed by
    ///     <c>ITrustedHeaderProvider</c>. Trust = beacon sync-committee BLS
    ///     quorum. Mirrors the Engine API <c>engine_forkchoiceUpdatedV3</c>
    ///     head ingestion, in-process.</item>
    ///   <item><c>AppChainAnchorCanonicalSource</c> (AppChain follower,
    ///     future): reads the L1 anchor contract's commitment for L2
    ///     block N. Trust = L1 finality. The whole point of anchoring.</item>
    ///   <item><c>SignedCheckpointSource</c> (future): verifies a
    ///     signed checkpoint from a trusted peer / governance
    ///     committee.</item>
    ///   <item><c>MainnetKnownCheckpoints</c>: offline hardcoded table of
    ///     famous mainnet blocks. Point lookup only; returns null for
    ///     "latest" (checkpoints aren't a tip concept).</item>
    /// </list>
    ///
    /// <para>
    /// Two methods serve different sync stages:
    /// <see cref="GetCanonicalAsync"/> is the point-lookup used by the
    /// follower for post-execution validation. <see cref="GetLatestAsync"/>
    /// is the tip-discovery used by the snap-bootstrap pivot picker and
    /// the skeleton-sync anchor — replaces peer-trusted
    /// <c>PeerLatestBlock</c> sampling with a source whose authority is
    /// external to the DevP2P peer set.
    /// </para>
    /// </summary>
    public interface ICanonicalStateRootSource
    {
        /// <summary>
        /// Short human-readable label for logging — "RPC(infura.io)",
        /// "AppChainAnchor(0xabc)", "Beacon(finalized)". Surfaces in the
        /// divergence diagnosis output so operators can tell which source
        /// answered.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Look up the canonical state root and block hash at
        /// <paramref name="blockNumber"/>. Returns <c>(null, null)</c>
        /// when this source has no answer (chain not yet finalised at
        /// that height, anchor not yet posted, RPC pruned the block,
        /// etc.). Throws only on transport / parse errors that the caller
        /// should treat as transient.
        /// </summary>
        Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct);

        /// <summary>
        /// Returns the source's view of the current canonical tip, or
        /// <c>null</c> when this source has no "latest" concept
        /// (e.g. <c>MainnetKnownCheckpoints</c>) or has no tip data yet
        /// (CL not synced, no anchor posted). Callers MUST handle null by
        /// retrying with backoff or falling through a composite chain.
        /// </summary>
        Task<CanonicalTip> GetLatestAsync(CancellationToken ct);
    }

    /// <summary>
    /// Tip descriptor returned by <see cref="ICanonicalStateRootSource.GetLatestAsync"/>.
    /// The snap-bootstrap pivot picker takes <see cref="BlockNumber"/> and
    /// subtracts a safety distance (a 64-block trail) to
    /// avoid micro-reorg-driven state-root invalidation. The optional
    /// <see cref="BlockHash"/> and <see cref="StateRoot"/> let the caller
    /// cross-check the peer-fetched header at that block.
    /// </summary>
    public sealed class CanonicalTip
    {
        /// <summary>Always populated.</summary>
        public ulong BlockNumber { get; set; }

        /// <summary>Optional; null/empty when the source only knows the height.</summary>
        public byte[] BlockHash { get; set; } = System.Array.Empty<byte>();

        /// <summary>Optional; null/empty when the source only knows the height.</summary>
        public byte[] StateRoot { get; set; } = System.Array.Empty<byte>();
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    /// <summary>
    /// Authoritative source of the canonical post-state root (and block
    /// hash) for a given block number. Plugged into sync, AppChain
    /// followers, anchoring proof producers, and audit-replay tools any
    /// time we need an out-of-band cross-check on a block's identity.
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
    ///     JSON-RPC node (Infura / Alchemy / local geth or erigon).
    ///     Cheap, immediate, trust = the operator's choice of RPC.</item>
    ///   <item><c>AppChainAnchorCanonicalSource</c> (AppChain follower,
    ///     future): reads the L1 anchor contract's commitment for L2
    ///     block N. Trust = L1 finality. The whole point of anchoring.</item>
    ///   <item><c>BeaconFinalitySource</c> (future, post-merge): asks the
    ///     consensus client which post-merge block is finalised; lower
    ///     bound on what's canonical.</item>
    ///   <item><c>SignedCheckpointSource</c> (future): verifies a
    ///     signed checkpoint from a trusted peer / governance
    ///     committee.</item>
    /// </list>
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
    }
}

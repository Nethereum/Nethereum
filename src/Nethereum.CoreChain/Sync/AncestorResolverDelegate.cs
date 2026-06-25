using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// CoreChain-side abstraction over the binary-search ancestor finder the
    /// DevP2P.Sync layer ships. <see cref="FollowerService"/> takes this delegate
    /// so the tip-poll loop can invoke <c>findAncestor</c> on
    /// <see cref="WalkerExitReason.LastKnownGoodDivergence"/> without CoreChain
    /// depending on the DevP2P.Sync project. Mirrors the
    /// <see cref="BackwardWalkerDelegate"/> shape: production wiring adapts
    /// <c>AncestorResolver.FindAsync</c> to this delegate at composition time.
    /// </summary>
    /// <param name="divergedBlock">Block at which the walker reported a hash mismatch (upper bound, inclusive).</param>
    /// <param name="floorBlock">Lower bound of the search (inclusive); typically <c>Metadata.GetLastBlock</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The highest block number whose hash agrees across local store and the canonical chain.</returns>
    public delegate Task<ulong> AncestorResolverDelegate(
        ulong divergedBlock,
        ulong floorBlock,
        CancellationToken ct);
}

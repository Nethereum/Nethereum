using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Sync
{
    public delegate Task<WalkerOutcome> BackwardWalkerDelegate(
        ulong fromBlockNumber,
        byte[] fromHash,
        ulong toBlockNumber,
        IChainStoreBundle bundle,
        CancellationToken ct,
        ulong noShortCircuitAboveBlock = ulong.MaxValue);

    public sealed record WalkerOutcome(
        WalkerExitReason ExitReason,
        ulong HeadersWritten,
        ulong? DivergenceBlock,
        ulong SkeletonBottomBlock = 0,
        bool MetExistingStore = false);

    public enum WalkerExitReason
    {
        ReachedTarget,
        MetExistingStore,
        StructuralGenesis,
        LastKnownGoodDivergence,
        PeerPoolEmpty,
        Cancelled,
    }
}

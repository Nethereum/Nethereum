using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed record ResumePlan(SnapSyncState? ResumeFrom, bool SkipPhase2)
    {
        internal static readonly ResumePlan None = new ResumePlan(null, false);
    }
}

using PivotState = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.PivotState;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed record StateSyncOutcome(SnapSyncClient.SyncResult Result, bool HealEntered, PivotState ConvergedPivot);
}

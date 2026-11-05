using System;
using Nethereum.CoreChain.Storage;
using SnapStorageCompletenessGateReport = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.SnapStorageCompletenessGateReport;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal static class StorageCompletenessGate
    {
        internal static SnapStorageCompletenessGateReport Build(IChainStoreBundle bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            var report = new SnapStorageCompletenessGateReport();
            var blob = bundle.Metadata.GetDeferredHealAccountsBlob();
            if (blob != null)
            {
                report.DeferredHealBlobPresent = true;
                report.PendingDeferredHealAccounts = NeedsHealAccountsCodec.Decode(blob).Count;
            }

            if (bundle is IFlatStateReconciler damageStore)
                report.DamagedStorageInventories += damageStore.GetPersistedDamage().Count;

            return report;
        }
    }
}

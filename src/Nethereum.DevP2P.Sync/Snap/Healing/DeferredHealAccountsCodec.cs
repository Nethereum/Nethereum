using System;
using System.Collections.Generic;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    [Obsolete("Renamed: use NeedsHealAccountsCodec. Accounts that need heal are healed at the live pivot; nothing is deferred.")]
    public static class DeferredHealAccountsCodec
    {
        public static byte[] Encode(IReadOnlyList<SnapSyncClient.AccountNeedingHeal> accounts)
            => NeedsHealAccountsCodec.Encode(accounts);

        internal static List<SnapSyncClient.AccountNeedingHeal> Union(
            byte[] blob, IEnumerable<SnapSyncClient.AccountNeedingHeal> more)
            => NeedsHealAccountsCodec.Union(blob, more);

        public static List<SnapSyncClient.AccountNeedingHeal> Decode(byte[] blob)
            => NeedsHealAccountsCodec.Decode(blob);
    }
}

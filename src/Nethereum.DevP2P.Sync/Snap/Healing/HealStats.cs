using System.Collections.Generic;
using System.Linq;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal sealed class HealStats
    {
        internal int TopBranchesHealed;
        internal long PrunedChildren;
        internal long RequiredAbsent;
        internal long RequiredStale;
        internal readonly Dictionary<string, long> StorageOwnerRequires = new();

        internal string TopStorageOwners(int top = 5)
        {
            if (StorageOwnerRequires.Count == 0) return "-";
            return string.Join(",", StorageOwnerRequires
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => $"{kv.Key}:{kv.Value}"));
        }
    }
}

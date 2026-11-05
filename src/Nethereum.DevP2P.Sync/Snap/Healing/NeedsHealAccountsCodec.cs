using System.Collections.Generic;
using System.Linq;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "NeedsHealAccountsCodec — persist the accounts a heal must visit")]
    public static class NeedsHealAccountsCodec
    {
        private const int EntrySize = 64;

        public static byte[] Encode(IReadOnlyList<SnapSyncClient.AccountNeedingHeal> accounts)
        {
            if (accounts == null || accounts.Count == 0) return System.Array.Empty<byte>();
            var blob = new byte[accounts.Count * EntrySize];
            for (int i = 0; i < accounts.Count; i++)
            {
                accounts[i].AccountHash.CopyTo(blob, i * EntrySize);
                accounts[i].ExpectedStorageRoot.CopyTo(blob, i * EntrySize + 32);
            }
            return blob;
        }

        internal static List<SnapSyncClient.AccountNeedingHeal> Union(
            byte[] blob, IEnumerable<SnapSyncClient.AccountNeedingHeal> more)
        {
            var union = Decode(blob);
            var seen = new HashSet<byte[]>(union.Select(a => a.AccountHash), Nethereum.Util.ByteArrayComparer.Current);
            if (more == null) return union;
            foreach (var account in more)
                if (seen.Add(account.AccountHash))
                    union.Add(account);
            return union;
        }

        public static List<SnapSyncClient.AccountNeedingHeal> Decode(byte[] blob)
        {
            var list = new List<SnapSyncClient.AccountNeedingHeal>();
            if (blob == null || blob.Length == 0 || blob.Length % EntrySize != 0) return list;
            for (int offset = 0; offset + EntrySize <= blob.Length; offset += EntrySize)
            {
                var accountHash = new byte[32];
                var storageRoot = new byte[32];
                System.Buffer.BlockCopy(blob, offset, accountHash, 0, 32);
                System.Buffer.BlockCopy(blob, offset + 32, storageRoot, 0, 32);
                list.Add(new SnapSyncClient.AccountNeedingHeal(accountHash, storageRoot));
            }
            return list;
        }
    }
}

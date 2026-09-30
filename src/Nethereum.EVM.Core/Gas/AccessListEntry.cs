using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.EVM.Gas
{
    public class AccessListEntry
    {
        public string Address { get; set; }
        public IList<string> StorageKeys { get; set; }

        public static List<AccessListEntry> From(List<AccessListItem> items)
        {
            if (items == null) return null;

            var entries = new List<AccessListEntry>(items.Count);
            foreach (var item in items)
                entries.Add(From(item));
            return entries;
        }

        private static AccessListEntry From(AccessListItem item)
        {
            var storageKeys = new List<string>();
            if (item.StorageKeys != null)
                foreach (var key in item.StorageKeys)
                    storageKeys.Add(key.ToHex(true));

            return new AccessListEntry { Address = item.Address, StorageKeys = storageKeys };
        }
    }
}

using System.Collections.Generic;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// EIP-2930 (Berlin) access list gas:
    /// <c>2400 × addressCount + 1900 × storageKeyCount</c>.
    /// </summary>
    public sealed class Eip2930AccessListGasRule : IAccessListGasRule
    {
        private const long G_ACCESS_LIST_ADDRESS = 2400;
        private const long G_ACCESS_LIST_STORAGE = 1900;

        public static readonly Eip2930AccessListGasRule Instance = new Eip2930AccessListGasRule();

        public long CalculateGas(IList<AccessListEntry> accessList)
        {
            if (accessList == null) return 0;
            long gas = 0;
            foreach (var entry in accessList)
            {
                gas += G_ACCESS_LIST_ADDRESS;
                if (entry.StorageKeys != null)
                {
                    gas += (long)entry.StorageKeys.Count * G_ACCESS_LIST_STORAGE;
                }
            }
            return gas;
        }
    }
}

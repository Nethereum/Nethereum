using System.Collections.Generic;

namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip7981AccessListGasRule : IAccessListGasRule, IAccessListFloorRule
    {
        private const long G_ACCESS_LIST_ADDRESS = GasConstants.EIP8038_COLD_ACCOUNT_ACCESS - GasConstants.WARM_STORAGE_READ_COST;
        private const long G_ACCESS_LIST_STORAGE = GasConstants.COLD_SLOAD_COST - GasConstants.WARM_STORAGE_READ_COST;

        private const long ADDRESS_FLOOR_TOKENS = 80;
        private const long STORAGE_KEY_FLOOR_TOKENS = 128;

        public static readonly Eip7981AccessListGasRule Instance = new Eip7981AccessListGasRule();

        public long CalculateGas(IList<AccessListEntry> accessList) =>
            Eip2930EntryCost(accessList) + FloorPerTokenGas(accessList);

        private static long Eip2930EntryCost(IList<AccessListEntry> accessList)
        {
            if (accessList == null) return 0;
            long gas = 0;
            foreach (var entry in accessList)
            {
                gas += G_ACCESS_LIST_ADDRESS;
                if (entry.StorageKeys != null)
                    gas += (long)entry.StorageKeys.Count * G_ACCESS_LIST_STORAGE;
            }
            return gas;
        }

        public long FloorTokensInAccessList(IList<AccessListEntry> accessList)
        {
            if (accessList == null) return 0;
            long tokens = 0;
            foreach (var entry in accessList)
            {
                tokens += ADDRESS_FLOOR_TOKENS;
                if (entry.StorageKeys != null)
                {
                    tokens += (long)entry.StorageKeys.Count * STORAGE_KEY_FLOOR_TOKENS;
                }
            }
            return tokens;
        }

        public long FloorPerTokenGas(IList<AccessListEntry> accessList)
        {
            return GasConstants.EIP7976_FLOOR_PER_TOKEN_GAS * FloorTokensInAccessList(accessList);
        }
    }
}

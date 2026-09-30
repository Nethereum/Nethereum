using System.Collections.Generic;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.BlockchainState
{
    public interface IAccountStorageReader
    {
#if EVM_SYNC
        bool AccountHasStorage(string address);
#else
        Task<bool> AccountHasStorageAsync(string address);
#endif
    }

    public static class AccountStorageValues
    {
        public static bool AnyNonZero(IEnumerable<byte[]> values)
        {
            if (values == null) return false;
            foreach (var value in values)
            {
                if (!ByteUtil.IsZero(value)) return true;
            }
            return false;
        }
    }
}

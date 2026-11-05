using System.Numerics;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public interface IDirtyKeyObserver
    {
        void OnAccountDirty(EvmAddress address);

        void OnStorageSlotDirty(EvmAddress address, BigInteger slot);
    }
}

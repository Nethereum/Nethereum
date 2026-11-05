using System.Numerics;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public interface IPendingFlushFlatOverlay
    {
        bool TryGetAccount(EvmAddress address, out Account account, out bool isDeleted);

        bool TryGetStorage(EvmAddress address, BigInteger slot, out byte[] value, out bool isCleared);

        bool TryGetCode(byte[] codeHash, out byte[] code);
    }
}

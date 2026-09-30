using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IPendingFlushFlatOverlay
    {
        bool TryGetAccount(string address, out Account account, out bool isDeleted);

        bool TryGetStorage(string address, BigInteger slot, out byte[] value, out bool isCleared);

        bool TryGetCode(byte[] codeHash, out byte[] code);
    }
}

using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public interface IAddressHashCache
    {
        bool TryGetAddressHash(EvmAddress address, out byte[] hash);

        void SetAddressHash(EvmAddress address, byte[] hash);

        void ClearAddressHashCache();
    }
}

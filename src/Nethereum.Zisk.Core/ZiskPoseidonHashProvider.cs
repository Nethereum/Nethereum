using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.Zisk.Core
{
    public class ZiskPoseidonHashProvider : IHashProvider
    {
        // Uses managed PoseidonEvmHasher for now.
        // When zkvm_poseidon2 P/Invoke linking is resolved (DMA memcpy issue),
        // switch to native syscall for ~10x performance in the Zisk guest.
        private readonly PoseidonPairHashProvider _managed = new PoseidonPairHashProvider();

        public byte[] ComputeHash(byte[] data)
        {
            return _managed.ComputeHash(data);
        }
    }
}

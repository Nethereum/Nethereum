using Nethereum.Util.HashProviders;

namespace Nethereum.Zisk.Core
{
    public class ZiskPoseidonHashProvider : IHashProvider
    {
        public byte[] ComputeHash(byte[] data)
        {
            return new byte[32];
        }
    }
}

using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskRipemd160Backend : IRipemd160Backend
    {
        public static readonly ZiskRipemd160Backend Instance = new ZiskRipemd160Backend();

        public byte[] Hash(byte[] input)
        {
            var data = input ?? new byte[0];
            return Ripemd160.ComputeHash(data);
        }
    }
}

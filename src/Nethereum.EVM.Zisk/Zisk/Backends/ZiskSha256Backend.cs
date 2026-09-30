using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskSha256Backend : ISha256Backend
    {
        public static readonly ZiskSha256Backend Instance = new ZiskSha256Backend();

        public byte[] Hash(byte[] input)
        {
            var data = input ?? new byte[0];
            var output = new byte[32];
            uint status = ZiskCrypto.zkvm_sha256(data, (nuint)data.Length, output);
            if (status != 0) throw new System.ArgumentException($"SHA256 failed (status {status})");
            return output;
        }
    }
}

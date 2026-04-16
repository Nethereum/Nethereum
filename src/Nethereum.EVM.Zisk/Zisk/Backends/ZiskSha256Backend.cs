using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM SHA-256 backend for precompile 0x02. Wraps the native
    /// witness-backed <c>ZiskCrypto.sha256_c</c> primitive.
    /// </summary>
    public sealed class ZiskSha256Backend : ISha256Backend
    {
        public static readonly ZiskSha256Backend Instance = new ZiskSha256Backend();

        public byte[] Hash(byte[] input)
        {
            var data = input ?? new byte[0];
            var output = new byte[32];
            ZiskCrypto.sha256_c(data, (nuint)data.Length, output);
            return output;
        }
    }
}

using System.Security.Cryptography;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Precompiles.Backends
{
    public sealed class DefaultSha256Backend : ISha256Backend
    {
        public static readonly DefaultSha256Backend Instance = new DefaultSha256Backend();

        public byte[] Hash(byte[] input)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(input ?? new byte[0]);
            }
        }
    }
}

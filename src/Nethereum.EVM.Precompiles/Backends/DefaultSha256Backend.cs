using System.Security.Cryptography;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default SHA-256 backend for precompile 0x02 using
    /// <see cref="SHA256"/> from <c>System.Security.Cryptography</c>.
    /// Used by <c>DefaultPrecompileRegistries</c> in the production EVM
    /// assembly.
    /// </summary>
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

using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Org.BouncyCastle.Crypto.Digests;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default RIPEMD-160 backend for precompile 0x03 using
    /// BouncyCastle's <see cref="RipeMD160Digest"/>. Returns the raw
    /// 20-byte digest; the handler is responsible for right-padding to
    /// 32 bytes.
    /// </summary>
    public sealed class DefaultRipemd160Backend : IRipemd160Backend
    {
        public static readonly DefaultRipemd160Backend Instance = new DefaultRipemd160Backend();

        public byte[] Hash(byte[] input)
        {
            var data = input ?? new byte[0];
            var digest = new RipeMD160Digest();
            digest.BlockUpdate(data, 0, data.Length);
            var result = new byte[20];
            digest.DoFinal(result, 0);
            return result;
        }
    }
}

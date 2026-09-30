using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Org.BouncyCastle.Crypto.Digests;

namespace Nethereum.EVM.Precompiles.Backends
{
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

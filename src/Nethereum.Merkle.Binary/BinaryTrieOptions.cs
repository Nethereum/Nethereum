using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary
{
    public delegate byte[] NodeResolverFunc(byte[] path, byte[] hash);

    public class BinaryTrieOptions
    {
        public IHashProvider HashProvider { get; set; }
        public NodeResolverFunc NodeResolver { get; set; }

        public static BinaryTrieOptions Default => new BinaryTrieOptions
        {
            HashProvider = new Sha256HashProvider()
        };
    }
}

using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Util;

namespace Nethereum.CoreChain.Services
{
    public class BinaryAccountProofResult
    {
        public string Address { get; set; }
        public byte Version { get; set; }
        public ulong Nonce { get; set; }
        public EvmUInt256 Balance { get; set; }
        public uint CodeSize { get; set; }
        public byte[] CodeHash { get; set; }
        public byte[] RootHash { get; set; }
        public BinaryTrieProof BasicDataProof { get; set; }
        public BinaryTrieProof CodeHashProof { get; set; }
    }
}

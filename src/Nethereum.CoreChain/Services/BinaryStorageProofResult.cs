using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Util;

namespace Nethereum.CoreChain.Services
{
    public class BinaryStorageProofResult
    {
        public string Address { get; set; }
        public EvmUInt256 Slot { get; set; }
        public byte[] Value { get; set; }
        public byte[] RootHash { get; set; }
        public BinaryTrieProof Proof { get; set; }
    }
}

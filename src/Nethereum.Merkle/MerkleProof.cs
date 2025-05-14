using System.Collections.Generic;

namespace Nethereum.Merkle
{
    public class MerkleProof
    {
        public List<byte[]> ProofNodes { get; set; } = new List<byte[]>();
    }

}
